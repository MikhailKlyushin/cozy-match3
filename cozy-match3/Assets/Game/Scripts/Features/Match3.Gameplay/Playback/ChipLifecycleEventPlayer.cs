using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;
using UnityEngine;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// Birth, death and morph of a single chip. The destruction animation is deliberately NOT
    /// awaited here: every destruction of a step has to run together and the ANIMATE barrier is
    /// what waits for them, otherwise the step serialises into 0.20 s per chip (§5.1, §11.3).
    /// </summary>
    public sealed class ChipLifecycleEventPlayer : ITurnEventPlayer
    {
        private readonly FxRegistry _fx;
        private readonly ChipVisualProfile _chipProfile;
        private readonly IMatch3Logger _logger;

        private static readonly TurnEventKind[] Handled =
        {
            TurnEventKind.ChipDestroyed,
            TurnEventKind.ChipSpawned,
            TurnEventKind.ChipTransformed
        };

        /// <summary>Last ComboStep offset seen, so a series is paced by its increments (§6.3).</summary>
        private int _lastTransformOffsetMs;

        /// <summary>
        /// The destruction effect covers the whole animation, punch and fade alike. It is wider
        /// than the cell on purpose: cells sit edge to edge and the chips fill them, so an effect
        /// held inside one has no empty pixel to be seen against.
        /// </summary>
        private const float FxCells = 1.8f;

        /// <summary>Size the flash expands from, so it arrives as a burst rather than a state.</summary>
        private const float FxStartCells = 0.7f;

        public ChipLifecycleEventPlayer(FxRegistry fx, ChipVisualProfile chipProfile, IMatch3Logger logger)
        {
            _fx = fx ?? throw new ArgumentNullException(nameof(fx));
            _chipProfile = chipProfile != null
                ? chipProfile
                : throw new ArgumentNullException(nameof(chipProfile));
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

        private async UniTask DestroyAsync(
            PlaybackContext context,
            ChipView chip,
            int instanceId,
            ChipColor color,
            CancellationToken ct)
        {
            TimingProfile timings = context.Timings;
            FxView fx = RentDestroyFx(context, chip.Cell, color, timings.DestroyTotalDuration);

            try
            {
                await AnimateDestructionAsync(context, chip, instanceId, timings, ct);
            }
            finally
            {
                _fx.Release(fx);
            }
        }

        /// <summary>
        /// Null when the colour has no effect assigned, which stays legal: the timing plays in
        /// full and only the visual is missing (`art-and-fx-guide` §4.6).
        /// </summary>
        private FxView RentDestroyFx(PlaybackContext context, GridPos cell, ChipColor color, float duration)
        {
            FxView view = _fx.Rent(_chipProfile.GetDestroyFx(color));
            if (view == null)
            {
                return null;
            }

            float cellSize = context.Board.Layout.CellSize;
            // White: the colour lives in the sprite, as it does for every other chip visual (§3.2).
            view.Prepare(
                context.Board.Layout.CellCenter(cell),
                new Vector2(cellSize * FxStartCells, cellSize * FxStartCells),
                Color.white,
                duration);

            // A flash that neither grows nor fades reads as a blink, and a blink is what the eye
            // skips on a board this busy. §11.3 fixes the duration, so the size carries it.
            view.SizeTo(new Vector2(cellSize * FxCells, cellSize * FxCells), duration, Ease.OutQuad);
            view.FadeOut(duration);
            return view;
        }

        private static async UniTask AnimateDestructionAsync(
            PlaybackContext context,
            ChipView chip,
            int instanceId,
            TimingProfile timings,
            CancellationToken ct)
        {
            chip.Rect.DOScale(timings.DestroyPunchScale, timings.DestroyPunchDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(chip.gameObject);
            await UniTask.Delay(
                TimeSpan.FromSeconds(timings.DestroyPunchDuration),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                ct);

            // The view can be recycled mid-animation when something takes over its cell: it
            // belongs to a live chip then, and finishing the death would erase that one.
            if (chip.InstanceId != instanceId)
            {
                return;
            }

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

            context.Board.DespawnChip(instanceId, chip);
        }

        private void StartDestruction(PlaybackContext context, in TurnEvent e, CancellationToken ct)
        {
            if (!context.Board.TryGetChip(e.InstanceId, out ChipView chip))
            {
                _logger.Warn("ChipDestroyed for an unknown chip instance " + e.InstanceId.ToString());
                return;
            }

            context.TrackDestruction(DestroyAsync(context, chip, e.InstanceId, e.Color, ct));
        }

        private static void Spawn(PlaybackContext context, in TurnEvent e)
        {
            // §11.3: a refilled chip starts above the top row, outside the mask, and falls in
            // with the rest of the step's batch. The offset keeps the column's refills stacked:
            // REFILL releases one per pass, and they would otherwise share a single point.
            int stackOffset = context.NextSpawnStackOffset(e.A.X);
            ChipView chip = context.Board.SpawnChipAboveBoard(e.InstanceId, e.Color, e.A, stackOffset);
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
