namespace Match3.Core
{
    /// <summary>
    /// Fixed RNG consumption order (GDD D12, §13). Enum values match the order, which makes it
    /// testable rather than a convention. New consumers are appended, never inserted.
    /// </summary>
    public enum RandomConsumer : byte
    {
        /// <summary>Initial board fill, colour re-rolls and the repair pass (E20).</summary>
        BoardGeneration = 0,

        /// <summary>Colours of chips created by spawners during REFILL (GDD §3.3).</summary>
        SpawnerColor = 1,

        /// <summary>Starting colour and phase of cx boxes at build time (GDD §7.2).</summary>
        CyclingElementPhase = 2,

        /// <summary>Targeting fallback when no reachable unclosed goal exists (GDD §6.1 step 4).</summary>
        TargetingTieBreak = 3,

        /// <summary>Board shuffle when no legal move remains (GDD §5.4).</summary>
        Shuffle = 4,

        /// <summary>Cells picked for leftover-move rockets on win (GDD §8.4).</summary>
        MovesBonus = 5
    }
}
