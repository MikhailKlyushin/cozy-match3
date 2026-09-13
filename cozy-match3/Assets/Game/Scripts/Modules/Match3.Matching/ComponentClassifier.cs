using Match3.Core;

namespace Match3.Matching
{
    /// <summary>GDD §4.2 step C: a component yields one booster, by the highest rank present.</summary>
    internal sealed class ComponentClassifier
    {
        private const int RainbowLineLength = 5;
        private const int RocketLineLength = 4;

        public ComponentRank Classify(
            PooledList<MatchPrimitive> primitives,
            PooledList<int> members,
            out BoosterType booster)
        {
            for (int i = 0; i < members.Count; i++)
            {
                MatchPrimitive primitive = primitives[members[i]];
                if (primitive.IsLine && primitive.Length >= RainbowLineLength)
                {
                    booster = BoosterType.Rainbow;
                    return ComponentRank.Rainbow;
                }
            }

            if (LineCrossing.TryFind(primitives, members, out _))
            {
                booster = BoosterType.Bomb;
                return ComponentRank.Bomb;
            }

            for (int i = 0; i < members.Count; i++)
            {
                if (primitives[members[i]].Kind == PrimitiveKind.Square)
                {
                    booster = BoosterType.Airplane;
                    return ComponentRank.Airplane;
                }
            }

            for (int i = 0; i < members.Count; i++)
            {
                MatchPrimitive primitive = primitives[members[i]];
                if (!primitive.IsLine || primitive.Length != RocketLineLength)
                {
                    continue;
                }

                // Orientation comes from the line, not from the swipe (GDD §4.3); horizontal
                // lines are collected first, so they win a tie inside one component.
                booster = primitive.Kind == PrimitiveKind.Horizontal ? BoosterType.RocketH : BoosterType.RocketV;
                return ComponentRank.Rocket;
            }

            booster = BoosterType.None;
            return ComponentRank.None;
        }
    }
}
