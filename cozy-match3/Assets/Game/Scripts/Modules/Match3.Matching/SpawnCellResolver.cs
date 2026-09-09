using Match3.Core;

namespace Match3.Matching
{
    /// <summary>GDD §4.3: first applicable rule wins; rule 1 is skipped in a cascade (E03).</summary>
    internal sealed class SpawnCellResolver
    {
        public GridPos Resolve(
            ComponentRank rank,
            GridPos[] cells,
            PooledList<MatchPrimitive> primitives,
            PooledList<int> members,
            GridPos playerSwapTarget)
        {
            if (playerSwapTarget.IsValid && Contains(cells, playerSwapTarget))
            {
                return playerSwapTarget;
            }

            if (rank == ComponentRank.Bomb && LineCrossing.TryFind(primitives, members, out GridPos crossing))
            {
                return crossing;
            }

            if (rank == ComponentRank.Airplane && TryFindSquareCorner(primitives, members, out GridPos corner))
            {
                return corner;
            }

            return Median(cells);
        }

        /// <summary>Rule 4: cells sorted y-up then x-up, index floor((n - 1) / 2).</summary>
        public GridPos Median(GridPos[] cells)
        {
            if (cells == null || cells.Length == 0)
            {
                return GridPos.Invalid;
            }

            return cells[(cells.Length - 1) / 2];
        }

        private static bool Contains(GridPos[] cells, GridPos cell)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Rule 3: bottom-left cell of the square, lowest y then lowest x on a tie.</summary>
        private static bool TryFindSquareCorner(
            PooledList<MatchPrimitive> primitives,
            PooledList<int> members,
            out GridPos corner)
        {
            corner = GridPos.Invalid;
            bool found = false;

            for (int i = 0; i < members.Count; i++)
            {
                MatchPrimitive primitive = primitives[members[i]];
                if (primitive.Kind != PrimitiveKind.Square)
                {
                    continue;
                }

                if (!found || GridPos.CompareYThenX(primitive.Origin, corner) < 0)
                {
                    corner = primitive.Origin;
                    found = true;
                }
            }

            return found;
        }
    }
}
