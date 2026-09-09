using System.Collections.Generic;
using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// Registry of <see cref="IDamageSourceRule"/> keyed by the §7.1 axis value. Adding ice, a
    /// chain or a vine is a new catalogue entry, not a change here (§10).
    /// </summary>
    public static class DamageRules
    {
        public static IReadOnlyList<IDamageSourceRule> CreateDefault()
            => new IDamageSourceRule[]
            {
                new AdjacentMatchRule(),
                new AdjacentMatchOfColorRule(),
                new OnCellRule(),
                new BoosterOnlyRule(),
                new ImmuneRule()
            };
    }

    /// <summary>Tokens bx/b2/b3: any match in an orthogonally adjacent cell and any blast (§7.2).</summary>
    internal sealed class AdjacentMatchRule : IDamageSourceRule
    {
        public DamageSourceKind Kind => DamageSourceKind.AdjacentMatch;

        // The service only offers covered cells and their orthogonal neighbours, so every source
        // that reaches this rule damages the box. Diagonals never reach it (§7.1).
        public bool IsDamagedBy(in DamageContext ctx) => true;
    }

    /// <summary>Tokens c1-c6 and cx: own colour only, yet a colourless blast still destroys it (Q4).</summary>
    internal sealed class AdjacentMatchOfColorRule : IDamageSourceRule
    {
        public DamageSourceKind Kind => DamageSourceKind.AdjacentMatchOfColor;

        public bool IsDamagedBy(in DamageContext ctx)
            => ctx.IsBoosterDamage || ctx.SourceColor == ctx.Element.CurrentColor;
    }

    /// <summary>Damaged only by a source covering the element's own cell (§7.1).</summary>
    internal sealed class OnCellRule : IDamageSourceRule
    {
        public DamageSourceKind Kind => DamageSourceKind.OnCell;

        public bool IsDamagedBy(in DamageContext ctx) => ctx.IsOnElementCell;
    }

    /// <summary>Immune to matches, damaged by booster area damage only (§7.1).</summary>
    internal sealed class BoosterOnlyRule : IDamageSourceRule
    {
        public DamageSourceKind Kind => DamageSourceKind.BoosterOnly;

        public bool IsDamagedBy(in DamageContext ctx) => ctx.IsBoosterDamage;
    }

    /// <summary>Blocker: never damaged, and it never stops the source either (A03, D11, E08).</summary>
    internal sealed class ImmuneRule : IDamageSourceRule
    {
        public DamageSourceKind Kind => DamageSourceKind.None;

        public bool IsDamagedBy(in DamageContext ctx) => false;
    }
}
