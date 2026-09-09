using Match3.Board;
using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>What produced one damage instance: one match component or one booster activation (D07).</summary>
    public enum DamageOriginKind : byte
    {
        MatchComponent = 0,

        BoosterActivation = 1
    }

    /// <summary>
    /// Damage is de-duplicated per SOURCE, not per cell: area damage covering a box with three
    /// cells is 1 damage (D07, E09), two different sources in one step are 2 (E11).
    /// </summary>
    public readonly struct DamageSource
    {
        public readonly DamageOriginKind Kind;

        /// <summary>Match component index or booster activation index.</summary>
        public readonly int Id;

        public DamageSource(DamageOriginKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }
    }

    /// <summary>
    /// Everything a damage rule may read. Rules decide from the §7.1 axes and this context, never
    /// from a token or an obstacle type (§10).
    /// </summary>
    public readonly struct DamageContext
    {
        public readonly IBoardReader Board;
        public readonly GridPos Cell;
        public readonly ElementInstance Element;
        public readonly ElementDefinition Definition;
        public readonly DamageSource Source;

        /// <summary>Colour of the source match; <see cref="ChipColor.None"/> for a booster (Q4).</summary>
        public readonly ChipColor SourceColor;

        /// <summary>true when the source is a booster, i.e. colourless area damage.</summary>
        public readonly bool IsBoosterDamage;

        /// <summary>true when the source covers the element's own cell instead of a neighbour.</summary>
        public readonly bool IsOnElementCell;

        internal DamageContext(
            IBoardReader board,
            GridPos cell,
            ElementInstance element,
            ElementDefinition definition,
            in DamageSource source,
            ChipColor sourceColor,
            bool isBoosterDamage,
            bool isOnElementCell)
        {
            Board = board;
            Cell = cell;
            Element = element;
            Definition = definition;
            Source = source;
            SourceColor = sourceColor;
            IsBoosterDamage = isBoosterDamage;
            IsOnElementCell = isOnElementCell;
        }
    }
}
