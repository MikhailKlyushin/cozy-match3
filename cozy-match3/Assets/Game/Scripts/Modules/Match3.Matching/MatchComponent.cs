using System.Collections.Generic;
using Match3.Core;

namespace Match3.Matching
{
    /// <summary>
    /// Connected group of primitives of one colour (D02). A component yields at most one booster
    /// and clears ALL of its cells whatever the rank, so a chip in two figures is never split (E02).
    /// </summary>
    public sealed class MatchComponent
    {
        private readonly GridPos[] _cells;

        internal MatchComponent(
            int index,
            ChipColor color,
            GridPos[] cells,
            ComponentRank rank,
            BoosterType booster,
            GridPos spawnCell)
        {
            Index = index;
            Color = color;
            _cells = cells;
            Rank = rank;
            Booster = booster;
            SpawnCell = spawnCell;
        }

        public ChipColor Color { get; }

        /// <summary>Sorted y-up then x-up.</summary>
        public IReadOnlyList<GridPos> Cells => _cells;

        public ComponentRank Rank { get; }

        /// <summary>GDD §4.3; <see cref="GridPos.Invalid"/> when <see cref="Rank"/> is None.</summary>
        public GridPos SpawnCell { get; }

        public BoosterType Booster { get; }

        /// <summary>Detection order; used as the damage source id (D07).</summary>
        public int Index { get; }
    }
}
