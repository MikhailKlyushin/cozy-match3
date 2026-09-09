using System;

namespace Match3.Core
{
    /// <summary>
    /// Flat cell array indexed by <see cref="GridPos"/>. Indexers return <c>ref</c> so struct
    /// cells are mutated in place (§14). Iteration order is always explicit y-then-x, which is
    /// why Dictionary/HashSet are banned in gameplay traversal (I4).
    /// </summary>
    public sealed class Grid<T>
    {
        private readonly T[] _cells;

        public Grid(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            _cells = new T[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        public int Length => _cells.Length;

        /// <summary>Unchecked: callers guard with <see cref="Contains(GridPos)"/>.</summary>
        public ref T this[GridPos p] => ref _cells[p.Y * Width + p.X];

        public ref T this[int x, int y] => ref _cells[y * Width + x];

        public ref T AtIndex(int index) => ref _cells[index];

        public bool Contains(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public GridPos PositionOf(int index) => new GridPos(index % Width, index / Width);

        public void Clear() => Array.Clear(_cells, 0, _cells.Length);

        public void Fill(in T value)
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = value;
            }
        }

        public void CopyFrom(Grid<T> other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (other.Width != Width || other.Height != Height)
            {
                throw new ArgumentException("Grid dimensions differ.", nameof(other));
            }

            Array.Copy(other._cells, _cells, _cells.Length);
        }
    }
}
