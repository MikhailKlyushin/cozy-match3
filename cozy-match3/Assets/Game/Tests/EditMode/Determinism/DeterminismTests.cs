using System.Collections.Generic;
using Match3.Core;
using Match3.Levels;
using Match3.Levels.Authoring;
using Match3.Resolve;
using NUnit.Framework;
using UnityEditor;

namespace Match3.Tests.EditMode.Determinism
{
    /// <summary>
    /// The §13 determinism claim, reduced to comparing numbers: the same seed and the same input
    /// log must produce the same board after every single turn (T24).
    /// </summary>
    public sealed class DeterminismTests
    {
        private const string CatalogPath = "Assets/Game/Content/Levels/LevelCatalog.asset";

        private LevelCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.IsNotNull(_catalog, "missing " + CatalogPath + " - run Match3/Authoring/Generate All");
        }

        [Test]
        public void SameSeedAndInputs_ReplayIdenticallyOnEveryShippedLevel()
        {
            for (int id = 1; id <= _catalog.Count; id++)
            {
                LevelData level = Level(id);
                int seed = 1000 + id;

                var recorded = new List<ReplayInput>();
                ReplayResult first = ReplayRunner.RunGreedy(level, seed, recorded);
                ReplayResult second = ReplayRunner.Run(level, seed, recorded);

                Assert.AreEqual(first.Turns, second.Turns, "level " + id + ": turn count differs");
                for (int turn = 0; turn < first.Turns; turn++)
                {
                    Assert.AreEqual(
                        first.Hashes[turn],
                        second.Hashes[turn],
                        "level " + id + ": boards diverged after turn " + turn);
                }
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentGames()
        {
            LevelData level = Level(1);

            ReplayResult a = ReplayRunner.RunGreedy(level, 11);
            ReplayResult b = ReplayRunner.RunGreedy(level, 22);

            bool identical = a.Turns == b.Turns;
            for (int i = 0; identical && i < a.Turns; i++)
            {
                identical = a.Hashes[i] == b.Hashes[i];
            }

            Assert.IsFalse(identical, "two seeds produced the same game, so the seed does nothing");
        }

        [Test]
        public void ReplayFromTheSameSeed_IsStableAcrossManyRuns()
        {
            LevelData level = Level(5);
            var recorded = new List<ReplayInput>();
            ReplayResult reference = ReplayRunner.RunGreedy(level, 4242, recorded);

            for (int run = 0; run < 5; run++)
            {
                ReplayResult replay = ReplayRunner.Run(level, 4242, recorded);
                CollectionAssert.AreEqual(reference.Hashes, replay.Hashes, "run " + run + " diverged");
            }
        }

        [Test]
        public void RngConsumers_FollowTheFixedOrderOfSection13()
        {
            LevelData level = Level(10);
            var recording = new RecordingRandom(new DeterministicRandom(7));

            ReplayRunner.RunGreedy(level, 7, null, recording);

            List<string> sequence = recording.BuildConsumerSequence();
            Assert.IsNotEmpty(sequence, "nothing consumed the RNG at all");

            // §13, D12: board generation runs first and nothing may draw before it.
            Assert.AreEqual(
                "BoardBuilder",
                sequence[0],
                "the initial board must be the first RNG consumer:\n" + string.Join(" -> ", sequence));

            // The stream is one per attempt, so every consumer must be one of the §13 list.
            var expected = new HashSet<string>
            {
                "BoardBuilder", "WeightedChipSpawnPolicy", "TargetingService",
                "ShuffleService", "MovesBonusService", "DeterministicRandom",
            };

            for (int i = 0; i < sequence.Count; i++)
            {
                Assert.IsTrue(
                    expected.Contains(sequence[i]),
                    "unexpected RNG consumer '" + sequence[i] + "'; §13 fixes the list, and a new "
                    + "one must be appended there before it may draw");
            }
        }

        [Test]
        public void FuzzRun_NeverThrowsAndNeverHitsACapOrDeadlock()
        {
            int games = 0;
            int wins = 0;
            int totalDepth = 0;
            int shuffles = 0;

            for (int id = 1; id <= _catalog.Count; id++)
            {
                LevelData level = Level(id);

                for (int seed = 1; seed <= 30; seed++)
                {
                    ReplayResult result = ReplayRunner.RunRandom(level, seed * 31 + id, seed);
                    games++;
                    totalDepth += result.MaxCascadeDepth;
                    shuffles += result.Shuffles;

                    Assert.AreEqual(0, result.CapHits, "level " + id + " seed " + seed + ": CapHit is always a bug (E05)");
                    Assert.AreEqual(0, result.Deadlocks, "level " + id + " seed " + seed + ": deadlock is always a bug (E19)");

                    if (result.Final == TurnOutcome.Won)
                    {
                        wins++;
                    }
                }
            }

            // Reported rather than asserted: winrate is balance, and T26 owns the target bands.
            TestContext.WriteLine(
                "fuzz: " + games + " games, " + wins + " won, max cascade depth sum " + totalDepth
                + ", shuffles " + shuffles);

            Assert.Greater(games, 0);
        }

        private LevelData Level(int id)
        {
            Assert.IsTrue(_catalog.TryGetById(id, out LevelConfig config), "missing level " + id);
            return LevelConfigConverter.ToLevelData(config);
        }
    }
}
