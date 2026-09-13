using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Match3.Levels;
using Match3.Levels.Authoring;
using Match3.Resolve;
using Match3.Tests.EditMode.Determinism;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Match3.Tests.EditMode.Balance
{
    /// <summary>
    /// T26: the greedy bot plays every shipped level 200 times, and the run is written out as
    /// docs/balance-report.md against the §9.1 bands and the §14 thresholds. Only the counters
    /// that are always a bug are asserted - winrate is balance, and the report is the deliverable.
    /// </summary>
    public sealed class BalanceReportTests
    {
        private const string CatalogPath = "Assets/Game/Content/Levels/LevelCatalog.asset";
        private const string ReportPath = "docs/balance-report.md";
        private const int GamesPerLevel = 200;

        /// <summary>Below this a player stops blaming their own play (§9.1 guardrail).</summary>
        private const float WinrateFloor = 12f;

        private const float CascadeDepthMin = 1.3f;
        private const float CascadeDepthMax = 1.8f;

        /// <summary>§14: a level a player leaves with more than six spare moves is too generous.</summary>
        private const float MoveSurplusThreshold = 6f;

        /// <summary>
        /// The single §9.4 lever proposed for a level that misses its band by more than 10 p.p.
        /// Written by hand: which lever fits depends on why the level misses, and rebalancing is
        /// a separate task - T26 only measures and proposes.
        /// </summary>
        private static readonly Advice[] Advices =
        {
            new Advice(
                9,
                "Цель — 10 `bx`, а на поле ровно 10 ящиков: 5 внешних и 5 вложенных. Запаса нет, "
                + "каждый ящик обязателен, и вложенный требует двух попаданий подряд по одной клетке. "
                + "Лимит 14 выведен по throughput 4.2, который вложенность не учитывает. "
                + "Рычаг — **лимит ходов** (§9.4 №2), 14 → 18: линейный, предсказуемый и не трогает "
                + "саму вложенность, которую уровень вводит."),
            new Advice(
                11,
                "Цель — активировать 3 ракеты и 1 бомбу за 18 ходов. Бомба требует пересечения двух "
                + "линий — самое редкое событие §4.2, отсюда дисперсия. Рычаг — **размер цели** "
                + "(§9.4 №5), бомба 1 → ракеты 4: лимит ходов здесь уже подняли на +1 сверх формулы, "
                + "и второй раз тот же рычаг лечил бы симптом, а не редкость бомбы."),
            new Advice(
                12,
                "Единственный уровень, который боту слишком лёгок. Три цели закрываются параллельно, "
                + "а бонус остатка ходов добивает ящики. Рычаг — **лимит ходов** (§9.4 №2), 27 → 22. "
                + "Отклонение вверх именно у бота ожидаемо: полоса 65–70 % назначена живым игрокам, "
                + "а бот не ошибается, — поэтому правку стоит держать до первых данных с людей."),
        };

        private static readonly Band[] TargetBands =
        {
            new Band(1, 92, 96), new Band(2, 92, 96), new Band(3, 92, 96), new Band(4, 92, 96),
            new Band(5, 88, 92), new Band(6, 92, 95), new Band(7, 85, 90), new Band(8, 90, 95),
            new Band(9, 90, 95), new Band(10, 90, 95), new Band(11, 85, 90), new Band(12, 65, 70),
        };

        [Test]
        public void GreedyBot_ClearsEveryShippedLevelWithoutCapHitsOrDeadlocks()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "missing " + CatalogPath + " - run Match3/Authoring/Generate All");

            var rows = new List<LevelStats>(catalog.Count);

            for (int id = 1; id <= catalog.Count; id++)
            {
                Assert.IsTrue(catalog.TryGetById(id, out LevelConfig config), "missing level " + id);
                rows.Add(Play(LevelConfigConverter.ToLevelData(config)));
            }

            string report = BuildReport(rows);
            Write(report);
            TestContext.WriteLine(report);

            for (int i = 0; i < rows.Count; i++)
            {
                LevelStats stats = rows[i];

                Assert.AreEqual(0, stats.CapHits, "level " + stats.Id + ": a wave cap hit is always a bug (E05, §14)");
                Assert.AreEqual(0, stats.Deadlocks, "level " + stats.Id + ": a deadlock is always a bug (E19, §14)");
                Assert.AreEqual(0, stats.Rejected, "level " + stats.Id + ": the bot only ever plays legal inputs");

                // §9.3 step 4: a level unbeatable under reasonable play is broken, however good
                // its arithmetic looked.
                Assert.Greater(
                    stats.Winrate,
                    WinrateFloor,
                    "level " + stats.Id + " is below the §9.1 guardrail at " + Format(stats.Winrate) + " %");
            }
        }

        private static LevelStats Play(LevelData level)
        {
            var stats = new LevelStats
            {
                Id = level.Id,
                Tier = level.Tier.ToString().ToLowerInvariant(),
                MoveLimit = level.MoveLimit,
            };

            for (int i = 0; i < GamesPerLevel; i++)
            {
                // One seed per game, offset by level so no two levels share a board stream.
                ReplayResult result = ReplayRunner.RunGreedy(level, (level.Id * 7919) + i);

                stats.Games++;
                stats.Turns += result.Turns;
                stats.CascadeDepthSum += result.CascadeDepthSum;
                stats.Shuffles += result.Shuffles;
                stats.CapHits += result.CapHits;
                stats.Deadlocks += result.Deadlocks;
                stats.Rejected += result.Rejected;

                if (result.MaxCascadeDepth > stats.MaxCascadeDepth)
                {
                    stats.MaxCascadeDepth = result.MaxCascadeDepth;
                }

                if (result.Final == TurnOutcome.Won)
                {
                    stats.Wins++;
                    stats.MovesLeftOnWin += result.MovesLeft;
                }
            }

            return stats;
        }

        private static string BuildReport(List<LevelStats> rows)
        {
            var text = new StringBuilder(8192);

            text.Append("# Отчёт прогона ботом (T26)\n\n");
            text.Append("Файл сгенерирован тестом `BalanceReportTests`; править руками бессмысленно —\n");
            text.Append("следующий прогон перезапишет.\n\n");
            text.Append("Жадный бот, ").Append(GamesPerLevel).Append(" партий на уровень, свой seed на партию.\n");
            text.Append("Приоритет хода: продвинуть первую незакрытую цель → потратить бустер, лежащий на поле →\n");
            text.Append("создать бустер высшего ранга → любой легальный свап.\n\n");
            text.Append("Бот — не игрок: он не ошибается, не медлит и не промахивается мимо клетки. Его winrate —\n");
            text.Append("оценка решаемости уровня (§9.3) и верхняя граница для живых игроков, а не прогноз\n");
            text.Append("полосы §9.1. Уровень, который не проходит бот, не пройдёт и человек.\n\n");

            AppendTable(text, rows, out float overallDepth);

            text.Append("\nСредняя глубина каскада по всем уровням: ").Append(Format(overallDepth))
                .Append(" при целевом диапазоне ").Append(Format(CascadeDepthMin))
                .Append('–').Append(Format(CascadeDepthMax)).Append(" (§14).");

            if (overallDepth > CascadeDepthMax)
            {
                text.Append(" Выше полосы: §14 тревожится о падении ниже 1.3 («поле слишком раздроблено»),\n")
                    .Append("а здесь поля наоборот открытые. Отклонение зафиксировано, правка не предлагается.");
            }
            else if (overallDepth < CascadeDepthMin)
            {
                text.Append(" **Ниже полосы** — по §14 это признак слишком раздробленного поля.");
            }

            text.Append("\n\n## Уровни вне полосы §9.1 более чем на 10 п.п.\n\n");
            AppendOutliers(text, rows);
            AppendMoveSurplus(text, rows);

            return text.ToString();
        }

        private static void AppendTable(StringBuilder text, List<LevelStats> rows, out float overallDepth)
        {
            text.Append("| Ур. | Тир | Партий | Winrate | Полоса §9.1 | Δ п.п. | Ост. ходов при победе ")
                .Append("| Каскад ср. | Каскад макс. | Перемеш. | CapHit | Deadlock |\n");
            text.Append("|---|---|---|---|---|---|---|---|---|---|---|---|\n");

            int totalTurns = 0;
            int totalDepth = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                LevelStats stats = rows[i];
                Band band = BandOf(stats.Id);
                float deviation = band.DeviationOf(stats.Winrate);

                totalTurns += stats.Turns;
                totalDepth += stats.CascadeDepthSum;

                text.Append("| ").Append(stats.Id)
                    .Append(" | ").Append(stats.Tier)
                    .Append(" | ").Append(stats.Games)
                    .Append(" | ").Append(Format(stats.Winrate)).Append(" %")
                    .Append(" | ").Append(band.Min).Append('–').Append(band.Max).Append(" %")
                    .Append(" | ").Append(deviation <= 0f ? "в полосе" : Format(deviation))
                    .Append(" | ").Append(Format(stats.AverageMovesLeftOnWin))
                    .Append(" из ").Append(stats.MoveLimit)
                    .Append(" | ").Append(Format(stats.AverageCascadeDepth))
                    .Append(" | ").Append(stats.MaxCascadeDepth)
                    .Append(" | ").Append(stats.Shuffles)
                    .Append(" | ").Append(stats.CapHits)
                    .Append(" | ").Append(stats.Deadlocks)
                    .Append(" |\n");
            }

            overallDepth = totalTurns > 0 ? (float)totalDepth / totalTurns : 0f;
        }

        private static void AppendOutliers(StringBuilder text, List<LevelStats> rows)
        {
            bool any = false;

            for (int i = 0; i < rows.Count; i++)
            {
                LevelStats stats = rows[i];
                Band band = BandOf(stats.Id);
                float deviation = band.DeviationOf(stats.Winrate);

                if (deviation <= 10f)
                {
                    continue;
                }

                any = true;

                text.Append("### Уровень ").Append(stats.Id).Append(" — ")
                    .Append(Format(stats.Winrate)).Append(" % против ")
                    .Append(band.Min).Append('–').Append(band.Max).Append(" %, отклонение ")
                    .Append(Format(deviation)).Append(" п.п.\n\n")
                    .Append(AdviceFor(stats.Id)).Append("\n\n");
            }

            if (!any)
            {
                text.Append("Нет: все уровни в пределах 10 п.п. от целевой полосы.\n\n");
            }

            text.Append("Правка баланса — отдельная задача: T26 только измеряет и называет рычаг.\n");
        }

        private static void AppendMoveSurplus(StringBuilder text, List<LevelStats> rows)
        {
            text.Append("\n## Остаток ходов при победе выше порога §14 (> ")
                .Append(Format(MoveSurplusThreshold)).Append(")\n\n");

            bool any = false;

            for (int i = 0; i < rows.Count; i++)
            {
                LevelStats stats = rows[i];
                if (stats.Wins == 0 || stats.AverageMovesLeftOnWin <= MoveSurplusThreshold)
                {
                    continue;
                }

                any = true;
                text.Append("- Уровень ").Append(stats.Id).Append(": ")
                    .Append(Format(stats.AverageMovesLeftOnWin)).Append(" из ").Append(stats.MoveLimit)
                    .Append(".\n");
            }

            if (!any)
            {
                text.Append("Нет.\n");
                return;
            }

            text.Append("\nПорог §14 написан про живого игрока, а бот не тратит ходов впустую, поэтому\n");
            text.Append("остаток завышен по построению и сам по себе резать лимиты не повод. Значимо здесь\n");
            text.Append("другое: там, где бот оставляет больше половины лимита, запас перестаёт быть\n");
            text.Append("страховкой от ошибки и становится незамеченной лёгкостью уровня.\n");
        }

        private static string AdviceFor(int id)
        {
            for (int i = 0; i < Advices.Length; i++)
            {
                if (Advices[i].Id == id)
                {
                    return Advices[i].Text;
                }
            }

            return "Рычаг не назначен: уровень вышел из полосы после того, как отчёт был написан.";
        }

        private static Band BandOf(int id)
        {
            for (int i = 0; i < TargetBands.Length; i++)
            {
                if (TargetBands[i].Id == id)
                {
                    return TargetBands[i];
                }
            }

            return new Band(id, 0, 100);
        }

        private static string Format(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        private static void Write(string report)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllText(Path.Combine(root, ReportPath), report, new UTF8Encoding(false));
        }

        private readonly struct Advice
        {
            public readonly int Id;
            public readonly string Text;

            public Advice(int id, string text)
            {
                Id = id;
                Text = text;
            }
        }

        private readonly struct Band
        {
            public readonly int Id;
            public readonly int Min;
            public readonly int Max;

            public Band(int id, int min, int max)
            {
                Id = id;
                Min = min;
                Max = max;
            }

            /// <summary>Percentage points outside the band, zero when inside it.</summary>
            public float DeviationOf(float winrate)
            {
                if (winrate < Min)
                {
                    return Min - winrate;
                }

                return winrate > Max ? winrate - Max : 0f;
            }
        }

        private sealed class LevelStats
        {
            public int Id;
            public string Tier;
            public int MoveLimit;
            public int Games;
            public int Wins;
            public int Turns;
            public int MovesLeftOnWin;
            public int CascadeDepthSum;
            public int MaxCascadeDepth;
            public int Shuffles;
            public int CapHits;
            public int Deadlocks;
            public int Rejected;

            public float Winrate => Games > 0 ? 100f * Wins / Games : 0f;

            public float AverageMovesLeftOnWin => Wins > 0 ? (float)MovesLeftOnWin / Wins : 0f;

            public float AverageCascadeDepth => Turns > 0 ? (float)CascadeDepthSum / Turns : 0f;
        }
    }
}
