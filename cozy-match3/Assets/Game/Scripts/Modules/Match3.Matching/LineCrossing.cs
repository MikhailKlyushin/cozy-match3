using Match3.Core;

namespace Match3.Matching
{
    /// <summary>
    /// Rank 2 predicate shared by classification (GDD §4.2) and the spawn cell (§4.3 rule 2):
    /// two crossing lines, each 3 or more cells, 5 or more cells together.
    /// </summary>
    internal static class LineCrossing
    {
        private const int MinLineLength = 3;
        private const int MinBombCells = 5;

        /// <summary>Returns the crossing with the lowest y, then the lowest x.</summary>
        public static bool TryFind(
            PooledList<MatchPrimitive> primitives,
            PooledList<int> members,
            out GridPos crossing)
        {
            crossing = GridPos.Invalid;
            bool found = false;

            for (int i = 0; i < members.Count; i++)
            {
                MatchPrimitive first = primitives[members[i]];
                if (first.Kind != PrimitiveKind.Horizontal || first.Length < MinLineLength)
                {
                    continue;
                }

                for (int j = 0; j < members.Count; j++)
                {
                    MatchPrimitive second = primitives[members[j]];
                    if (second.Kind != PrimitiveKind.Vertical || second.Length < MinLineLength)
                    {
                        continue;
                    }

                    if (first.Length + second.Length - 1 < MinBombCells)
                    {
                        continue;
                    }

                    if (!first.TryGetCrossing(second, out GridPos candidate))
                    {
                        continue;
                    }

                    if (!found || GridPos.CompareYThenX(candidate, crossing) < 0)
                    {
                        crossing = candidate;
                        found = true;
                    }
                }
            }

            return found;
        }
    }
}
