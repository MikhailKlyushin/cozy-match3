using Match3.Core;

namespace Match3.Board
{
    /// <summary>
    /// One obstacle described purely as values on the §7.1 axes (A04). Adding ice, a chain or a
    /// vine must be a new definition plus a visual, never a change to the resolve pipeline (§10).
    /// </summary>
    public sealed class ElementDefinition
    {
        /// <summary>Sentinel for <see cref="MaxHealth"/>: the blocker takes no damage (A03).</summary>
        public const int InfiniteHealth = -1;

        public ElementDefinition(
            ElementId id,
            string token,
            DamageSourceKind damageSource,
            ElementColorMode colorMode,
            ChipColor fixedColor,
            int maxHealth,
            Occupancy occupancy,
            GravityBehaviour gravity,
            SpreadBehaviour spread,
            GoalRole goalRole,
            PerTurnBehaviour perTurn,
            bool isColoredBox)
        {
            Id = id;
            Token = token;
            DamageSource = damageSource;
            ColorMode = colorMode;
            FixedColor = fixedColor;
            MaxHealth = maxHealth;
            Occupancy = occupancy;
            Gravity = gravity;
            Spread = spread;
            GoalRole = goalRole;
            PerTurn = perTurn;
            IsColoredBox = isColoredBox;
        }

        public ElementId Id { get; }

        /// <summary>Two-character grid token from GDD §10.2.</summary>
        public string Token { get; }

        public DamageSourceKind DamageSource { get; }

        public ElementColorMode ColorMode { get; }

        /// <summary>Meaningful only when <see cref="ColorMode"/> is <see cref="ElementColorMode.Fixed"/>.</summary>
        public ChipColor FixedColor { get; }

        /// <summary><see cref="InfiniteHealth"/> means indestructible.</summary>
        public int MaxHealth { get; }

        public Occupancy Occupancy { get; }

        public GravityBehaviour Gravity { get; }

        public SpreadBehaviour Spread { get; }

        public GoalRole GoalRole { get; }

        public PerTurnBehaviour PerTurn { get; }

        /// <summary>Counts towards the DestroyAnyColoredBox goal: c1-c6 and cx (GDD §8.1).</summary>
        public bool IsColoredBox { get; }

        public bool IsIndestructible => MaxHealth == InfiniteHealth;

        public bool BlocksFall => Gravity == GravityBehaviour.StaticBlocksFall;

        public bool OccupiesCell => Occupancy == Occupancy.OccupiesCell;
    }
}
