using System;

namespace Match3.Resolve
{
    /// <summary>How a chip reached its new cell; the player picks the animation from it (§11.3).</summary>
    [Flags]
    public enum ChipMoveFlags : byte
    {
        None = 0,

        /// <summary>Vertical fall (GDD §5.3 rule 1).</summary>
        Fall = 1,

        /// <summary>Diagonal slide off a shelf (GDD §5.3 rule 2, D10).</summary>
        Slide = 2,

        Swap = 4,

        Shuffle = 8
    }

    /// <summary>
    /// Why a booster fired. All sources count identically towards ActivateBooster goals
    /// (§6.1, §6.4); the distinction serves the booster_fired metric (§14) and FX.
    /// </summary>
    public enum BoosterActivationSource : byte
    {
        /// <summary>Tap or swap by the player (D03).</summary>
        Player = 0,

        /// <summary>Caught in another booster's blast (E04, §6.4).</summary>
        Chain = 1,

        /// <summary>Participant or sub-activation of a combination plan (§6.3).</summary>
        Combo = 2,

        Cheat = 3,

        /// <summary>Leftover-move rocket (§8.4).</summary>
        Bonus = 4
    }

    public enum LevelEndReason : byte
    {
        /// <summary>Win beats lose when both conditions hold (D14).</summary>
        GoalsClosed = 0,

        MovesExhausted = 1,

        /// <summary>Shuffle could not restore a legal move (E19, §5.4). Always a bug.</summary>
        Deadlock = 2,

        Cheat = 3
    }

    /// <summary>Any value here is a bug, not balance (E05).</summary>
    [Flags]
    public enum CapKind : byte
    {
        None = 0,

        /// <summary>Chain exceeded <see cref="ResolveCaps.MaxWaves"/> (D08).</summary>
        WaveCap = 1,

        /// <summary>Cascade exceeded <see cref="ResolveCaps.MaxCascadeDepth"/>.</summary>
        DepthCap = 2
    }
}
