using System;
using Match3.Board;
using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>
    /// GDD §6.2: every chip of one colour. The colour is the swapped chip's
    /// (<see cref="BoosterActivation.ColorHint"/>) or, on a tap, the most needed colour (§6.1).
    /// </summary>
    public sealed class RainbowEffect : IBoosterEffect
    {
        private readonly ITargetingService _targeting;

        public RainbowEffect(ITargetingService targeting)
        {
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
        }

        public BoosterType Type => BoosterType.Rainbow;

        public void Resolve(in BoosterActivation activation, IBoardReader board, CellBuffer hitCells)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (hitCells == null)
            {
                throw new ArgumentNullException(nameof(hitCells));
            }

            ChipColor color = activation.ColorHint != ChipColor.None
                ? activation.ColorHint
                : _targeting.PickNeededColor();

            int found = color == ChipColor.None ? 0 : AddChipsOfColor(color, board, hitCells);
            if (found == 0)
            {
                // E06/E07: no chip of that colour, so the ball still takes its own neighbourhood
                // instead of being wasted.
                BoosterCells.AddNeighbourhood(board, hitCells, activation.Cell);
            }
        }

        private static int AddChipsOfColor(ChipColor color, IBoardReader board, CellBuffer hitCells)
        {
            int found = 0;

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    ChipSlot slot = board.GetSlot(cell);
                    if (!slot.IsChip || slot.Color != color)
                    {
                        continue;
                    }

                    hitCells.AddUnique(cell);
                    found++;
                }
            }

            return found;
        }
    }
}
