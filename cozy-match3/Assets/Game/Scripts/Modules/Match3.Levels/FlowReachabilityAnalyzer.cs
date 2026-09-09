using System;
using System.Collections.Generic;
using Match3.Board;

namespace Match3.Levels
{
    /// <summary>
    /// Flow graph of GDD §5.3 used by the §3.4 dead-pocket check: a cell is fed either from
    /// straight above or, when the cell above is impassable, by a diagonal slide from above-left
    /// or above-right. Only permanent obstructions block — holes and the indestructible element;
    /// destructible obstacles are passable (§3.4 rule 1).
    /// </summary>
    public sealed class FlowReachabilityAnalyzer
    {
        private readonly ElementCatalog _catalog;

        private bool[] _blocked = Array.Empty<bool>();
        private bool[] _reachable = Array.Empty<bool>();
        private TokenGrid _grid;
        private IReadOnlyList<int> _spawners;
        private int _width;
        private int _height;

        public FlowReachabilityAnalyzer(ElementCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// <paramref name="spawners"/> empty or null means the GDD §3.3 default. Results are
        /// cached per grid and spawner list, so validating a whole board stays linear.
        /// </summary>
        public bool IsReachableFromSpawner(TokenGrid grid, IReadOnlyList<int> spawners, int x, int y)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            Analyze(grid, spawners);

            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return false;
            }

            return _reachable[y * _width + x];
        }

        private void Analyze(TokenGrid grid, IReadOnlyList<int> spawners)
        {
            if (ReferenceEquals(_grid, grid) && ReferenceEquals(_spawners, spawners))
            {
                return;
            }

            _grid = grid;
            _spawners = spawners;
            _width = grid.Width;
            _height = grid.Height;

            int count = _width * _height;
            if (_blocked.Length < count)
            {
                _blocked = new bool[count];
                _reachable = new bool[count];
            }

            for (int i = 0; i < count; i++)
            {
                _reachable[i] = false;
            }

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _blocked[y * _width + x] = LevelTokens.IsPermanentObstruction(grid.TokenAt(x, y), _catalog);
                }
            }

            int topRow = _height - 1;
            for (int x = 0; x < _width; x++)
            {
                _reachable[topRow * _width + x] = !_blocked[topRow * _width + x] && IsSpawnerColumn(x);
            }

            for (int y = topRow - 1; y >= 0; y--)
            {
                for (int x = 0; x < _width; x++)
                {
                    _reachable[y * _width + x] = FlowsInto(x, y);
                }
            }
        }

        private bool FlowsInto(int x, int y)
        {
            if (_blocked[y * _width + x])
            {
                return false;
            }

            int above = (y + 1) * _width + x;
            if (_reachable[above])
            {
                return true;
            }

            // §5.3 rule 2: the diagonal slide is only checked when the cell straight above is
            // impassable. A passable but unreachable cell above means nothing ever arrives.
            if (!_blocked[above])
            {
                return false;
            }

            bool fromLeft = x > 0 && _reachable[above - 1];
            bool fromRight = x + 1 < _width && _reachable[above + 1];
            return fromLeft || fromRight;
        }

        private bool IsSpawnerColumn(int x)
        {
            if (_spawners != null && _spawners.Count > 0)
            {
                for (int i = 0; i < _spawners.Count; i++)
                {
                    if (_spawners[i] == x)
                    {
                        return true;
                    }
                }

                return false;
            }

            // GDD §3.3 default: every column whose top cell exists and is not permanently blocked.
            return true;
        }
    }
}
