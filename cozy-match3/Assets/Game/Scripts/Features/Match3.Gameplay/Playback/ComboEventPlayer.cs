using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Match3.Content;
using Match3.Core;
using Match3.Resolve;
using UnityEngine;

namespace Match3.Gameplay.Playback
{
    /// <summary>
    /// The shared flare of a booster combination (§6.3): the second participant slides into the
    /// epicentre and both flare. The sub-activations the plan spawns arrive as their own
    /// BoosterActivated events, so the combination is not replayed here.
    /// </summary>
    public sealed class ComboEventPlayer : ITurnEventPlayer
    {
        private readonly FxRegistry _fx;
        private readonly ChipVisualProfile _chipProfile;

        private static readonly TurnEventKind[] _kinds = { TurnEventKind.ComboActivated };

        /// <summary>Flare size before and after the burst, in cells.</summary>
        private const float FlareSize = 0.8f;
        private const float BurstCells = 3f;

        public ComboEventPlayer(FxRegistry fx, ChipVisualProfile chipProfile)
        {
            _fx = fx ?? throw new ArgumentNullException(nameof(fx));
            _chipProfile = chipProfile ?? throw new ArgumentNullException(nameof(chipProfile));
        }

        public IReadOnlyList<TurnEventKind> Kinds => _kinds;

        public UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ref readonly TurnEvent e = ref context.Transcript.GetEvent(eventIndex);

            // A is the epicentre and carries participant Booster; B holds ComboBoosterB (§6.3).
            return PlayBurstAsync(context, e.A, e.B, e.Booster, e.ComboBoosterB, ct);
        }

        private async UniTask PlayBurstAsync(
            PlaybackContext context,
            GridPos epicentre,
            GridPos other,
            BoosterType boosterA,
            BoosterType boosterB,
            CancellationToken ct)
        {
            TimingProfile timings = context.Timings;
            BoardLayout layout = context.Board.Layout;
            Vector2 centre = layout.CellCenter(epicentre);
            float cellSize = layout.CellSize;
            Vector2 flareSize = CellSquare(cellSize, FlareSize);

            FxView pull = _fx.Rent(FxFor(boosterB));
            FxView burst = _fx.Rent(FxFor(boosterA));

            try
            {
                // The pair merges over one mass-transform beat, then flares for one wave beat -
                // §11.3 has no combination duration of its own.
                if (pull != null)
                {
                    pull.Prepare(layout.CellCenter(other), flareSize, Color.white);
                    pull.MoveTo(centre, timings.MassTransformStagger);
                }

                await Wait(timings.MassTransformStagger, ct);

                if (pull != null)
                {
                    pull.FadeOut(timings.WaveBarrier);
                }

                if (burst != null)
                {
                    burst.Prepare(centre, flareSize, Color.white);
                    burst.SizeTo(CellSquare(cellSize, BurstCells), timings.WaveBarrier);
                    burst.FadeOut(timings.WaveBarrier);
                }

                await Wait(timings.WaveBarrier, ct);
            }
            finally
            {
                _fx.Release(pull);
                _fx.Release(burst);
            }
        }

        private static Vector2 CellSquare(float cellSize, float cells)
            => new Vector2(cellSize * cells, cellSize * cells);

        private static UniTask Wait(float seconds, CancellationToken ct)
            => seconds <= 0f
                ? UniTask.CompletedTask
                : UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);

        private GameObject FxFor(BoosterType booster) => _chipProfile.GetBoosterFx(booster);
    }
}
