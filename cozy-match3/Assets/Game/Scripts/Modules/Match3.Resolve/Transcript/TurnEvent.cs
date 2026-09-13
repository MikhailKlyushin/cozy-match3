using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// One transcript entry. Field meaning depends on <see cref="Kind"/> — see
    /// <see cref="TurnEventKind"/> for the per-kind contract.
    /// </summary>
    public readonly struct TurnEvent
    {
        public readonly TurnEventKind Kind;
        public readonly byte Step;
        public readonly byte Wave;
        public readonly byte Flags;
        public readonly GridPos A;
        public readonly GridPos B;
        public readonly ChipColor Color;
        public readonly BoosterType Booster;
        public readonly ElementId Element;
        public readonly int Amount;
        public readonly int Value;
        public readonly int InstanceId;

        /// <summary>Start of this event's slice in <see cref="TurnTranscript.Cells"/>.</summary>
        public readonly int CellsOffset;

        public readonly int CellsCount;

        public TurnEvent(
            TurnEventKind kind,
            byte step = 0,
            byte wave = 0,
            byte flags = 0,
            GridPos a = default,
            GridPos b = default,
            ChipColor color = ChipColor.None,
            BoosterType booster = BoosterType.None,
            ElementId element = default,
            int amount = 0,
            int value = 0,
            int instanceId = 0,
            int cellsOffset = 0,
            int cellsCount = 0)
        {
            Kind = kind;
            Step = step;
            Wave = wave;
            Flags = flags;
            A = a;
            B = b;
            Color = color;
            Booster = booster;
            Element = element;
            Amount = amount;
            Value = value;
            InstanceId = instanceId;
            CellsOffset = cellsOffset;
            CellsCount = cellsCount;
        }

        /// <summary>Valid for <see cref="TurnEventKind.ChipMoved"/>.</summary>
        public ChipMoveFlags MoveFlags => (ChipMoveFlags)Flags;

        /// <summary>Valid for <see cref="TurnEventKind.BoosterActivated"/>.</summary>
        public BoosterActivationSource ActivationSource => (BoosterActivationSource)Flags;

        /// <summary>Valid for <see cref="TurnEventKind.LevelWon"/> and <see cref="TurnEventKind.LevelLost"/>.</summary>
        public LevelEndReason EndReason => (LevelEndReason)Flags;

        /// <summary>Valid for <see cref="TurnEventKind.CapHit"/>.</summary>
        public CapKind Cap => (CapKind)Flags;

        /// <summary>Valid for <see cref="TurnEventKind.ComboActivated"/>: the second participant.</summary>
        public BoosterType ComboBoosterB => (BoosterType)Value;
    }
}
