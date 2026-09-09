namespace Match3.Hud
{
    /// <summary>
    /// On-screen text (GDD §11.1). The player is Russian-speaking, so the strings are Russian
    /// while the code stays English; there is no localisation layer in this build.
    /// </summary>
    internal static class HudStrings
    {
        public const string MovesCaption = "Ходы";

        public const string WinTitle = "Уровень пройден";

        public const string WinButton = "Далее";

        public const string LoseTitle = "Ходы закончились";

        public const string LoseButton = "Заново";

        public const string EndOfContentTitle = "Все уровни пройдены";

        public const string EndOfContentButton = "Играть заново с 1-го уровня";

        public const string LastLevelFormat = "Последний уровень: {0}";
    }
}
