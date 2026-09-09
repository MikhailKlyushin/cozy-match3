namespace Match3.Matching
{
    /// <summary>Numeric values are the GDD §4.2 ranks: 1 is the highest.</summary>
    public enum ComponentRank : byte
    {
        None = 0,

        /// <summary>Line of 5 or more.</summary>
        Rainbow = 1,

        /// <summary>Two crossing lines, each 3 or more, 5 cells or more together.</summary>
        Bomb = 2,

        /// <summary>2x2 square.</summary>
        Airplane = 3,

        /// <summary>Line of exactly 4.</summary>
        Rocket = 4
    }
}
