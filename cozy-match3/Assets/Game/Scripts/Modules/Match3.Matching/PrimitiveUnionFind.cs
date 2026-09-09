using System;

namespace Match3.Matching
{
    /// <summary>
    /// Merges primitives that share at least one cell, transitively (GDD §4.2 step B, D02).
    /// The smaller index always wins so component roots stay deterministic.
    /// </summary>
    internal sealed class PrimitiveUnionFind
    {
        private int[] _parent = Array.Empty<int>();
        private int _count;

        public void Reset(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (_parent.Length < count)
            {
                _parent = new int[count];
            }

            _count = count;
            for (int i = 0; i < count; i++)
            {
                _parent[i] = i;
            }
        }

        public int Find(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int root = index;
            while (_parent[root] != root)
            {
                root = _parent[root];
            }

            int current = index;
            while (_parent[current] != root)
            {
                int next = _parent[current];
                _parent[current] = root;
                current = next;
            }

            return root;
        }

        public void Union(int a, int b)
        {
            int rootA = Find(a);
            int rootB = Find(b);
            if (rootA == rootB)
            {
                return;
            }

            if (rootA < rootB)
            {
                _parent[rootB] = rootA;
            }
            else
            {
                _parent[rootA] = rootB;
            }
        }
    }
}
