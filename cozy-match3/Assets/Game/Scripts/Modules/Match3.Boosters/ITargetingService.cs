using Match3.Core;

namespace Match3.Boosters
{
    /// <summary>
    /// The one deterministic rule behind the airplane and every combination with the rainbow ball
    /// (GDD §6.1, D09, A07). A second targeting routine anywhere in the project is a review finding.
    /// </summary>
    public interface ITargetingService
    {
        /// <summary>
        /// §6.1: (1) the first unclosed goal in config order → (2) among the cells serving it, the
        /// one whose destruction yields the most goal units (an obstacle with 1 hp outranks one
        /// with 2) → (3) tie-break: lowest y, then lowest x → (4) no unclosed goal is served by any
        /// cell: a cell drawn from the RNG among the cells holding chips
        /// (<see cref="RandomConsumer.TargetingTieBreak"/>).
        /// <see cref="GridPos.Invalid"/> only when the board holds no such cell at all.
        /// </summary>
        GridPos PickGoalTarget();

        /// <summary>
        /// Up to <paramref name="count"/> distinct cells by the same priority, for the combinations
        /// aiming at targets #1, #2 and #3 (§6.3). Appends to <paramref name="result"/> without
        /// clearing it and returns the number of cells added.
        /// </summary>
        int PickGoalTargets(int count, CellBuffer result);

        /// <summary>
        /// §6.1: (1) the colour of the first unclosed CollectColor goal in config order → (2) the
        /// colour with the most chips on the board → (3) tie-break: the lowest colour index.
        /// <see cref="ChipColor.None"/> when the board holds no chips.
        /// </summary>
        ChipColor PickNeededColor();
    }
}
