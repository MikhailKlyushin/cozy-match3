using Match3.Goals;

namespace Match3.Hud
{
    /// <summary>
    /// What a popup needs to render: goal progress for win and lose, the last level id for
    /// End of content (§11.1).
    /// </summary>
    public readonly struct PopupContent
    {
        public readonly IGoalTracker Goals;

        /// <summary>Highest id in the catalogue, never a literal 12 (§8.3).</summary>
        public readonly int LastLevelId;

        private PopupContent(IGoalTracker goals, int lastLevelId)
        {
            Goals = goals;
            LastLevelId = lastLevelId;
        }

        public static PopupContent ForGoals(IGoalTracker goals) => new PopupContent(goals, 0);

        public static PopupContent ForEndOfContent(int lastLevelId) => new PopupContent(null, lastLevelId);
    }
}
