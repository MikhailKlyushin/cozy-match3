using System;
using Match3.Board;
using Match3.Core;

namespace Match3.Matching
{
    /// <summary>
    /// DETECT and CLASSIFY (§5.1 steps 1-2): primitives, union into components by shared cells
    /// (D02), rank and spawn cell (§4.2, §4.3).
    /// </summary>
    public sealed class MatchDetectionService
    {
        private readonly PrimitiveFinder _finder;
        private readonly ComponentClassifier _classifier = new ComponentClassifier();
        private readonly SpawnCellResolver _spawnCells = new SpawnCellResolver();
        private readonly PrimitiveUnionFind _union = new PrimitiveUnionFind();
        private readonly PooledList<MatchPrimitive> _primitives = new PooledList<MatchPrimitive>(32);
        private readonly PooledList<int> _members = new PooledList<int>(8);
        private readonly PooledList<GridPos> _componentCells = new PooledList<GridPos>(16);
        private readonly int[] _cellPrimitive;
        private readonly int[] _cellComponent;
        private readonly bool[] _claimedSpawnCells;
        private readonly int _width;
        private readonly int _height;

        private int[] _primitiveComponent = Array.Empty<int>();

        public MatchDetectionService(IBoardReader board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            _finder = new PrimitiveFinder(board);
            _width = board.Width;
            _height = board.Height;

            int cellCount = _width * _height;
            _cellPrimitive = new int[cellCount];
            _cellComponent = new int[cellCount];
            _claimedSpawnCells = new bool[cellCount];
        }

        /// <summary>
        /// <paramref name="result"/> is cleared inside. <paramref name="playerSwapTarget"/> is the
        /// cell the player swapped into (§4.3 rule 1) and <see cref="GridPos.Invalid"/> in a
        /// cascade (E03).
        /// </summary>
        public void Detect(PooledList<MatchComponent> result, GridPos playerSwapTarget)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            _primitives.Clear();
            _finder.Find(_primitives);
            if (_primitives.Count == 0)
            {
                return;
            }

            ResetScratch();
            MergePrimitives();

            int componentCount = AssignComponents();
            for (int component = 0; component < componentCount; component++)
            {
                result.Add(BuildComponent(component, playerSwapTarget));
            }
        }

        /// <summary>Cheap "is there any match at all" probe for VALIDATE and board generation.</summary>
        public bool HasAnyMatch() => _finder.HasAny();

        private void ResetScratch()
        {
            int cellCount = _width * _height;
            for (int i = 0; i < cellCount; i++)
            {
                _cellPrimitive[i] = -1;
                _cellComponent[i] = -1;
                _claimedSpawnCells[i] = false;
            }

            if (_primitiveComponent.Length < _primitives.Count)
            {
                _primitiveComponent = new int[_primitives.Count];
            }

            for (int i = 0; i < _primitives.Count; i++)
            {
                _primitiveComponent[i] = -1;
            }
        }

        /// <summary>GDD §4.2 step B: primitives sharing a cell end up in one component.</summary>
        private void MergePrimitives()
        {
            _union.Reset(_primitives.Count);

            for (int i = 0; i < _primitives.Count; i++)
            {
                MatchPrimitive primitive = _primitives[i];
                int cellCount = primitive.CellCount;
                for (int c = 0; c < cellCount; c++)
                {
                    GridPos cell = primitive.CellAt(c);
                    int index = cell.Y * _width + cell.X;
                    if (_cellPrimitive[index] < 0)
                    {
                        _cellPrimitive[index] = i;
                    }
                    else
                    {
                        _union.Union(i, _cellPrimitive[index]);
                    }
                }
            }
        }

        /// <summary>Numbers components in board traversal order, y-up then x-up.</summary>
        private int AssignComponents()
        {
            int componentCount = 0;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = y * _width + x;
                    int primitive = _cellPrimitive[index];
                    if (primitive < 0)
                    {
                        continue;
                    }

                    int root = _union.Find(primitive);
                    int component = _primitiveComponent[root];
                    if (component < 0)
                    {
                        component = componentCount++;
                        _primitiveComponent[root] = component;
                    }

                    _cellComponent[index] = component;
                }
            }

            return componentCount;
        }

        private MatchComponent BuildComponent(int component, GridPos playerSwapTarget)
        {
            _componentCells.Clear();
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (_cellComponent[y * _width + x] == component)
                    {
                        _componentCells.Add(new GridPos(x, y));
                    }
                }
            }

            _members.Clear();
            ChipColor color = ChipColor.None;
            for (int i = 0; i < _primitives.Count; i++)
            {
                if (_primitiveComponent[_union.Find(i)] != component)
                {
                    continue;
                }

                _members.Add(i);
                color = _primitives[i].Color;
            }

            var cells = new GridPos[_componentCells.Count];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = _componentCells[i];
            }

            ComponentRank rank = _classifier.Classify(_primitives, _members, out BoosterType booster);
            GridPos spawnCell = GridPos.Invalid;
            if (rank != ComponentRank.None)
            {
                spawnCell = _spawnCells.Resolve(rank, cells, _primitives, _members, playerSwapTarget);
                if (spawnCell.IsValid)
                {
                    int index = spawnCell.Y * _width + spawnCell.X;
                    if (_claimedSpawnCells[index])
                    {
                        // Two components claiming one cell: the later one falls back to rule 4 (§4.3).
                        spawnCell = _spawnCells.Median(cells);
                        index = spawnCell.Y * _width + spawnCell.X;
                    }

                    _claimedSpawnCells[index] = true;
                }
            }

            return new MatchComponent(component, color, cells, rank, booster, spawnCell);
        }
    }
}
