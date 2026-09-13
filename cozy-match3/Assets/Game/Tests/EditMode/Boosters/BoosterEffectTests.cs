using System;
using Match3.Board;
using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Boosters
{
    public sealed class BoosterEffectTests
    {
        private const int Seed = 20260909;

        private const string FiveByFive = @"
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1";

        private const string MixedThreeByThree = @"
            t1 t2 t1
            t2 rb t2
            t1 t1 t2";

        [Test]
        public void RocketH_ClearsTheWholeRowIncludingItsOwnCell()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t1 t1 t1
                t1 rh t1 t1
                t1 t1 t1 t1");
            CellBuffer hits = NewBuffer(board);

            Fire(new RocketEffect(BoosterType.RocketH), new GridPos(1, 1), BoosterType.RocketH, board, hits);

            Assert.AreEqual(4, hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.AreEqual(1, hits[i].Y);
            }

            Assert.IsTrue(hits.Contains(new GridPos(1, 1)), "the rocket clears its own cell too");
        }

        [Test]
        public void RocketV_ClearsTheWholeColumnIncludingItsOwnCell()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t1 t1 t1
                t1 rv t1 t1
                t1 t1 t1 t1");
            CellBuffer hits = NewBuffer(board);

            Fire(new RocketEffect(BoosterType.RocketV), new GridPos(1, 1), BoosterType.RocketV, board, hits);

            Assert.AreEqual(3, hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                Assert.AreEqual(1, hits[i].X);
            }
        }

        [Test]
        public void RocketH_PassesThroughObstaclesAndTheBlocker()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t1 t1 t1 t1
                rh bx ## b2 t1
                t1 t1 t1 t1 t1");
            CellBuffer hits = NewBuffer(board);

            Fire(new RocketEffect(BoosterType.RocketH), new GridPos(0, 1), BoosterType.RocketH, board, hits);

            Assert.AreEqual(5, hits.Count, "D11/E08: the line is never stopped");
            Assert.IsTrue(hits.Contains(new GridPos(4, 1)), "the cell behind the blocker is not skipped");
            Assert.IsTrue(hits.Contains(new GridPos(2, 1)), "the blocker cell itself is hit");
        }

        [Test]
        public void RocketH_NeverHitsAHole()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t1 t1
                rh __ t1");
            CellBuffer hits = NewBuffer(board);

            Fire(new RocketEffect(BoosterType.RocketH), new GridPos(0, 0), BoosterType.RocketH, board, hits);

            Assert.AreEqual(2, hits.Count);
            Assert.IsFalse(hits.Contains(new GridPos(1, 0)));
        }

        [Test]
        public void Rocket_AppendsWithoutDuplicatingACellTheBufferAlreadyHolds()
        {
            BoardModel board = BoardFixture.From(@"t1 rh t1");
            CellBuffer hits = NewBuffer(board);
            hits.Add(new GridPos(0, 0));

            Fire(new RocketEffect(BoosterType.RocketH), new GridPos(1, 0), BoosterType.RocketH, board, hits);

            Assert.AreEqual(3, hits.Count);
            Assert.AreEqual(new GridPos(0, 0), hits[0], "the buffer belongs to the caller and is not cleared");
        }

        [Test]
        public void Bomb_CoversTheFiveByFiveSquareAroundItsOwnCell()
        {
            BoardModel board = BoardFixture.From(FiveByFive);
            CellBuffer hits = NewBuffer(board);

            Fire(new BombEffect(), new GridPos(2, 2), BoosterType.Bomb, board, hits);

            Assert.AreEqual(25, hits.Count);
        }

        [Test]
        public void Bomb_IsClippedByTheBoardEdge()
        {
            BoardModel board = BoardFixture.From(FiveByFive);
            CellBuffer hits = NewBuffer(board);

            Fire(new BombEffect(), new GridPos(0, 0), BoosterType.Bomb, board, hits);

            Assert.AreEqual(9, hits.Count);
            Assert.IsFalse(hits.Contains(new GridPos(3, 0)), "the square reaches two cells, not further");
        }

        [Test]
        public void Bomb_NeverHitsAHole()
        {
            BoardModel board = BoardFixture.From(@"
                t1 __ t1
                t1 t1 t1
                t1 t1 t1");
            CellBuffer hits = NewBuffer(board);

            Fire(new BombEffect(), new GridPos(1, 1), BoosterType.Bomb, board, hits);

            Assert.AreEqual(8, hits.Count);
            Assert.IsFalse(hits.Contains(new GridPos(1, 2)));
        }

        [Test]
        public void Rainbow_HitsEveryChipOfTheSwappedColour()
        {
            BoardModel board = BoardFixture.From(MixedThreeByThree);
            CellBuffer hits = NewBuffer(board);
            IGoalTracker goals = Goals(GoalDefinition.CollectColor(ChipColor.C1, 5));

            Fire(NewRainbow(board, goals), new GridPos(1, 1), BoosterType.Rainbow, ChipColor.C2, board, hits);

            Assert.AreEqual(4, hits.Count);
            Assert.AreEqual(new GridPos(2, 0), hits[0], "cells are appended y-up then x-up");
            Assert.AreEqual(new GridPos(0, 1), hits[1]);
            Assert.AreEqual(new GridPos(2, 1), hits[2]);
            Assert.AreEqual(new GridPos(1, 2), hits[3]);
        }

        [Test]
        public void Rainbow_TapWithoutAHintUsesTheMostNeededColour()
        {
            BoardModel board = BoardFixture.From(MixedThreeByThree);
            CellBuffer hits = NewBuffer(board);
            IGoalTracker goals = Goals(GoalDefinition.CollectColor(ChipColor.C1, 5));

            Fire(NewRainbow(board, goals), new GridPos(1, 1), BoosterType.Rainbow, ChipColor.None, board, hits);

            Assert.AreEqual(4, hits.Count);
            Assert.IsTrue(hits.Contains(new GridPos(0, 0)), "the C1 goal is unclosed, so C1 is the needed colour");
            Assert.IsFalse(hits.Contains(new GridPos(2, 0)), "the C2 chip is not touched");
        }

        [Test]
        public void Rainbow_FallsBackToItsNeighbourhoodWhenNoChipHasTheColour()
        {
            BoardModel board = BoardFixture.From(MixedThreeByThree);
            CellBuffer hits = NewBuffer(board);
            IGoalTracker goals = Goals(GoalDefinition.CollectColor(ChipColor.C1, 5));

            Fire(NewRainbow(board, goals), new GridPos(1, 1), BoosterType.Rainbow, ChipColor.C6, board, hits);

            Assert.AreEqual(5, hits.Count, "E06: the ball is never wasted");
            Assert.AreEqual(new GridPos(1, 0), hits[0]);
            Assert.AreEqual(new GridPos(0, 1), hits[1]);
            Assert.AreEqual(new GridPos(1, 1), hits[2]);
            Assert.AreEqual(new GridPos(2, 1), hits[3]);
            Assert.AreEqual(new GridPos(1, 2), hits[4]);
        }

        [Test]
        public void Rainbow_FallsBackWhenTheBoardHoldsNoChipAtAll()
        {
            BoardModel board = BoardFixture.From(@"
                bx bx
                bx rb");
            CellBuffer hits = NewBuffer(board);
            IGoalTracker goals = Goals(GoalDefinition.DestroyElement(ElementTokens.Box1, 3));

            Fire(NewRainbow(board, goals), new GridPos(1, 0), BoosterType.Rainbow, ChipColor.None, board, hits);

            Assert.AreEqual(3, hits.Count, "E07: own cell plus the neighbours inside the board");
            Assert.AreEqual(new GridPos(0, 0), hits[0]);
            Assert.AreEqual(new GridPos(1, 0), hits[1]);
            Assert.AreEqual(new GridPos(1, 1), hits[2]);
        }

        [Test]
        public void Airplane_HitsTheTargetCellAndItsFourOrthogonalNeighbours()
        {
            BoardModel board = BoardFixture.From(@"
                t2 t2 t2
                t2 t1 t2
                t2 t2 t2");
            CellBuffer hits = NewBuffer(board);
            IGoalTracker goals = Goals(GoalDefinition.CollectColor(ChipColor.C1, 5));

            Fire(NewAirplane(board, goals), new GridPos(0, 0), BoosterType.Airplane, board, hits);

            Assert.AreEqual(5, hits.Count);
            Assert.AreEqual(new GridPos(1, 0), hits[0]);
            Assert.AreEqual(new GridPos(0, 1), hits[1]);
            Assert.AreEqual(new GridPos(1, 1), hits[2], "the only C1 chip is the target");
            Assert.AreEqual(new GridPos(2, 1), hits[3]);
            Assert.AreEqual(new GridPos(1, 2), hits[4]);
        }

        [Test]
        public void Airplane_ClipsTheImpactAreaAtTheBoardEdge()
        {
            BoardModel board = BoardFixture.From(@"
                t2 t2 t2
                t2 t2 t2
                t1 t2 t2");
            CellBuffer hits = NewBuffer(board);
            IGoalTracker goals = Goals(GoalDefinition.CollectColor(ChipColor.C1, 5));

            Fire(NewAirplane(board, goals), new GridPos(2, 2), BoosterType.Airplane, board, hits);

            Assert.AreEqual(3, hits.Count);
            Assert.AreEqual(new GridPos(0, 0), hits[0]);
            Assert.AreEqual(new GridPos(1, 0), hits[1]);
            Assert.AreEqual(new GridPos(0, 1), hits[2]);
        }

        [Test]
        public void Airplane_AddsNoCellWhenTheBoardHasNoTarget()
        {
            BoardModel board = BoardFixture.From(@"
                ## bx
                __ ##");
            CellBuffer hits = NewBuffer(board);
            IGoalTracker goals = Goals(GoalDefinition.ActivateBooster(BoosterType.Bomb, 1));

            Fire(NewAirplane(board, goals), new GridPos(1, 1), BoosterType.Airplane, board, hits);

            Assert.AreEqual(0, hits.Count);
        }

        [Test]
        public void BoosterCatalog_ReturnsTheEffectRegisteredForEachBoosterType()
        {
            BoardModel board = BoardFixture.From(MixedThreeByThree);
            IGoalTracker goals = Goals(GoalDefinition.CollectColor(ChipColor.C1, 5));
            ITargetingService targeting = NewTargeting(board, goals);
            var catalog = new BoosterCatalog(new IBoosterEffect[]
            {
                new RocketEffect(BoosterType.RocketH),
                new RocketEffect(BoosterType.RocketV),
                new BombEffect(),
                new RainbowEffect(targeting),
                new AirplaneEffect(targeting)
            });

            Assert.AreEqual(BoosterType.RocketH, catalog.Get(BoosterType.RocketH).Type);
            Assert.AreEqual(BoosterType.RocketV, catalog.Get(BoosterType.RocketV).Type);
            Assert.AreEqual(BoosterType.Bomb, catalog.Get(BoosterType.Bomb).Type);
            Assert.AreEqual(BoosterType.Rainbow, catalog.Get(BoosterType.Rainbow).Type);
            Assert.AreEqual(BoosterType.Airplane, catalog.Get(BoosterType.Airplane).Type);
        }

        [Test]
        public void BoosterCatalog_ThrowsForABoosterTypeWithoutAnEffect()
        {
            var catalog = new BoosterCatalog(new IBoosterEffect[] { new BombEffect() });

            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Get(BoosterType.Rainbow));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Get(BoosterType.None));
        }

        private static void Fire(
            IBoosterEffect effect,
            GridPos cell,
            BoosterType booster,
            IBoardReader board,
            CellBuffer hits)
            => Fire(effect, cell, booster, ChipColor.None, board, hits);

        private static void Fire(
            IBoosterEffect effect,
            GridPos cell,
            BoosterType booster,
            ChipColor colorHint,
            IBoardReader board,
            CellBuffer hits)
        {
            var activation = new BoosterActivation(cell, booster, colorHint);
            effect.Resolve(in activation, board, hits);
        }

        private static RainbowEffect NewRainbow(IBoardReader board, IGoalTracker goals)
            => new RainbowEffect(NewTargeting(board, goals));

        private static AirplaneEffect NewAirplane(IBoardReader board, IGoalTracker goals)
            => new AirplaneEffect(NewTargeting(board, goals));

        private static ITargetingService NewTargeting(IBoardReader board, IGoalTracker goals)
            => new TargetingService(board, goals, new DeterministicRandom(Seed));

        private static IGoalTracker Goals(GoalDefinition definition)
            => new GoalTracker(new[] { definition });

        private static CellBuffer NewBuffer(IBoardReader board)
        {
            var buffer = new CellBuffer();
            buffer.Configure(board.Width, board.Height);
            return buffer;
        }
    }
}
