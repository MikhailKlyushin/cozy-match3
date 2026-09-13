using Match3.Core;

namespace Match3.Cheats
{
    /// <summary>
    /// On-screen text of the cheat panel (GDD §12). The build ships a single language — English —
    /// for both the labels and the log; there is no localisation layer.
    /// </summary>
    internal static class CheatStrings
    {
        public const string Title = "Cheats";

        public const string Close = "Close";

        public const string LevelPlaceholder = "Level #";

        public const string GoToLevel = "Go";

        public const string LevelEmpty = "Enter a level number";

        public const string LevelMissingFormat = "Level {0} is not in the catalog";

        public const string LevelReadyFormat = "Level {0}";

        public const string WinLevel = "Win level";

        public const string LoseLevel = "Lose level";

        public const string AddMoves = "+5 moves";

        public const string RocketBooster = "Rocket";

        public const string BombBooster = "Bomb";

        public const string RainbowBooster = "Rainbow ball";

        public const string AirplaneBooster = "Airplane";

        public const string DisarmBooster = "Clear selection";

        public const string BoosterNotArmed = "No booster armed";

        public const string BoosterArmedFormat = "Armed: {0} — tap a cell";

        public const string SeedPlaceholder = "seed";

        public const string SeedFormat = "Seed: {0}";

        public const string SeedUnknown = "Seed: —";

        public const string ApplySeed = "Apply";

        public const string RestartAttempt = "Restart";

        public const string FreeMoves = "Free moves";

        public const string CoordinateGrid = "Show coordinate grid";

        public const string DisableHints = "Disable hints";

        public const string HintNow = "Hint now";

        /// <summary>Marked as a dev extension: it is deliberately outside the §12 list.</summary>
        public const string DumpTranscript = "Dump transcript to log (dev)";

        public static string BoosterName(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.RocketH:
                case BoosterType.RocketV:
                    return RocketBooster;

                case BoosterType.Bomb:
                    return BombBooster;

                case BoosterType.Rainbow:
                    return RainbowBooster;

                case BoosterType.Airplane:
                    return AirplaneBooster;

                default:
                    return BoosterNotArmed;
            }
        }
    }
}
