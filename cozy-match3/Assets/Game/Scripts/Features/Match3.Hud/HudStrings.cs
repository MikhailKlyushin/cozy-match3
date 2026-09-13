namespace Match3.Hud
{
    /// <summary>
    /// On-screen text (GDD §11.1). The build ships a single language — English — so the literals
    /// live here rather than behind a localisation layer.
    /// </summary>
    internal static class HudStrings
    {
        public const string MovesCaption = "Moves";

        public const string LevelFormat = "Level {0}";

        public const string WinTitle = "Level complete";

        public const string WinButton = "Next";

        public const string LoseTitle = "Out of moves";

        public const string LoseButton = "Retry";

        public const string EndOfContentTitle = "All levels complete";

        public const string EndOfContentButton = "Play again from level 1";

        public const string LastLevelFormat = "Last level: {0}";
    }
}
