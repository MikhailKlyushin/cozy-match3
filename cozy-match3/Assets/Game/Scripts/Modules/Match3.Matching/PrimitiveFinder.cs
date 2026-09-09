using System;
using Match3.Board;
using Match3.Core;

namespace Match3.Matching
{
    /// <summary>
    /// GDD §4.2 step A: maximal lines of 3 or more and 2x2 squares of one colour. Boosters are
    /// colourless and never take part in a primitive.
    /// </summary>
    internal sealed class PrimitiveFinder
    {
        private readonly IBoardReader _board;

        private const int MinLineLength = 3;

        public PrimitiveFinder(IBoardReader board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
        }

        /// <summary>Appends horizontal lines, then vertical lines, then squares, each in y-up x-up order.</summary>
        public void Find(PooledList<MatchPrimitive> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            FindHorizontalLines(result);
            FindVerticalLines(result);
            FindSquares(result);
        }

        public bool HasAny()
        {
            int width = _board.Width;
            int height = _board.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    ChipColor color = ColorAt(x, y);
                    if (color == ChipColor.None)
                    {
                        continue;
                    }

                    if (RunLength(x, y, 1, 0) >= MinLineLength || RunLength(x, y, 0, 1) >= MinLineLength)
                    {
                        return true;
                    }

                    if (IsSquareOrigin(x, y, color))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void FindHorizontalLines(PooledList<MatchPrimitive> result)
        {
            int width = _board.Width;
            int height = _board.Height;

            for (int y = 0; y < height; y++)
            {
                int x = 0;
                while (x < width)
                {
                    ChipColor color = ColorAt(x, y);
                    if (color == ChipColor.None)
                    {
                        x++;
                        continue;
                    }

                    int length = RunLength(x, y, 1, 0);
                    if (length >= MinLineLength)
                    {
                        result.Add(new MatchPrimitive(PrimitiveKind.Horizontal, new GridPos(x, y), length, color));
                    }

                    x += length;
                }
            }
        }

        private void FindVerticalLines(PooledList<MatchPrimitive> result)
        {
            int width = _board.Width;
            int height = _board.Height;

            for (int x = 0; x < width; x++)
            {
                int y = 0;
                while (y < height)
                {
                    ChipColor color = ColorAt(x, y);
                    if (color == ChipColor.None)
                    {
                        y++;
                        continue;
                    }

                    int length = RunLength(x, y, 0, 1);
                    if (length >= MinLineLength)
                    {
                        result.Add(new MatchPrimitive(PrimitiveKind.Vertical, new GridPos(x, y), length, color));
                    }

                    y += length;
                }
            }
        }

        private void FindSquares(PooledList<MatchPrimitive> result)
        {
            int width = _board.Width;
            int height = _board.Height;

            for (int y = 0; y + 1 < height; y++)
            {
                for (int x = 0; x + 1 < width; x++)
                {
                    ChipColor color = ColorAt(x, y);
                    if (color != ChipColor.None && IsSquareOrigin(x, y, color))
                    {
                        result.Add(new MatchPrimitive(PrimitiveKind.Square, new GridPos(x, y), 2, color));
                    }
                }
            }
        }

        private bool IsSquareOrigin(int x, int y, ChipColor color)
        {
            if (x + 1 >= _board.Width || y + 1 >= _board.Height)
            {
                return false;
            }

            return ColorAt(x + 1, y) == color
                   && ColorAt(x, y + 1) == color
                   && ColorAt(x + 1, y + 1) == color;
        }

        private int RunLength(int x, int y, int stepX, int stepY)
        {
            ChipColor color = ColorAt(x, y);
            if (color == ChipColor.None)
            {
                return 0;
            }

            int length = 1;
            int nextX = x + stepX;
            int nextY = y + stepY;
            while (nextX < _board.Width && nextY < _board.Height && ColorAt(nextX, nextY) == color)
            {
                length++;
                nextX += stepX;
                nextY += stepY;
            }

            return length;
        }

        private ChipColor ColorAt(int x, int y)
        {
            var p = new GridPos(x, y);
            if (_board.GetKind(p) != CellKind.Playable)
            {
                return ChipColor.None;
            }

            ChipSlot slot = _board.GetSlot(p);
            return slot.Kind == SlotKind.Chip ? slot.Color : ChipColor.None;
        }
    }
}
