using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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

        private readonly List<ChipView> _activeChips = new List<ChipView>(128);

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

            if (!TryFindChipAt(context.Board, e.A, out ChipView chipA)
                || !TryFindChipAt(context.Board, e.B, out ChipView chipB))
            {
                _logger.Warn("Swap between " + e.A.ToString() + " and " + e.B.ToString()
                             + " has no chip view on one of the cells");
                return;
            }

            Vector2 homeA = context.Board.Layout.CellCenter(e.A);
            Vector2 homeB = context.Board.Layout.CellCenter(e.B);

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

        /// <summary>
        /// The swap events carry cells, not instance ids, and the view indexes chips by id, so the
        /// two participants are located among the active chip views. Once per turn, never per cell.
        /// </summary>
        private bool TryFindChipAt(BoardView board, GridPos cell, out ChipView chip)
        {
            _activeChips.Clear();
            board.GetComponentsInChildren<ChipView>(false, _activeChips);

            for (int i = 0; i < _activeChips.Count; i++)
            {
                if (_activeChips[i].Cell == cell)
                {
                    chip = _activeChips[i];
                    return true;
                }
            }

            chip = null;
            return false;
        }
    }
}
