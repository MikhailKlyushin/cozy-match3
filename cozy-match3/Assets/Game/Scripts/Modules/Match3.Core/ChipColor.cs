namespace Match3.Core
{
    /// <summary>Chip colour as an index, never a shade: visuals bind to the index (GDD §4.1, A12).</summary>
    public enum ChipColor : byte
    {
        None = 0,
        C1 = 1,
        C2 = 2,
        C3 = 3,
        C4 = 4,
        C5 = 5,
        C6 = 6
    }

    public static class ChipColors
    {
        /// <summary>GDD §4.1: colorCount is constrained to [4, 6].</summary>
        public const int MinColorCount = 4;

        public const int MaxColorCount = 6;

        public static ChipColor FromIndex(int index) => (ChipColor)(byte)index;

        public static int ToIndex(ChipColor color) => (int)color;

        /// <summary>cx cycling: color = (color mod colorCount) + 1, applied on POST-TURN step 1 (GDD §7.2).</summary>
        public static ChipColor NextCycling(ChipColor current, int colorCount)
        {
            int index = (int)current;
            return (ChipColor)(byte)(index % colorCount + 1);
        }
    }
}
