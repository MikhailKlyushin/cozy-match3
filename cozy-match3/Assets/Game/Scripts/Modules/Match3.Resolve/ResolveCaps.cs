namespace Match3.Resolve
{
    /// <summary>
    /// Hard termination caps, not balance levers: an unbounded chain on WebGL is a permanently
    /// frozen tab. Hitting <see cref="MaxWaves"/> or <see cref="MaxCascadeDepth"/> is always a
    /// bug and is reported through CapHit plus logger.Error (E05, §14).
    /// </summary>
    public static class ResolveCaps
    {
        /// <summary>GDD D08, §6.4.</summary>
        public const int MaxWaves = 20;

        /// <summary>GDD §5.1 stage 10.</summary>
        public const int MaxCascadeDepth = 30;

        /// <summary>Attempts per shuffle phase: Fisher-Yates first, then colour regeneration (§5.4).</summary>
        public const int MaxShuffleAttempts = 10;

        /// <summary>GDD §8.4.</summary>
        public const int MaxMovesBonus = 10;

        /// <summary>Rainbow plus airplane: first 8 cells in y-then-x order become airplanes (§6.3).</summary>
        public const int MaxAirplanesInCombo = 8;

        /// <summary>Board regenerations before switching to the repair pass (§13, E20).</summary>
        public const int MaxBoardGenerationAttempts = 50;

        /// <summary>GDD §3.4, §7.1.</summary>
        public const int MaxNestingDepth = 3;
    }
}
