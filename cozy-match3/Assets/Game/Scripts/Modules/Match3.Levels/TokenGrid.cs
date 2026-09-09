using System;
using Match3.Core;

namespace Match3.Levels
{
    /// <summary>
    /// Token grid after the single row flip (D01): <see cref="TokenAt"/> already works in board
    /// coordinates, origin bottom-left. Nobody flips rows again — a second flip in the view
    /// silently cancels this one.
    /// </summary>
    public sealed class TokenGrid
    {
        private readonly Grid<string> _tokens;

        internal TokenGrid(int width, int height)
        {
            _tokens = new Grid<string>(width, height);
        }

        public int Width => _tokens.Width;

        public int Height => _tokens.Height;

        public string TokenAt(int x, int y)
        {
            if (!_tokens.Contains(x, y))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Cell outside the grid: " + new GridPos(x, y));
            }

            return _tokens[x, y];
        }

        internal void Set(int x, int y, string token) => _tokens[x, y] = token;
    }
}
