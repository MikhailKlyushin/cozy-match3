namespace Match3.Goals
{
    /// <summary>
    /// One unit of progress. A single credit can move several goals: a destroyed c1 advances both
    /// DestroyElement("c1") and DestroyAnyColoredBox (GDD §8.1).
    /// </summary>
    public readonly struct GoalDelta
    {
        public readonly int GoalIndex;

        /// <summary>
        /// Clamped part of the credit (E18): 0 means the goal was already closed and the credit
        /// was discarded, which consumers report as no <c>GoalProgress</c> event at all.
        /// </summary>
        public readonly int Delta;

        public readonly int NewValue;

        internal GoalDelta(int goalIndex, int delta, int newValue)
        {
            GoalIndex = goalIndex;
            Delta = delta;
            NewValue = newValue;
        }
    }
}
