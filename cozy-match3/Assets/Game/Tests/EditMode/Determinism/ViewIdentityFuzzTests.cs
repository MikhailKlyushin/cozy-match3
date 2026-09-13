using Match3.Bootstrap;
using Match3.Core;
using Match3.Levels;
using Match3.Levels.Authoring;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using UnityEditor;

namespace Match3.Tests.EditMode.Determinism
{
    /// <summary>
    /// Plays every shipped level and checks, after each turn, that a view driven by nothing but
    /// the transcript still holds exactly the board the model holds (rules T5/V1). A slot that
    /// changes hands without the transcript saying so shows up on screen as two chips in one
    /// cell, and only a whole-playthrough sweep finds the paths that do it.
    /// </summary>
    public sealed class ViewIdentityFuzzTests
    {
        private const string CatalogPath = "Assets/Game/Content/Levels/LevelCatalog.asset";

        private const int SeedsPerLevel = 4;

        private LevelCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            Assert.IsNotNull(_catalog, "missing " + CatalogPath + " - run Match3/Authoring/Generate All");
        }

        [Test]
        public void EveryPlaythrough_LeavesTheViewHoldingExactlyTheBoard()
        {
            int turns = 0;

            for (int id = 1; id <= _catalog.Count; id++)
            {
                LevelData level = Level(id);

                for (int seed = 1; seed <= SeedsPerLevel; seed++)
                {
                    turns += Play(level, seed * 23 + id, id);
                }
            }

            Assert.Greater(turns, 0, "nothing was played, so nothing was checked");
        }

        /// <summary>Plays greedily to the end of the level and returns the number of turns taken.</summary>
        private static int Play(LevelData level, int seed, int levelId)
        {
            LevelRuntime runtime = LevelRuntime.Create(
                level, seed, Board.BuiltInElementCatalog.Create(), NullLogger.Instance);

            var view = new TranscriptViewSimulator(runtime.Board);
            int turns = 0;

            while (runtime.TurnRule.CanAcceptInput && runtime.TurnRule.MovesLeft > 0)
            {
                HintPlan plan = runtime.Hint.GetHint();
                if (!plan.HasHint)
                {
                    break;
                }

                TurnTranscript transcript = plan.Kind == HintKind.TapBooster
                    ? runtime.TurnRule.ExecuteTap(plan.A)
                    : runtime.TurnRule.ExecuteSwap(plan.A, plan.B);

                view.Apply(transcript);
                turns++;

                if (view.TryFindMismatch(runtime.Board, out string message))
                {
                    Assert.Fail("level " + levelId.ToString() + " seed " + seed.ToString()
                                + " turn " + turns.ToString() + ": " + message);
                }
            }

            return turns;
        }

        private LevelData Level(int id)
        {
            Assert.IsTrue(_catalog.TryGetById(id, out LevelConfig config), "missing level " + id);
            return LevelConfigConverter.ToLevelData(config);
        }
    }
}
