using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>One sub-activation of a combination plan (§6.3).</summary>
    public readonly struct ComboStep
    {
        public readonly GridPos Cell;
        public readonly BoosterType Booster;

        /// <summary>
        /// Delay before this sub-activation, seconds, counted from the previous step. Mass
        /// transformations use 0.06-0.08 s (§6.3): a simultaneous detonation reads as one flash
        /// and devalues the reward.
        /// </summary>
        public readonly float Delay;

        /// <summary>
        /// true - the chip in <see cref="Cell"/> first turns into <see cref="Booster"/>
        /// (ChipTransformed), then fires from there. false - the cell is already the place the
        /// effect happens, so an airplane step carries the impact cell targeting has picked.
        /// </summary>
        public readonly bool TransformFirst;

        internal ComboStep(GridPos cell, BoosterType booster, float delay, bool transformFirst)
        {
            Cell = cell;
            Booster = booster;
            Delay = delay;
            TransformFirst = transformFirst;
        }
    }
}
