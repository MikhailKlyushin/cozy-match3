using Match3.Board;
using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Boosters
{
    public sealed class TargetingServiceTests
    {
        private const int Seed = 20260909;

        private PooledList<GoalDelta> _deltas;

        [SetUp]
        public void SetUp()
        {
            _deltas = new PooledList<GoalDelta>();
        }

        [Test]
        public void PickGoalTarget_UsesFirstUnclosedGoalInConfigOrder()
        {
            BoardModel board = BoardFixture.From(@"
                t2 t1 t2
                t1 t2 t1
                t2 t2 t1");
            var goals = new GoalTracker(new[]
            {
                GoalDefinition.CollectColor(ChipColor.C1, 5),
                GoalDefinition.CollectColor(ChipColor.C2, 5)
            });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(new GridPos(2, 0), target, "goal 1 serves colour C1, not C2");
        }

        [Test]
        public void PickGoalTarget_SkipsClosedGoalAndKeepsConfigOrder()
        {
            BoardModel board = BoardFixture.From(@"
                t2 t1 t2
                t1 t2 t1
                t2 t2 t1");
            var goals = new GoalTracker(new[]
            {
                GoalDefinition.CollectColor(ChipColor.C1, 1),
                GoalDefinition.CollectColor(ChipColor.C2, 5)
            });
            goals.CreditChip(ChipColor.C1, _deltas);

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(new GridPos(0, 0), target, "the C1 goal is closed, so C2 is aimed at");
        }

        [Test]
        public void PickGoalTarget_BreaksTieByLowestYThenLowestX()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t3 t1
                t3 t3 t3
                t3 t1 t1");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 9) });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(new GridPos(1, 0), target);
        }

        [Test]
        public void PickGoalTarget_PrefersObstacleWithOneHitPointOverOneWithTwo()
        {
            BoardModel board = BoardFixture.From(@"
                t1 b2 t1
                t1 t1 t1
                t1 b2 t1");
            var damaged = new GridPos(1, 2);
            Assert.IsTrue(board.TryGetElement(damaged, out ElementInstance box));
            box.Health = 1;
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box2, 2) });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(damaged, target, "1 hp scores this turn, 2 hp does not, so y is not decisive");
        }

        [Test]
        public void PickGoalTarget_IgnoresDeadElement()
        {
            BoardModel board = BoardFixture.From(@"
                t1 bx t1
                t1 t1 t1
                t1 bx t1");
            Assert.IsTrue(board.TryGetElement(new GridPos(1, 0), out ElementInstance dead));
            dead.Health = 0;
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box1, 2) });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(new GridPos(1, 2), target);
        }

        [Test]
        public void PickGoalTarget_NeverCountsNotCountableElement()
        {
            BoardModel board = BoardFixture.From(@"
                t1 ## t1
                t1 t1 t1");
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Blocker, 1) });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreNotEqual(new GridPos(1, 1), target);
            Assert.IsTrue(board.GetSlot(target).IsChip, "the blocker serves nothing, so step 4 applies");
        }

        [Test]
        public void PickGoalTarget_ColoredBoxGoal_IgnoresPlainBox()
        {
            BoardModel board = BoardFixture.From(@"
                bx t1 c3
                t1 t1 t1");
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyAnyColoredBox(1) });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(new GridPos(2, 1), target);
        }

        [Test]
        public void PickGoalTarget_ActivateBoosterGoal_FallsThroughToTheNextUnclosedGoal()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t1
                t2 t1 t2");
            var goals = new GoalTracker(new[]
            {
                GoalDefinition.ActivateBooster(BoosterType.Bomb, 1),
                GoalDefinition.CollectColor(ChipColor.C2, 3)
            });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(new GridPos(0, 0), target, "no cell serves ActivateBooster");
        }

        [Test]
        public void PickGoalTarget_RandomFallbackIsReproducibleForAFixedSeed()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t3
                t3 t1 t2
                t2 t3 t1");
            var goals = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Bomb, 1) });

            GridPos first = NewService(board, goals).PickGoalTarget();
            GridPos second = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(first, second);
            Assert.IsTrue(board.GetSlot(first).IsChip);
        }

        [Test]
        public void PickGoalTarget_RandomFallbackDrawsOneIndexOverTheChipCellsOnly()
        {
            BoardModel board = BoardFixture.From(@"
                t1 rb
                bx t2");
            var goals = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Rainbow, 1) });
            var random = new RecordingRandom(new DeterministicRandom(Seed));

            GridPos target = new TargetingService(board, goals, random).PickGoalTarget();

            Assert.AreEqual(1, random.Log.Count);
            Assert.AreEqual(RecordingRandom.CallKind.NextIntMax, random.Log[0].Kind);
            Assert.AreEqual(2, random.Log[0].Arg0, "the booster cell and the box cell are not candidates");
            Assert.IsTrue(board.GetSlot(target).IsChip);
        }

        [Test]
        public void PickGoalTarget_ReturnsInvalidWhenBoardHoldsNoChip()
        {
            BoardModel board = BoardFixture.From(@"
                ## bx
                __ ##");
            var goals = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Bomb, 1) });

            GridPos target = NewService(board, goals).PickGoalTarget();

            Assert.AreEqual(GridPos.Invalid, target);
        }

        [Test]
        public void PickGoalTargets_ReturnsThreeDistinctCellsInPriorityOrder()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t1
                t2 t1 t2
                t1 t1 t2");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 9) });
            CellBuffer result = NewBuffer(board);

            int added = NewService(board, goals).PickGoalTargets(3, result);

            Assert.AreEqual(3, added);
            Assert.AreEqual(new GridPos(0, 0), result[0]);
            Assert.AreEqual(new GridPos(1, 0), result[1]);
            Assert.AreEqual(new GridPos(1, 1), result[2]);
        }

        [Test]
        public void PickGoalTargets_AppendsWithoutClearingTheResult()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t1
                t2 t1 t2
                t1 t1 t2");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 9) });
            CellBuffer result = NewBuffer(board);
            var kept = new GridPos(2, 2);
            result.Add(kept);

            int added = NewService(board, goals).PickGoalTargets(2, result);

            Assert.AreEqual(2, added);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(kept, result[0]);
        }

        [Test]
        public void PickGoalTargets_MovesToTheNextGoalWhenTheFirstRunsOutOfCells()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t2
                t2 t2 t2");
            var goals = new GoalTracker(new[]
            {
                GoalDefinition.CollectColor(ChipColor.C1, 5),
                GoalDefinition.CollectColor(ChipColor.C2, 5)
            });
            CellBuffer result = NewBuffer(board);

            int added = NewService(board, goals).PickGoalTargets(2, result);

            Assert.AreEqual(2, added);
            Assert.AreEqual(new GridPos(0, 1), result[0], "the only C1 chip");
            Assert.AreEqual(new GridPos(0, 0), result[1], "goal 1 is exhausted, so goal 2 is aimed at");
        }

        [Test]
        public void PickGoalTargets_ReturnsFewerWhenTheBoardRunsOutOfCells()
        {
            BoardModel board = BoardFixture.From(@"
                ## t1
                ## ##");
            var goals = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Bomb, 1) });
            CellBuffer result = NewBuffer(board);

            int added = NewService(board, goals).PickGoalTargets(3, result);

            Assert.AreEqual(1, added);
            Assert.AreEqual(new GridPos(1, 1), result[0]);
        }

        [Test]
        public void PickNeededColor_UsesFirstUnclosedCollectColorGoalInConfigOrder()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t1 t2
                t1 t2 t1");
            var goals = new GoalTracker(new[]
            {
                GoalDefinition.CollectColor(ChipColor.C2, 1),
                GoalDefinition.CollectColor(ChipColor.C5, 3)
            });
            goals.CreditChip(ChipColor.C2, _deltas);

            ChipColor color = NewService(board, goals).PickNeededColor();

            Assert.AreEqual(ChipColor.C5, color, "goal order wins over the chip counts on the board");
        }

        [Test]
        public void PickNeededColor_FallsBackToTheMostCommonChipColor()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t3 t3
                t2 t3 t1");
            var goals = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box1, 1) });

            ChipColor color = NewService(board, goals).PickNeededColor();

            Assert.AreEqual(ChipColor.C3, color);
        }

        [Test]
        public void PickNeededColor_ClosedCollectColorGoal_FallsBackToTheMostCommonChipColor()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t3 t3
                t2 t3 t1");
            var goals = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C6, 1) });
            goals.CreditChip(ChipColor.C6, _deltas);

            ChipColor color = NewService(board, goals).PickNeededColor();

            Assert.AreEqual(ChipColor.C3, color);
        }

        [Test]
        public void PickNeededColor_BreaksTieByLowestColorIndex()
        {
            BoardModel board = BoardFixture.From(@"
                t4 t2
                t2 t4");
            var goals = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Bomb, 1) });

            ChipColor color = NewService(board, goals).PickNeededColor();

            Assert.AreEqual(ChipColor.C2, color);
        }

        [Test]
        public void PickNeededColor_ReturnsNoneWhenBoardHoldsNoChip()
        {
            BoardModel board = BoardFixture.From(@"
                ## bx
                __ ##");
            var goals = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Bomb, 1) });

            ChipColor color = NewService(board, goals).PickNeededColor();

            Assert.AreEqual(ChipColor.None, color);
        }

        private static TargetingService NewService(BoardModel board, IGoalTracker goals)
            => new TargetingService(board, goals, new DeterministicRandom(Seed));

        private static CellBuffer NewBuffer(BoardModel board)
        {
            var buffer = new CellBuffer();
            buffer.Configure(board.Width, board.Height);
            return buffer;
        }
    }
}
