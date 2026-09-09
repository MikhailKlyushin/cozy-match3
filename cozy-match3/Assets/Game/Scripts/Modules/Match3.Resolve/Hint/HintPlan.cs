using Match3.Core;

namespace Match3.Resolve
{
    public enum HintKind : byte
    {
        None = 0,

        Swap = 1,

        /// <summary>Priority 4 fallback: no legal swap exists at all (GDD §5.5).</summary>
        TapBooster = 2
    }

    /// <summary>
    /// A single suggested move, chosen deterministically without RNG so it can be covered by a
    /// test (GDD §5.5, D15). <see cref="RulePriority"/> is reported by the hint_shown metric (§14).
    /// </summary>
    public readonly struct HintPlan
    {
        public static readonly HintPlan None = default;

        public readonly HintKind Kind;
        public readonly GridPos A;
        public readonly GridPos B;

        /// <summary>Which of the four §5.5 rules produced this hint, 1-4.</summary>
        public readonly int RulePriority;

        public HintPlan(HintKind kind, GridPos a, GridPos b, int rulePriority)
        {
            Kind = kind;
            A = a;
            B = b;
            RulePriority = rulePriority;
        }

        public bool HasHint => Kind != HintKind.None;

        public static HintPlan Swap(GridPos a, GridPos b, int rulePriority)
            => new HintPlan(HintKind.Swap, a, b, rulePriority);

        public static HintPlan TapBooster(GridPos cell)
            => new HintPlan(HintKind.TapBooster, cell, GridPos.Invalid, 4);
    }
}
