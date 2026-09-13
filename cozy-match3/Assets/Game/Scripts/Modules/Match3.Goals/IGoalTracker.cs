using Match3.Board;
using Match3.Core;

namespace Match3.Goals
{
    /// <summary>
    /// Level goals (GDD §8.1-§8.3). Config order is both the display order and the booster
    /// targeting priority (§8.2), so the tracker never reorders goals.
    /// </summary>
    public interface IGoalTracker
    {
        int Count { get; }

        bool AllClosed { get; }

        /// <summary>Config order, never sorted (§8.2). -1 when every goal is closed.</summary>
        int FirstUnclosedIndex { get; }

        GoalDefinition GetDefinition(int index);

        GoalState GetState(int index);

        /// <summary>
        /// Appends to <paramref name="deltas"/> without clearing it: the buffer belongs to the
        /// caller. Credit beyond the target is discarded (E18).
        /// </summary>
        void CreditChip(ChipColor color, PooledList<GoalDelta> deltas);

        /// <summary>
        /// Called at hp = 0 only: partial damage never counts (§8.1). Elements with
        /// <see cref="Match3.Core.GoalRole.NotCountable"/> are ignored.
        /// </summary>
        void CreditElement(ElementDefinition definition, PooledList<GoalDelta> deltas);

        /// <summary>
        /// Counts activations, not creations (Q1). The tracker credits exactly what the caller
        /// reports, which is what makes chain and combination activations count like a
        /// player-fired one, one activation per participant (§6.1).
        /// </summary>
        void CreditBoosterActivation(BoosterType booster, PooledList<GoalDelta> deltas);

        /// <summary>Win cheat (§12): closes every goal at its target and reports the deltas.</summary>
        void CloseAll(PooledList<GoalDelta> deltas);
    }
}
