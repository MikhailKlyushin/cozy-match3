using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>One booster firing in one cell (GDD §6.1).</summary>
    public readonly struct BoosterActivation
    {
        public readonly GridPos Cell;
        public readonly BoosterType Booster;

        /// <summary>
        /// Rainbow ball colour: the colour of the chip it was swapped with, or
        /// <see cref="ChipColor.None"/> to fall back to the most needed colour (§6.2, E06).
        /// </summary>
        public readonly ChipColor ColorHint;

        public BoosterActivation(GridPos cell, BoosterType booster, ChipColor colorHint)
        {
            Cell = cell;
            Booster = booster;
            ColorHint = colorHint;
        }
    }
}
