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
    /// COMMIT and its rejection (§5.1): the two chips trade places over SwapDuration, or bounce
    /// out and back and end exactly where they started (E16).
    /// </summary>
    public sealed class SwapEventPlayer : ITurnEventPlayer
    {
        private readonly IMatch3Logger _logger;

        private static readonly TurnEventKind[] Handled =
        {
            TurnEventKind.SwapPerformed,
            TurnEventKind.SwapRejected
        };

        public SwapEventPlayer(IMatch3Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<TurnEventKind> Kinds => Handled;

        public async UniTask PlayAsync(PlaybackContext context, int eventIndex, CancellationToken ct)
        {
            TurnEvent e = context.Transcript.GetEvent(eventIndex);

            // A tap writes SwapPerformed(cell, cell) (D03): nothing travels, so nothing animates.
            if (e.A == e.B)
            {
                return;
            }

            // The swap events carry cells, not instance ids, so the participants are looked up by
            // the cell the views themselves claim. Twice per turn, never per cell.
            if (!context.Board.TryGetChipAt(e.A, out ChipView chipA)
                || !context.Board.TryGetChipAt(e.B, out ChipView chipB))
            {
                _logger.Warn("Swap between " + e.A.ToString() + " and " + e.B.ToString()
                             + " has no chip view on one of the cells");
                return;
            }

            Vector2 homeA = context.Board.Layout.CellCenter(e.A);
            Vector2 homeB = context.Board.Layout.CellCenter(e.B);

            context.Sfx.Play(e.Kind == TurnEventKind.SwapPerformed ? SfxId.Swap : SfxId.SwapRejected);

            if (e.Kind == TurnEventKind.SwapPerformed)
            {
                // Identity travels with the chip, so the logical cells change now and the tween
                // only catches the picture up.
                chipA.SetPosition(e.B, chipA.Rect.anchoredPosition);
                chipB.SetPosition(e.A, chipB.Rect.anchoredPosition);

                Move(chipA, homeB, context.Timings.SwapDuration);
                Move(chipB, homeA, context.Timings.SwapDuration);
                await DelayAsync(context.Timings.SwapDuration, ct);
                return;
            }

            Move(chipA, homeB, context.Timings.RejectedSwapOut);
            Move(chipB, homeA, context.Timings.RejectedSwapOut);
            await DelayAsync(context.Timings.RejectedSwapOut, ct);

            Move(chipA, homeA, context.Timings.RejectedSwapBack);
            Move(chipB, homeB, context.Timings.RejectedSwapBack);
            await DelayAsync(context.Timings.RejectedSwapBack, ct);

            chipA.SetRawPosition(homeA);
            chipB.SetRawPosition(homeB);
        }

        private static void Move(ChipView chip, Vector2 target, float duration)
        {
            chip.Rect.DOAnchorPos(target, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(chip.gameObject);
        }

        private static UniTask DelayAsync(float seconds, CancellationToken ct)
            => seconds <= 0f
                ? UniTask.CompletedTask
                : UniTask.Delay(
                    TimeSpan.FromSeconds(seconds),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);
    }
}
