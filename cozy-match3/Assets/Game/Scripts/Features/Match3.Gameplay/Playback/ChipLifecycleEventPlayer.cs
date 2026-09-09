using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Birth, death and morph of a single chip. The destruction animation is deliberately NOT
    /// awaited here: every destruction of a step has to run together and the ANIMATE barrier is
    /// what waits for them, otherwise the step serialises into 0.20 s per chip (§5.1, §11.3).
    /// </summary>
    public sealed class ChipLifecycleEventPlayer : ITurnEventPlayer
    {
        private readonly IMatch3Logger _logger;

        private static readonly TurnEventKind[] Handled =
        {
            TurnEventKind.ChipDestroyed,
            TurnEventKind.ChipSpawned,
            TurnEventKind.ChipTransformed
        };

        /// <summary>Last ComboStep offset seen, so a series is paced by its increments (§6.3).</summary>
        private int _lastTransformOffsetMs;

        public ChipLifecycleEventPlayer(IMatch3Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<TurnEventKind> Kinds => Handled;

        public async UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            TurnEvent e = context.Transcript.GetEvent(eventIndex);

            switch (e.Kind)
            {
                case TurnEventKind.ChipDestroyed:
                    StartDestruction(context, e, ct);
                    break;

                case TurnEventKind.ChipSpawned:
                    Spawn(context, e);
                    break;

                case TurnEventKind.ChipTransformed:
                    await MorphAsync(context, e, ct);
                    break;
            }
        }

        private static async UniTask DestroyAsync(
            PlaybackContext context,
            ChipView chip,
            int instanceId,
            CancellationToken ct)
        {
            TimingProfile timings = context.Timings;

            chip.Rect.DOScale(timings.DestroyPunchScale, timings.DestroyPunchDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(chip.gameObject);
            await UniTask.Delay(
                TimeSpan.FromSeconds(timings.DestroyPunchDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            chip.Rect.DOScale(0f, timings.DestroyFadeDuration)
                .SetEase(Ease.InQuad)
                .SetLink(chip.gameObject);
            chip.Image.DOFade(0f, timings.DestroyFadeDuration)
                .SetLink(chip.gameObject);
            await UniTask.Delay(
                TimeSpan.FromSeconds(timings.DestroyFadeDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            context.Board.DespawnChip(instanceId);
        }

        private void StartDestruction(PlaybackContext context, in TurnEvent e, CancellationToken ct)
        {
            if (!context.Board.TryGetChip(e.InstanceId, out ChipView chip))
            {
                _logger.Warn("ChipDestroyed for an unknown chip instance " + e.InstanceId.ToString());
                return;
            }

            context.TrackDestruction(DestroyAsync(context, chip, e.InstanceId, ct));
        }

        private static void Spawn(PlaybackContext context, in TurnEvent e)
        {
            // §11.3: a refilled chip starts one cell above the top row, outside the mask, and
            // falls in with the rest of the step's batch.
            ChipView chip = context.Board.SpawnChipAboveBoard(e.InstanceId, e.Color, e.A.X);
            context.TrackMove(chip, e.A, ChipMoveFlags.Fall);
        }

        private async UniTask MorphAsync(PlaybackContext context, TurnEvent e, CancellationToken ct)
        {
            // Amount is the ComboStep offset from the start of the plan, in milliseconds (§6.3).
            // Waiting its increment plays the mass transformation as a series instead of one
            // flash, and keeps the wait linear rather than quadratic.
            int waitMs = e.Amount > _lastTransformOffsetMs ? e.Amount - _lastTransformOffsetMs : e.Amount;
            _lastTransformOffsetMs = e.Amount;

            if (waitMs > 0)
            {
                await UniTask.Delay(waitMs, DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
            }

            if (!context.Board.TryGetChip(e.InstanceId, out ChipView chip))
            {
                _logger.Warn("ChipTransformed for an unknown chip instance " + e.InstanceId.ToString());
                return;
            }

            // The chip keeps its identity, so gravity and the following events still find it.
            chip.MorphToBooster(e.Booster);
        }
    }
}
