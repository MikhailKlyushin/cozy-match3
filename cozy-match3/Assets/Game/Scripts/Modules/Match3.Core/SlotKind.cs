namespace Match3.Core
{
    /// <summary>What occupies a playable cell on top of any element in it.</summary>
    public enum SlotKind : byte
    {
        Empty = 0,
        Chip = 1,
        Booster = 2
    }
}
