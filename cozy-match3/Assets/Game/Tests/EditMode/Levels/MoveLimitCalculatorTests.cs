using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Levels;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Levels
{
    public sealed class MoveLimitCalculatorTests
    {
        private const string Open7X7 = @"
            .. .. .. .. .. .. ..
            .. .. .. .. .. .. ..
            .. .. .. .. .. .. ..
            .. .. .. .. .. .. ..
            .. .. .. .. .. .. ..
            .. .. .. .. .. .. ..
            .. .. .. .. .. .. ..";

        [Test]
        public void Compute_ReproducesTheGddWorkedExampleForLevel5()
        {
            LevelData level = GddLevels.Level5();

            int moves = MoveLimitCalculator.Compute(level, GddLevels.Level5Throughput);

            Assert.AreEqual(26, moves, "work 80 / thr 3.4 x 1.10 = 25.9 -> 26 (§10.3)");
            Assert.AreEqual(level.MoveLimit, moves, "level 5 follows the formula exactly");
        }

        [Test]
        public void Compute_GivesSeventeenForLevel11WhoseConfigKeepsEighteen()
        {
            LevelData level = GddLevels.Level11();

            int moves = MoveLimitCalculator.Compute(level, GddLevels.Level11Throughput);

            Assert.AreEqual(17, moves, "work 64 / thr 4.2 x 1.10 = 16.8 -> 17");
            Assert.AreEqual(18, level.MoveLimit, "the +1 is a deliberate, recorded deviation (§10.4)");
        }

        [Test]
        public void Compute_ChargesNestedElementsAboveOuterOnes()
        {
            LevelData level = GddLevels.Level9();

            int moves = MoveLimitCalculator.Compute(level, GddLevels.Level9Throughput);

            Assert.AreEqual(14, moves, "5 outer x 4 + 5 nested x 5 = 45 (§9.3)");
        }

        [Test]
        public void Compute_TakesTheMaxOverColorGoals()
        {
            LevelData level = TestLevel.From(
                Open7X7,
                colorCount: 4,
                moveLimit: 21,
                goals: new[]
                {
                    GoalDefinition.CollectColor(ChipColor.C1, 18),
                    GoalDefinition.CollectColor(ChipColor.C2, 18)
                },
                id: 2);

            int moves = MoveLimitCalculator.Compute(level, 4.5f);

            Assert.AreEqual(21, moves, "one clear serves both colours: work is 72, not 144 (D13)");
        }

        [Test]
        public void Compute_SumsGoalsThatAreNotColorGoals()
        {
            LevelData level = TestLevel.From(
                @"
                .. .. .. .. .. .. ..
                .. bx .. bx .. bx ..
                .. .. .. .. .. .. ..
                .. bx .. bx .. bx ..
                .. .. .. .. .. .. ..
                .. .. bx .. bx .. ..
                .. .. .. .. .. .. ..",
                colorCount: 4,
                moveLimit: 22,
                goals: new[]
                {
                    GoalDefinition.DestroyElement("bx", 8),
                    GoalDefinition.CollectColor(ChipColor.C1, 8)
                },
                id: 3);

            int moves = MoveLimitCalculator.Compute(level, 3.8f);

            Assert.AreEqual(22, moves, "work = 8 x 4 boxes + 8 x 4 colour = 64 (§10.4 level 3)");
        }

        [Test]
        public void CostPerUnit_MatchesTheGddTableForChipsAndBoxes()
        {
            ElementCatalog catalog = BuiltInElementCatalog.Create();

            Assert.AreEqual(5f, CostPerUnit(GoalDefinition.CollectColor(ChipColor.C1, 1), 5, catalog), 0.001f);
            Assert.AreEqual(4f, CostPerUnit(GoalDefinition.DestroyElement("bx", 1), 4, catalog), 0.001f);
            Assert.AreEqual(8f, CostPerUnit(GoalDefinition.DestroyElement("b2", 1), 4, catalog), 0.001f);
            Assert.AreEqual(12f, CostPerUnit(GoalDefinition.DestroyElement("b3", 1), 4, catalog), 0.001f);
            Assert.AreEqual(10f, CostPerUnit(GoalDefinition.DestroyElement("c1", 1), 5, catalog), 0.001f);
            Assert.AreEqual(6f, CostPerUnit(GoalDefinition.DestroyElement("cx", 1), 5, catalog), 0.001f);
            Assert.AreEqual(10f, CostPerUnit(GoalDefinition.DestroyAnyColoredBox(1), 5, catalog), 0.001f);
        }

        [Test]
        public void CostPerUnit_MatchesTheGddTableForBoosterActivations()
        {
            ElementCatalog catalog = BuiltInElementCatalog.Create();

            Assert.AreEqual(14f, CostPerUnit(GoalDefinition.ActivateBooster(BoosterType.RocketH, 1), 5, catalog), 0.001f);
            Assert.AreEqual(14f, CostPerUnit(GoalDefinition.ActivateBooster(BoosterType.RocketV, 1), 5, catalog), 0.001f);
            Assert.AreEqual(22f, CostPerUnit(GoalDefinition.ActivateBooster(BoosterType.Bomb, 1), 5, catalog), 0.001f);
            Assert.AreEqual(24f, CostPerUnit(GoalDefinition.ActivateBooster(BoosterType.Airplane, 1), 5, catalog), 0.001f);
            Assert.AreEqual(36f, CostPerUnit(GoalDefinition.ActivateBooster(BoosterType.Rainbow, 1), 5, catalog), 0.001f);
        }

        [Test]
        public void CostPerUnit_IsZeroForTheIndestructibleBlocker()
        {
            ElementCatalog catalog = BuiltInElementCatalog.Create();

            Assert.AreEqual(
                0f,
                CostPerUnit(GoalDefinition.DestroyElement(ElementTokens.Blocker, 1), 4, catalog),
                0.001f,
                "the blocker can never be a goal (§7.2); the validator reports it");
        }

        [Test]
        public void MultiplierFor_MatchesTheGddTable()
        {
            Assert.AreEqual(1.30f, MoveLimitCalculator.MultiplierFor(DifficultyTier.Easy), 0.001f);
            Assert.AreEqual(1.10f, MoveLimitCalculator.MultiplierFor(DifficultyTier.Medium), 0.001f);
            Assert.AreEqual(0.95f, MoveLimitCalculator.MultiplierFor(DifficultyTier.Hard), 0.001f);
            Assert.AreEqual(0.85f, MoveLimitCalculator.MultiplierFor(DifficultyTier.SuperHard), 0.001f);
        }

        private static float CostPerUnit(GoalDefinition goal, int colorCount, ElementCatalog catalog)
            => MoveLimitCalculator.CostPerUnit(goal, colorCount, catalog);
    }
}
