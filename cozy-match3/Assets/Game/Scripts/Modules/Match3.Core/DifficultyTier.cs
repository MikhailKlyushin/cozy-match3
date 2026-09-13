namespace Match3.Core
{
    /// <summary>Feeds the move-limit multiplier (GDD §9.3) and analytics (§14); no runtime logic.</summary>
    public enum DifficultyTier : byte
    {
        /// <summary>multiplier 1.30</summary>
        Easy = 0,

        /// <summary>multiplier 1.10</summary>
        Medium = 1,

        /// <summary>multiplier 0.95</summary>
        Hard = 2,

        /// <summary>multiplier 0.85, unused by the 12 shipped levels</summary>
        SuperHard = 3
    }
}
