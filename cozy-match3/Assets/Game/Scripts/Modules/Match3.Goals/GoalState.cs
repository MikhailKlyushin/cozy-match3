namespace Match3.Goals
{
    /// <summary>Progress of one goal. <see cref="Value"/> is already clamped at the target (E18).</summary>
    public readonly struct GoalState
    {
        public readonly int Value;
        public readonly bool Closed;

        internal GoalState(int value, bool closed)
        {
            Value = value;
            Closed = closed;
        }
    }
}
