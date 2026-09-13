using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Cell set with O(1) de-duplication via a board bitmask and a deterministic y-then-x sort.
    /// Replaces <c>HashSet&lt;GridPos&gt;</c>, whose traversal order is undefined and therefore
    /// banned in gameplay (I4).
    /// </summary>
    public sealed class CellBuffer
    {
        private static readonly YThenXComparer Comparer = new YThenXComparer();

        private readonly PooledList<GridPos> _cells = new PooledList<GridPos>(64);

        private ulong[] _mask = Array.Empty<ulong>();
        private int _width;
        private int _height;

        public int Count => _cells.Count;

        public GridPos this[int index] => _cells[index];

        public IReadOnlyList<GridPos> Cells => _cells;

        /// <summary>Required before <see cref="AddUnique"/> or <see cref="Contains"/>.</summary>
        public void Configure(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Board dimensions must be positive.");
            }

            _width = width;
            _height = height;

            int words = (width * height + 63) / 64;
            if (_mask.Length < words)
            {
                _mask = new ulong[words];
            }

            Clear();
        }

        public void Add(GridPos p)
        {
            _cells.Add(p);
            SetMaskBit(p);
        }

        /// <summary>Returns true when the cell was not present yet.</summary>
        public bool AddUnique(GridPos p)
        {
            if (Contains(p))
            {
                return false;
            }

            Add(p);
            return true;
        }

        public bool Contains(GridPos p)
        {
            if (_width == 0)
            {
                throw new InvalidOperationException("CellBuffer.Configure was not called.");
            }

            if (p.X < 0 || p.X >= _width || p.Y < 0 || p.Y >= _height)
            {
                return false;
            }

            int bit = p.Y * _width + p.X;
            return (_mask[bit >> 6] & (1UL << (bit & 63))) != 0UL;
        }

        public void Clear()
        {
            _cells.Clear();
            if (_mask.Length > 0)
            {
                Array.Clear(_mask, 0, _mask.Length);
            }
        }

        /// <summary>Canonical GDD tie-break order, e.g. the 8-airplane cap picks the first 8 here (§6.3).</summary>
        public void SortByYThenX()
        {
            if (_cells.Count > 1)
            {
                Array.Sort(_cells.Buffer, 0, _cells.Count, Comparer);
            }
        }

        public void AddUniqueRange(CellBuffer other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            for (int i = 0; i < other.Count; i++)
            {
                AddUnique(other[i]);
            }
        }

        private void SetMaskBit(GridPos p)
        {
            if (_width == 0)
            {
                return;
            }

            if (p.X < 0 || p.X >= _width || p.Y < 0 || p.Y >= _height)
            {
                return;
            }

            int bit = p.Y * _width + p.X;
            _mask[bit >> 6] |= 1UL << (bit & 63);
        }

        private sealed class YThenXComparer : IComparer<GridPos>
        {
            public int Compare(GridPos a, GridPos b) => GridPos.CompareYThenX(a, b);
        }
    }
}
