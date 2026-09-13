using Match3.Board;
using Match3.Core;

namespace Match3.Matching
{
    /// <summary>
    /// Colour geometry shared by the initial board generation (E20) and the third shuffle phase
    /// (§5.4): both need an arrangement with no ready match.
    /// </summary>
    public static class MatchFreePlacement
    {
        private const int MinLineLength = 3;

        /// <summary>
        /// First colour that closes no primitive, or <paramref name="fallback"/> when the cell is
        /// fully constrained and the caller has to report the ready-made match (§3.4 rule 2).
        /// The RNG draw itself stays at the call site: §13 fixes which subsystem consumes the
        /// stream, and a shared helper drawing on their behalf would hide that.
        /// </summary>
        public static ChipColor FirstColorWithoutMatch(
            IBoardReader board,
            GridPos cell,
            int colorCount,
            ChipColor fallback)
        {
            if (!WouldCloseMatch(board, cell, fallback))
            {
                return fallback;
            }

            for (int index = 1; index <= colorCount; index++)
            {
                ChipColor candidate = ChipColors.FromIndex(index);
                if (!WouldCloseMatch(board, cell, candidate))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        public static bool WouldCloseMatch(IBoardReader board, GridPos cell, ChipColor color)
        {
            if (RunLength(board, cell, color, -1, 0) + 1 + RunLength(board, cell, color, 1, 0) >= MinLineLength)
            {
                return true;
            }

            if (RunLength(board, cell, color, 0, -1) + 1 + RunLength(board, cell, color, 0, 1) >= MinLineLength)
            {
                return true;
            }

            return ClosesSquare(board, cell, color);
        }

        private static int RunLength(IBoardReader board, GridPos cell, ChipColor color, int stepX, int stepY)
        {
            int length = 0;
            var next = new GridPos(cell.X + stepX, cell.Y + stepY);
            while (ColorAt(board, next) == color)
            {
                length++;
                next = new GridPos(next.X + stepX, next.Y + stepY);
            }

            return length;
        }

        private static bool ClosesSquare(IBoardReader board, GridPos cell, ChipColor color)
        {
            for (int offsetY = -1; offsetY <= 0; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 0; offsetX++)
                {
                    var origin = new GridPos(cell.X + offsetX, cell.Y + offsetY);
                    if (!board.Contains(origin) || !board.Contains(new GridPos(origin.X + 1, origin.Y + 1)))
                    {
                        continue;
                    }

                    if (IsSquareOfColor(board, origin, cell, color))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsSquareOfColor(IBoardReader board, GridPos origin, GridPos placed, ChipColor color)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int x = 0; x <= 1; x++)
                {
                    var corner = new GridPos(origin.X + x, origin.Y + y);
                    if (corner != placed && ColorAt(board, corner) != color)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static ChipColor ColorAt(IBoardReader board, GridPos cell)
        {
            if (!board.Contains(cell) || board.GetKind(cell) != CellKind.Playable)
            {
                return ChipColor.None;
            }

            ChipSlot slot = board.GetSlot(cell);
            return slot.Kind == SlotKind.Chip ? slot.Color : ChipColor.None;
        }
    }
}
