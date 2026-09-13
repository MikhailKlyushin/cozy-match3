using System.Collections.Generic;
using Match3.Board;
using Match3.Core;
using Match3.Levels;
using Match3.Levels.Authoring;
using NUnit.Framework;
using UnityEditor;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Levels
{
    /// <summary>
    /// Validates the 12 shipped level assets (T14). A broken level must fail loudly here rather
    /// than produce a slightly wrong board at runtime (§3.4).
    /// </summary>
    public sealed class ShippedLevelAssetsTests
    {
        private const string CatalogPath = "Assets/Game/Content/Levels/LevelCatalog.asset";
        private const int ExpectedLevelCount = 12;
        private const int SeedsPerLevel = 20;

        /// <summary>Throughput per level from the GDD §10.4 table, used to re-derive the limit.</summary>
        private static readonly float[] Throughput =
        {
            4.5f, 4.5f, 3.8f, 3.8f, 3.4f, 4.2f, 4.2f, 3.8f, 4.2f, 3.4f, 4.2f, 4.2f,
        };

        /// <summary>Move limits from GDD §10.4.</summary>
        private static readonly int[] MoveLimits =
        {
            18, 21, 22, 22, 26, 15, 27, 25, 14, 19, 18, 27,
        };

        /// <summary>Level 11 is the one recorded deviation: the formula gives 17 (§10.4).</summary>
        private const int DeviatingLevelId = 11;

        private LevelCatalog _catalog;
        private ElementCatalog _elements;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.IsNotNull(_catalog, "missing " + CatalogPath + " - run Match3/Authoring/Generate All");
            _elements = BuiltInElementCatalog.Create();
        }

        [Test]
        public void Catalog_HoldsTwelveLevelsWithSequentialIds()
        {
            Assert.AreEqual(ExpectedLevelCount, _catalog.Count);

            for (int id = 1; id <= ExpectedLevelCount; id++)
            {
                Assert.IsTrue(_catalog.TryGetById(id, out LevelConfig config), "missing level " + id);
                Assert.AreEqual(id, config.Id);
            }
        }

        [Test]
        public void LastLevelId_ComesFromTheDataNotALiteral()
        {
            Assert.AreEqual(ExpectedLevelCount, _catalog.LastLevelId);
            Assert.AreEqual(1, _catalog.FirstLevelId);
        }

        [Test]
        public void EveryLevel_HasTheMoveLimitFromTheDesignTable()
        {
            for (int id = 1; id <= ExpectedLevelCount; id++)
            {
                _catalog.TryGetById(id, out LevelConfig config);
                Assert.AreEqual(MoveLimits[id - 1], config.MoveLimit, "level " + id);
            }
        }

        [Test]
        public void EveryLevel_MatchesTheDerivedMoveLimitExceptTheRecordedDeviation()
        {
            for (int id = 1; id <= ExpectedLevelCount; id++)
            {
                _catalog.TryGetById(id, out LevelConfig config);
                LevelData level = LevelConfigConverter.ToLevelData(config);

                int derived = MoveLimitCalculator.Compute(level, Throughput[id - 1]);

                if (id == DeviatingLevelId)
                {
                    Assert.AreNotEqual(derived, config.MoveLimit, "level 11 is the recorded +1 deviation");
                    Assert.AreEqual(derived + 1, config.MoveLimit);
                    Assert.IsNotEmpty(config.BalanceNotes, "a deviation must carry its reason in the asset");
                    continue;
                }

                Assert.AreEqual(derived, config.MoveLimit, "level " + id + " diverges from the §9.3 formula");
            }
        }

        [Test]
        public void EveryLevel_PassesValidationOnManySeeds()
        {
            var validator = new LevelValidator(_elements);

            for (int id = 1; id <= ExpectedLevelCount; id++)
            {
                _catalog.TryGetById(id, out LevelConfig config);
                LevelData level = LevelConfigConverter.ToLevelData(config);

                for (int seed = 1; seed <= SeedsPerLevel; seed++)
                {
                    ValidationReport report = validator.Validate(level, new DeterministicRandom(seed * 7919 + id));
                    Assert.IsFalse(
                        report.HasErrors,
                        "level " + id + " seed " + seed + ":\n" + report.Describe());
                }
            }
        }

        [Test]
        public void EveryLevel_BuildsABoardWithoutReadyMatchesAndWithALegalMove()
        {
            var builder = new BoardBuilder(_elements, NullLogger.Instance);

            for (int id = 1; id <= ExpectedLevelCount; id++)
            {
                _catalog.TryGetById(id, out LevelConfig config);
                LevelData level = LevelConfigConverter.ToLevelData(config);

                for (int seed = 1; seed <= SeedsPerLevel; seed++)
                {
                    BoardModel board = builder.Build(level, new DeterministicRandom(seed * 104729 + id));

                    var detection = new Match3.Matching.MatchDetectionService(board);
                    var validator = new Match3.Matching.SwapValidator(board, detection);
                    var moves = new Match3.Matching.LegalMoveService(board, validator);

                    Assert.IsFalse(detection.HasAnyMatch(), "level " + id + " seed " + seed + ": ready match (E20)");
                    Assert.IsTrue(moves.HasAnyMove, "level " + id + " seed " + seed + ": no legal move (E20)");
                }
            }
        }

        [Test]
        public void LevelNine_CarriesItsNestedContent()
        {
            _catalog.TryGetById(9, out LevelConfig config);
            LevelData level = LevelConfigConverter.ToLevelData(config);

            Assert.AreEqual(5, level.Contents.Count, "§10.4: five nested boxes");
            for (int i = 0; i < level.Contents.Count; i++)
            {
                Assert.AreEqual(ElementTokens.Box1, level.Contents[i].Token);
            }
        }

        [Test]
        public void LevelTen_OverridesItsSpawnerColumns()
        {
            _catalog.TryGetById(10, out LevelConfig config);
            LevelData level = LevelConfigConverter.ToLevelData(config);

            Assert.AreEqual(new List<int> { 0, 1, 2, 5, 6, 7 }, new List<int>(level.Spawners));
        }

        [Test]
        public void EarlyLevels_UseTheShorterHintDelay()
        {
            for (int id = 1; id <= 3; id++)
            {
                _catalog.TryGetById(id, out LevelConfig config);
                Assert.AreEqual(3f, config.HintDelaySeconds, 0.001f, "levels 1-3 teach faster (§5.5)");
            }

            _catalog.TryGetById(4, out LevelConfig fourth);
            Assert.AreEqual(5f, fourth.HintDelaySeconds, 0.001f);
        }

        [Test]
        public void ColorCounts_FollowTheDesignCurve()
        {
            var expected = new[] { 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6 };

            for (int id = 1; id <= ExpectedLevelCount; id++)
            {
                _catalog.TryGetById(id, out LevelConfig config);
                Assert.AreEqual(expected[id - 1], config.ColorCount, "level " + id);
            }
        }
    }
}
