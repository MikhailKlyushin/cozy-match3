using System.Collections.Generic;
using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// Ordered record of everything one turn did. Pooled and reused via <see cref="Reset"/>, so a
    /// warm turn allocates nothing (rule T7). Written only by <see cref="TranscriptWriter"/>.
    /// </summary>
    public sealed class TurnTranscript
    {
        private readonly PooledList<TurnEvent> _events;
        private readonly PooledList<GridPos> _cells;

        public TurnTranscript(int eventCapacity = 256, int cellCapacity = 256)
        {
            _events = new PooledList<TurnEvent>(eventCapacity);
            _cells = new PooledList<GridPos>(cellCapacity);
        }

        public IReadOnlyList<TurnEvent> Events => _events;

        /// <summary>Shared buffer the per-event slices point into.</summary>
        public IReadOnlyList<GridPos> Cells => _cells;

        public TurnOutcome Outcome { get; private set; }

        public int MovesLeft { get; private set; }

        /// <summary>Deepest cascade step reached, for the cascade_depth metric (§14).</summary>
        public int MaxDepth { get; private set; }

        public int Seed { get; private set; }

        public int EventCount => _events.Count;

        public int CellCount => _cells.Count;

        /// <summary>Capacity probes used by the no-allocation test (T01).</summary>
        public int EventCapacity => _events.Capacity;

        public int CellCapacity => _cells.Capacity;

        /// <summary>Allocation-free indexed read for the transcript player and tests.</summary>
        public ref readonly TurnEvent GetEvent(int index) => ref _events[index];

        public GridPos GetCell(int index) => _cells[index];

        /// <summary>Clears content while keeping capacity.</summary>
        public void Reset()
        {
            _events.Clear();
            _cells.Clear();
            Outcome = TurnOutcome.Rejected;
            MovesLeft = 0;
            MaxDepth = 0;
            Seed = 0;
        }

        internal void SetHeader(int seed, int movesLeft)
        {
            Seed = seed;
            MovesLeft = movesLeft;
        }

        internal void SetOutcome(TurnOutcome outcome) => Outcome = outcome;

        internal void SetMovesLeft(int movesLeft) => MovesLeft = movesLeft;

        internal void SetMaxDepth(int depth)
        {
            if (depth > MaxDepth)
            {
                MaxDepth = depth;
            }
        }

        internal void AddEvent(in TurnEvent e) => _events.Add(e);

        internal int AppendCell(GridPos p)
        {
            int offset = _cells.Count;
            _cells.Add(p);
            return offset;
        }

        internal int AppendCells(CellBuffer cells)
        {
            int offset = _cells.Count;
            for (int i = 0; i < cells.Count; i++)
            {
                _cells.Add(cells[i]);
            }

            return offset;
        }

        internal int AppendCells(IReadOnlyList<GridPos> cells)
        {
            int offset = _cells.Count;
            for (int i = 0; i < cells.Count; i++)
            {
                _cells.Add(cells[i]);
            }

            return offset;
        }
    }
}
