using System;
using System.Collections.Generic;
using Match3.Core;

namespace Match3.Board
{
    /// <summary>
    /// Board state and the single source of truth for a level attempt. Reading is public through
    /// <see cref="IBoardReader"/>; mutation is internal so it can only happen inside the resolve
    /// pipeline, the level builder or the swap probe (A10).
    /// </summary>
    public sealed class Board : IBoardReader
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>Mirrors the §3.4 nesting limit; Board cannot reference ResolveCaps.</summary>
        private const int MaxNestingProbeDepth = 3;

        private readonly Grid<Cell> _cells;
        private readonly PooledList<ElementInstance> _elements = new PooledList<ElementInstance>(32);
        private readonly bool[] _spawnerColumns;

        private int _nextInstanceId = 1;

        public Board(int width, int height, ElementCatalog catalog)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _cells = new Grid<Cell>(width, height);
            _spawnerColumns = new bool[width];

            for (int i = 0; i < _cells.Length; i++)
            {
                ref Cell cell = ref _cells.AtIndex(i);
                cell.Kind = CellKind.Playable;
                cell.ElementIndex = Cell.NoElement;
            }
        }

        public int Width => _cells.Width;

        public int Height => _cells.Height;

        public ElementCatalog Catalog { get; }

        public int ElementCount => _elements.Count;

        public bool Contains(GridPos p) => _cells.Contains(p);

        public CellKind GetKind(GridPos p) => _cells[p].Kind;

        public ChipSlot GetSlot(GridPos p) => _cells[p].Slot;

        public bool TryGetElement(GridPos p, out ElementInstance element)
        {
            int index = _cells[p].ElementIndex;
            if (index == Cell.NoElement)
            {
                element = null;
                return false;
            }

            element = _elements[index];
            return true;
        }

        public ElementDefinition GetDefinition(GridPos p)
            => TryGetElement(p, out ElementInstance element) ? Catalog.Get(element.Definition) : null;

        public bool IsPassableForFall(GridPos p)
        {
            if (!_cells.Contains(p))
            {
                return false;
            }

            ref readonly Cell cell = ref _cells[p];
            if (cell.Kind != CellKind.Playable)
            {
                return false;
            }

            if (cell.ElementIndex == Cell.NoElement)
            {
                return true;
            }

            ElementInstance element = _elements[cell.ElementIndex];
            return !element.IsAlive || !Catalog.Get(element.Definition).BlocksFall;
        }

        public bool IsMovable(GridPos p)
        {
            if (!_cells.Contains(p))
            {
                return false;
            }

            ref readonly Cell cell = ref _cells[p];
            if (cell.Kind != CellKind.Playable || cell.Slot.Kind == SlotKind.Empty)
            {
                return false;
            }

            if (cell.ElementIndex == Cell.NoElement)
            {
                return true;
            }

            ElementInstance element = _elements[cell.ElementIndex];
            return !element.IsAlive || !Catalog.Get(element.Definition).OccupiesCell;
        }

        public bool IsSpawnerColumn(int x) => x >= 0 && x < _spawnerColumns.Length && _spawnerColumns[x];

        /// <summary>Fills <paramref name="buffer"/> (length 4) with in-bounds orthogonal neighbours.</summary>
        public int GetOrthogonalNeighbours(GridPos p, GridPos[] buffer)
        {
            int count = 0;
            AppendIfInside(p.Down, buffer, ref count);
            AppendIfInside(p.Left, buffer, ref count);
            AppendIfInside(p.Right, buffer, ref count);
            AppendIfInside(p.Up, buffer, ref count);
            return count;
        }

        /// <summary>Fills <paramref name="buffer"/> (length 4) with in-bounds diagonal neighbours.</summary>
        public int GetDiagonalNeighbours(GridPos p, GridPos[] buffer)
        {
            int count = 0;
            AppendIfInside(new GridPos(p.X - 1, p.Y - 1), buffer, ref count);
            AppendIfInside(new GridPos(p.X + 1, p.Y - 1), buffer, ref count);
            AppendIfInside(p.UpLeft, buffer, ref count);
            AppendIfInside(p.UpRight, buffer, ref count);
            return count;
        }

        /// <summary>
        /// Hashes rule-relevant state only. InstanceId and the transient Consumed flag are
        /// excluded: two boards with the same colours in the same cells are the same game state.
        /// </summary>
        public ulong ComputeHash()
        {
            ulong hash = FnvOffsetBasis;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    ref readonly Cell cell = ref _cells[x, y];
                    hash = Mix(hash, (byte)cell.Kind);
                    hash = Mix(hash, (byte)cell.Slot.Kind);
                    hash = Mix(hash, (byte)cell.Slot.Color);
                    hash = Mix(hash, (byte)cell.Slot.Booster);

                    int index = cell.ElementIndex;
                    for (int depth = 0; depth <= MaxNestingProbeDepth && index != Cell.NoElement; depth++)
                    {
                        ElementInstance element = _elements[index];
                        hash = Mix(hash, element.Definition.Value);
                        hash = Mix(hash, unchecked((uint)element.Health));
                        hash = Mix(hash, (byte)element.CurrentColor);
                        index = element.NestedIndex;
                    }

                    hash = Mix(hash, 0xFFu);
                }
            }

            return hash;
        }

        internal ref Cell CellAt(GridPos p) => ref _cells[p];

        internal ElementInstance ElementAt(int index) => _elements[index];

        internal int AllocateInstanceId() => _nextInstanceId++;

        internal void SetKind(GridPos p, CellKind kind) => _cells[p].Kind = kind;

        internal int SetChip(GridPos p, ChipColor color)
        {
            int instanceId = AllocateInstanceId();
            SetChip(p, color, instanceId);
            return instanceId;
        }

        internal void SetChip(GridPos p, ChipColor color, int instanceId)
        {
            ref Cell cell = ref _cells[p];
            cell.Slot.Kind = SlotKind.Chip;
            cell.Slot.Color = color;
            cell.Slot.Booster = BoosterType.None;
            cell.Slot.InstanceId = instanceId;
            cell.Slot.Consumed = false;
        }

        internal int SetBooster(GridPos p, BoosterType booster)
        {
            int instanceId = AllocateInstanceId();
            SetBooster(p, booster, instanceId);
            return instanceId;
        }

        internal void SetBooster(GridPos p, BoosterType booster, int instanceId)
        {
            ref Cell cell = ref _cells[p];
            cell.Slot.Kind = SlotKind.Booster;
            cell.Slot.Color = ChipColor.None;
            cell.Slot.Booster = booster;
            cell.Slot.InstanceId = instanceId;
            cell.Slot.Consumed = false;
        }

        /// <summary>Keeps the InstanceId so the view morphs the existing chip (§6.3 mass transforms).</summary>
        internal void TransformToBooster(GridPos p, BoosterType booster)
        {
            ref Cell cell = ref _cells[p];
            cell.Slot.Kind = SlotKind.Booster;
            cell.Slot.Color = ChipColor.None;
            cell.Slot.Booster = booster;
            cell.Slot.Consumed = false;
        }

        internal void ClearSlot(GridPos p)
        {
            ref Cell cell = ref _cells[p];
            cell.Slot = default;
        }

        /// <summary>Moves a slot preserving its InstanceId, which is how the view keeps identity.</summary>
        internal void MoveSlot(GridPos from, GridPos to)
        {
            ref Cell source = ref _cells[from];
            ref Cell target = ref _cells[to];
            target.Slot = source.Slot;
            source.Slot = default;
        }

        internal void SwapSlots(GridPos a, GridPos b)
        {
            ref Cell first = ref _cells[a];
            ref Cell second = ref _cells[b];
            ChipSlot tmp = first.Slot;
            first.Slot = second.Slot;
            second.Slot = tmp;
        }

        internal void SetConsumed(GridPos p, bool consumed) => _cells[p].Slot.Consumed = consumed;

        internal void ResetConsumedFlags()
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells.AtIndex(i).Slot.Consumed = false;
            }
        }

        internal int AddElement(ElementId definition, ChipColor color, int nestedIndex)
        {
            ElementDefinition def = Catalog.Get(definition);
            var instance = new ElementInstance(definition, def.MaxHealth, color, nestedIndex);
            _elements.Add(instance);
            return _elements.Count - 1;
        }

        internal void AttachElement(GridPos p, int elementIndex) => _cells[p].ElementIndex = elementIndex;

        internal void DetachElement(GridPos p) => _cells[p].ElementIndex = Cell.NoElement;

        /// <summary>GDD §3.3 default: any column whose top cell is playable and not permanently blocked.</summary>
        internal void RecomputeDefaultSpawners()
        {
            int topRow = Height - 1;
            for (int x = 0; x < Width; x++)
            {
                var top = new GridPos(x, topRow);
                bool playable = _cells[top].Kind == CellKind.Playable;
                bool permanentlyBlocked = TryGetElement(top, out ElementInstance element)
                                          && element.IsAlive
                                          && Catalog.Get(element.Definition).IsIndestructible;
                _spawnerColumns[x] = playable && !permanentlyBlocked;
            }
        }

        internal void SetSpawnerColumns(IReadOnlyList<int> columns)
        {
            Array.Clear(_spawnerColumns, 0, _spawnerColumns.Length);
            for (int i = 0; i < columns.Count; i++)
            {
                int x = columns[i];
                if (x < 0 || x >= _spawnerColumns.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(columns), "Spawner column out of range: " + x);
                }

                _spawnerColumns[x] = true;
            }
        }

        private static ulong Mix(ulong hash, ulong value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }

        private void AppendIfInside(GridPos p, GridPos[] buffer, ref int count)
        {
            if (_cells.Contains(p))
            {
                buffer[count++] = p;
            }
        }
    }
}
