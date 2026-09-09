namespace Match3.Core
{
    /// <summary>
    /// Per-level spawn weight of one colour (GDD §4.1, §10.1). Lives in Core because both
    /// Match3.Levels (which parses it) and Match3.Resolve (which spawns by it) need it, and
    /// those two assemblies do not reference each other (§3.1).
    /// </summary>
    public readonly struct ColorWeight
    {
        public readonly ChipColor Color;

        /// <summary>Relative weight; must be non-negative, and weights need not sum to anything.</summary>
        public readonly int Weight;

        public ColorWeight(ChipColor color, int weight)
        {
            Color = color;
            Weight = weight;
        }
    }
}
