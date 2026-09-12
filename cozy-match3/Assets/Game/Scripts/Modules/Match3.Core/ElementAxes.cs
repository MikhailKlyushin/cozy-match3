namespace Match3.Core
{
    /// <summary>
    /// Axis damageSource (GDD §7.1). Resolved through the IDamageSourceRule registry; a switch
    /// over obstacle tokens inside DAMAGE/CLEAR/GRAVITY is a review blocker (§10).
    /// </summary>
    public enum DamageSourceKind : byte
    {
        /// <summary>Match in an orthogonally adjacent cell, or booster area damage. Token bx.</summary>
        AdjacentMatch = 0,

        /// <summary>Own-colour match only; booster area damage still destroys it (Q4).</summary>
        AdjacentMatchOfColor = 1,

        /// <summary>Match in the obstacle's own cell. Declared, unused by the 12 levels.</summary>
        OnCell = 2,

        /// <summary>Boosters only. Declared, unused by the 12 levels.</summary>
        BoosterOnly = 3,

        /// <summary>Immune to everything: the indestructible blocker (A03).</summary>
        None = 4
    }

    public enum ElementColorMode : byte
    {
        None = 0,

        /// <summary>Tokens c1-c6.</summary>
        Fixed = 1,

        /// <summary>Token cx, paired with <see cref="PerTurnBehaviour.CycleColor"/>.</summary>
        Cycling = 2
    }

    /// <summary>Axis occupancy (GDD §7.1). Only <see cref="OccupiesCell"/> is implemented (§10).</summary>
    public enum Occupancy : byte
    {
        OccupiesCell = 0,
        UnderChip = 1,
        OverChip = 2
    }

    /// <summary>
    /// Axis gravity (GDD §7.1). In-scope boxes are <see cref="StaticBlocksFall"/>; the
    /// indestructible blocker is <see cref="StaticPassable"/>.
    /// </summary>
    public enum GravityBehaviour : byte
    {
        /// <summary>Does not move and blocks falling (E10).</summary>
        StaticBlocksFall = 0,

        /// <summary>Does not move and takes the cell, but a fall drops straight through (§5.3).</summary>
        StaticPassable = 1,
        Falls = 2
    }

    /// <summary>Axis spread (GDD §7.1). Declared for extension, unused in scope (E21).</summary>
    public enum SpreadBehaviour : byte
    {
        None = 0,
        EveryNMoves = 1
    }

    public enum GoalRole : byte
    {
        /// <summary>Counts towards its goal type once hp reaches 0.</summary>
        Countable = 0,

        /// <summary>Only the blocker in scope.</summary>
        NotCountable = 1
    }

    /// <summary>Axis perTurnBehaviour (GDD §7.1), applied on POST-TURN step 1.</summary>
    public enum PerTurnBehaviour : byte
    {
        None = 0,

        /// <summary>Colour never changes mid-resolution: damage uses the colour as of DAMAGE (§7.2).</summary>
        CycleColor = 1
    }
}
