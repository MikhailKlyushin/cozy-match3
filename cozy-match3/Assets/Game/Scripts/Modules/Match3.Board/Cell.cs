using Match3.Core;

namespace Match3.Board
{
    /// <summary>What sits in a playable cell above any element occupying it.</summary>
    public struct ChipSlot
    {
        public SlotKind Kind;
        public ChipColor Color;
        public BoosterType Booster;

        /// <summary>
        /// Deterministic counter, not a Guid: the view maps it to a ChipView while replaying the
        /// transcript, so identity must survive a move.
        /// </summary>
        public int InstanceId;

        /// <summary>Booster already fired in this resolve step; what stops two rockets looping (D08).</summary>
        public bool Consumed;

        public bool IsEmpty => Kind == SlotKind.Empty;

        public bool IsChip => Kind == SlotKind.Chip;

        public bool IsBooster => Kind == SlotKind.Booster;
    }

    public struct Cell
    {
        /// <summary>No element in this cell.</summary>
        public const int NoElement = -1;

        public CellKind Kind;
        public ChipSlot Slot;

        /// <summary>Index into the board's element storage, or <see cref="NoElement"/>.</summary>
        public int ElementIndex;

        public bool IsPlayable => Kind == CellKind.Playable;

        public bool HasElement => ElementIndex != NoElement;
    }
}
