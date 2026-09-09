using Match3.Core;

namespace Match3.Cheats
{
    /// <summary>
    /// On-screen text of the cheat panel (GDD §12). The player is Russian-speaking, so the labels
    /// are Russian while the code and the log stay English; there is no localisation layer.
    /// </summary>
    internal static class CheatStrings
    {
        public const string Title = "Читы";

        public const string Close = "Закрыть";

        public const string LevelPlaceholder = "№ уровня";

        public const string GoToLevel = "Перейти";

        public const string LevelEmpty = "Введите номер уровня";

        public const string LevelMissingFormat = "Уровня {0} нет в каталоге";

        public const string LevelReadyFormat = "Уровень {0}";

        public const string WinLevel = "Выиграть уровень";

        public const string LoseLevel = "Проиграть уровень";

        public const string AddMoves = "+5 ходов";

        public const string RocketBooster = "Ракета";

        public const string BombBooster = "Бомба";

        public const string RainbowBooster = "Радужный шар";

        public const string AirplaneBooster = "Самолётик";

        public const string DisarmBooster = "Снять выбор";

        public const string BoosterNotArmed = "Бустер не выбран";

        public const string BoosterArmedFormat = "Выбран: {0} — тапни по клетке";

        public const string SeedPlaceholder = "seed";

        public const string SeedFormat = "Seed: {0}";

        public const string SeedUnknown = "Seed: —";

        public const string ApplySeed = "Применить";

        public const string RestartAttempt = "Заново";

        public const string FreeMoves = "Ходы бесплатно";

        public const string CoordinateGrid = "Показать сетку координат";

        public const string DisableHints = "Отключить подсказки";

        public const string HintNow = "Подсказка сейчас";

        /// <summary>Marked as a dev extension: it is deliberately outside the §12 list.</summary>
        public const string DumpTranscript = "Дамп транскрипта в лог (dev)";

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
