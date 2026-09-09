using System;
using System.Collections.Generic;
using Match3.Boosters;
using Match3.Core;
using Match3.Goals;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Boosters
{
    public sealed class ComboMatrixTests
    {
        private const int Seed = 20260909;
        private const float Tolerance = 0.0001f;
        private const float RocketRainStep = 0.06f;
        private const float HeavyRainStep = 0.08f;

        private const string PlainFiveByFive = @"
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1";

        private const string PlainSevenBySeven = @"
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1";

        private const string HoledSevenBySeven = @"
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 __ t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1
            t1 t1 t1 t1 t1 t1 t1";

        /// <summary>Four C1 chips at (0,0), (1,1), (3,2), (1,3).</summary>
        private const string RainFiveByFive = @"
            t2 t2 t2 t2 t2
            t2 t1 t2 t2 t2
            t2 t2 t2 t1 t2
            t2 t1 t2 t2 t2
            t1 t2 t2 t2 t2";

        /// <summary>Two C1 chips at (0,0) and (1,1).</summary>
        private const string TwoTargets = @"
            t2 t2 t2
            t2 t1 t2
            t1 t2 t2";

        /// <summary>Three C1 chips at (0,0), (2,1) and (1,2).</summary>
        private const string ThreeTargets = @"
            t2 t1 t2
            t2 t2 t1
            t1 t2 t2";

        private const string SixteenChips = @"
            t1 t1 t1 t1
            t1 t1 t1 t1
            t1 t1 t1 t1
            t1 t1 t1 t1";

        [Test]
        public void IsCovered_EveryPairOfTheMatrixIsFilled()
        {
            var types = new[]
            {
                BoosterType.RocketH,
                BoosterType.RocketV,
                BoosterType.Bomb,
                BoosterType.Rainbow,
                BoosterType.Airplane
            };

            for (int i = 0; i < types.Length; i++)
            {
                for (int j = 0; j < types.Length; j++)
                {
                    Assert.IsTrue(ComboMatrix.IsCovered(types[i], types[j]), types[i] + " + " + types[j]);
                }
            }
        }

        [Test]
        public void IsCovered_ReturnsFalseWhenOneSideIsNotABooster()
        {
            Assert.IsFalse(ComboMatrix.IsCovered(BoosterType.None, BoosterType.Bomb));
            Assert.IsFalse(ComboMatrix.IsCovered(BoosterType.Bomb, BoosterType.None));
        }

        [Test]
        public void RocketPlusRocket_FiresAFullRowAndAFullColumnThroughTheEpicentre()
        {
            ComboResolver resolver = NewResolver(PlainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.RocketH,
                new GridPos(1, 2),
                BoosterType.RocketV);

            Assert.AreEqual(new GridPos(2, 2), plan.Epicentre);
            Assert.AreEqual(BoosterType.RocketH, plan.A);
            Assert.AreEqual(BoosterType.RocketV, plan.B);
            Assert.AreEqual(2, plan.Steps.Count);
            AssertStep(plan.Steps[0], new GridPos(2, 2), BoosterType.RocketH, 0f, false);
            AssertStep(plan.Steps[1], new GridPos(2, 2), BoosterType.RocketV, 0f, false);
            Assert.AreEqual(0, plan.DirectCells.Count);
            Assert.IsFalse(plan.DamagesEveryObstacle);
        }

        [Test]
        public void RocketPlusBomb_FiresAThickCrossOfThreeRowsAndThreeColumns()
        {
            ComboResolver resolver = NewResolver(PlainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.RocketV,
                new GridPos(1, 2),
                BoosterType.Bomb);

            Assert.AreEqual(BoosterType.RocketV, plan.A, "the plan keeps the raw pair; only the lookup normalises");
            Assert.AreEqual(6, plan.Steps.Count);
            AssertStep(plan.Steps[0], new GridPos(2, 1), BoosterType.RocketH, 0f, false);
            AssertStep(plan.Steps[1], new GridPos(2, 2), BoosterType.RocketH, 0f, false);
            AssertStep(plan.Steps[2], new GridPos(2, 3), BoosterType.RocketH, 0f, false);
            AssertStep(plan.Steps[3], new GridPos(1, 2), BoosterType.RocketV, 0f, false);
            AssertStep(plan.Steps[4], new GridPos(2, 2), BoosterType.RocketV, 0f, false);
            AssertStep(plan.Steps[5], new GridPos(3, 2), BoosterType.RocketV, 0f, false);
        }

        [Test]
        public void BombPlusBomb_DestroysOneSevenBySevenBlast()
        {
            ComboResolver resolver = NewResolver(PlainSevenBySeven, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(3, 3),
                BoosterType.Bomb,
                new GridPos(2, 3),
                BoosterType.Bomb);

            Assert.AreEqual(0, plan.Steps.Count, "one blast, not nine bombs");
            Assert.AreEqual(49, plan.DirectCells.Count);
            Assert.IsFalse(plan.DamagesEveryObstacle);
        }

        [Test]
        public void BombPlusBomb_CentresOnTheSwapTargetCellAndClipsAtTheEdge()
        {
            ComboResolver resolver = NewResolver(HoledSevenBySeven, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(5, 5),
                BoosterType.Bomb,
                new GridPos(4, 5),
                BoosterType.Bomb);

            Assert.AreEqual(new GridPos(5, 5), plan.Epicentre);
            Assert.AreEqual(24, plan.DirectCells.Count, "the 7x7 blast is clipped to 5x5 here, minus the hole");
            Assert.IsTrue(Contains(plan.DirectCells, new GridPos(2, 2)));
            Assert.IsFalse(
                Contains(plan.DirectCells, new GridPos(1, 5)),
                "the blast is centred on the target cell, not on the source cell");
            Assert.IsFalse(Contains(plan.DirectCells, new GridPos(3, 3)), "a hole is never destroyed");
        }

        [Test]
        public void RocketPlusRainbow_TurnsEveryChipOfTheNeededColourIntoARocket()
        {
            ComboResolver resolver = NewResolver(RainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.RocketH,
                new GridPos(1, 2),
                BoosterType.Rainbow);

            Assert.AreEqual(4, plan.Steps.Count);
            AssertStep(plan.Steps[0], new GridPos(0, 0), BoosterType.RocketH, RocketRainStep, true);
            AssertStep(plan.Steps[1], new GridPos(1, 1), BoosterType.RocketV, RocketRainStep, true);
            AssertStep(plan.Steps[2], new GridPos(3, 2), BoosterType.RocketH, RocketRainStep, true);
            AssertStep(plan.Steps[3], new GridPos(1, 3), BoosterType.RocketV, RocketRainStep, true);
            Assert.AreEqual(0, plan.DirectCells.Count);
        }

        [Test]
        public void RocketPlusRainbow_AlternatesTheRocketOrientationAlongTheYUpXUpOrder()
        {
            ComboResolver resolver = NewResolver(RainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.Rainbow,
                new GridPos(1, 2),
                BoosterType.RocketV);

            Assert.AreEqual(4, plan.Steps.Count);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                BoosterType expected = (i & 1) == 0 ? BoosterType.RocketH : BoosterType.RocketV;
                Assert.AreEqual(expected, plan.Steps[i].Booster, "even index is horizontal");
            }
        }

        [Test]
        public void BombPlusRainbow_TurnsEveryChipOfTheNeededColourIntoABomb()
        {
            ComboResolver resolver = NewResolver(RainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.Bomb,
                new GridPos(1, 2),
                BoosterType.Rainbow);

            Assert.AreEqual(4, plan.Steps.Count);
            AssertStep(plan.Steps[0], new GridPos(0, 0), BoosterType.Bomb, HeavyRainStep, true);
            AssertStep(plan.Steps[1], new GridPos(1, 1), BoosterType.Bomb, HeavyRainStep, true);
            AssertStep(plan.Steps[2], new GridPos(3, 2), BoosterType.Bomb, HeavyRainStep, true);
            AssertStep(plan.Steps[3], new GridPos(1, 3), BoosterType.Bomb, HeavyRainStep, true);
        }

        [Test]
        public void RainbowPlusRainbow_DestroysEveryChipAndDamagesEveryObstacle()
        {
            ComboResolver resolver = NewResolver(
                @"
                t1 bx t2
                t2 ## t1
                t1 t2 t3",
                GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 1),
                BoosterType.Rainbow,
                new GridPos(2, 0),
                BoosterType.Rainbow);

            Assert.IsTrue(plan.DamagesEveryObstacle);
            Assert.AreEqual(0, plan.Steps.Count);
            Assert.AreEqual(7, plan.DirectCells.Count, "every chip on the board, obstacle cells excluded");
            Assert.AreEqual(new GridPos(0, 0), plan.DirectCells[0]);
            Assert.IsFalse(Contains(plan.DirectCells, new GridPos(1, 1)), "the blocker holds no chip");
            Assert.IsFalse(Contains(plan.DirectCells, new GridPos(1, 2)), "the box holds no chip");
        }

        [Test]
        public void RocketPlusAirplane_SendsTwoAirplanesAndFiresARocketAtEachImpact()
        {
            ComboResolver resolver = NewResolver(TwoTargets, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.RocketH,
                new GridPos(1, 2),
                BoosterType.Airplane);

            Assert.AreEqual(4, plan.Steps.Count);
            AssertStep(plan.Steps[0], new GridPos(0, 0), BoosterType.Airplane, 0f, false);
            AssertStep(plan.Steps[1], new GridPos(0, 0), BoosterType.RocketH, 0f, true);
            AssertStep(plan.Steps[2], new GridPos(1, 1), BoosterType.Airplane, 0f, false);
            AssertStep(plan.Steps[3], new GridPos(1, 1), BoosterType.RocketV, 0f, true);
        }

        [Test]
        public void BombPlusAirplane_SendsTwoAirplanesEachDeliveringABlast()
        {
            ComboResolver resolver = NewResolver(TwoTargets, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.Bomb,
                new GridPos(1, 2),
                BoosterType.Airplane);

            Assert.AreEqual(4, plan.Steps.Count);
            AssertStep(plan.Steps[0], new GridPos(0, 0), BoosterType.Airplane, 0f, false);
            AssertStep(plan.Steps[1], new GridPos(0, 0), BoosterType.Bomb, 0f, true);
            AssertStep(plan.Steps[2], new GridPos(1, 1), BoosterType.Airplane, 0f, false);
            AssertStep(plan.Steps[3], new GridPos(1, 1), BoosterType.Bomb, 0f, true);
        }

        [Test]
        public void RainbowPlusAirplane_TurnsTheFirstEightChipsIntoAirplanesAndDestroysTheRest()
        {
            ComboResolver resolver = NewResolver(SixteenChips, GoalDefinition.CollectColor(ChipColor.C1, 20));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.Rainbow,
                new GridPos(1, 2),
                BoosterType.Airplane);

            Assert.AreEqual(8, plan.Steps.Count, "cap 8 airplanes");
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var expected = new GridPos(i % 4, i / 4);
                AssertStep(plan.Steps[i], expected, BoosterType.Airplane, HeavyRainStep, true);
            }

            Assert.AreEqual(8, plan.DirectCells.Count, "the chips past the cap are simply destroyed");
            Assert.AreEqual(new GridPos(0, 2), plan.DirectCells[0]);
            Assert.AreEqual(new GridPos(3, 3), plan.DirectCells[7]);
        }

        [Test]
        public void AirplanePlusAirplane_SendsThreeAirplanesToTheFirstThreeGoalTargets()
        {
            ComboResolver resolver = NewResolver(ThreeTargets, GoalDefinition.CollectColor(ChipColor.C1, 9));

            ComboPlan plan = resolver.Resolve(
                new GridPos(2, 2),
                BoosterType.Airplane,
                new GridPos(1, 2),
                BoosterType.Airplane);

            Assert.AreEqual(3, plan.Steps.Count);
            AssertStep(plan.Steps[0], new GridPos(0, 0), BoosterType.Airplane, 0f, false);
            AssertStep(plan.Steps[1], new GridPos(2, 1), BoosterType.Airplane, 0f, false);
            AssertStep(plan.Steps[2], new GridPos(1, 2), BoosterType.Airplane, 0f, false);
        }

        [Test]
        public void Resolve_IsSymmetricInThePairOrderAndInRocketOrientation()
        {
            ComboResolver resolver = NewResolver(RainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));
            var epicentre = new GridPos(2, 2);
            var other = new GridPos(1, 2);

            AssertSameSteps(
                resolver.Resolve(epicentre, BoosterType.Bomb, other, BoosterType.Rainbow),
                resolver.Resolve(epicentre, BoosterType.Rainbow, other, BoosterType.Bomb));
            AssertSameSteps(
                resolver.Resolve(epicentre, BoosterType.RocketH, other, BoosterType.Bomb),
                resolver.Resolve(epicentre, BoosterType.RocketV, other, BoosterType.Bomb));
        }

        [Test]
        public void MassTransformations_NeverDetonateAtOnce()
        {
            ComboResolver rain = NewResolver(RainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));
            var epicentre = new GridPos(2, 2);
            var other = new GridPos(1, 2);

            AssertStaggered(rain.Resolve(epicentre, BoosterType.RocketH, other, BoosterType.Rainbow));
            AssertStaggered(rain.Resolve(epicentre, BoosterType.Bomb, other, BoosterType.Rainbow));

            ComboResolver capped = NewResolver(SixteenChips, GoalDefinition.CollectColor(ChipColor.C1, 20));
            AssertStaggered(capped.Resolve(epicentre, BoosterType.Rainbow, other, BoosterType.Airplane));
        }

        [Test]
        public void Resolve_ThrowsWhenThePairIsOutsideTheMatrix()
        {
            ComboResolver resolver = NewResolver(PlainFiveByFive, GoalDefinition.CollectColor(ChipColor.C1, 9));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => resolver.Resolve(new GridPos(2, 2), BoosterType.None, new GridPos(1, 2), BoosterType.Bomb));
        }

        private static ComboResolver NewResolver(string layout, GoalDefinition goal)
        {
            BoardModel board = BoardFixture.From(layout);
            IGoalTracker goals = new GoalTracker(new[] { goal });
            return new ComboResolver(board, new TargetingService(board, goals, new DeterministicRandom(Seed)));
        }

        private static void AssertStep(
            ComboStep step,
            GridPos cell,
            BoosterType booster,
            float delay,
            bool transformFirst)
        {
            Assert.AreEqual(cell, step.Cell);
            Assert.AreEqual(booster, step.Booster);
            Assert.AreEqual(delay, step.Delay, Tolerance);
            Assert.AreEqual(transformFirst, step.TransformFirst);
        }

        private static void AssertSameSteps(ComboPlan first, ComboPlan second)
        {
            Assert.AreEqual(first.Steps.Count, second.Steps.Count);
            for (int i = 0; i < first.Steps.Count; i++)
            {
                ComboStep expected = first.Steps[i];
                AssertStep(second.Steps[i], expected.Cell, expected.Booster, expected.Delay, expected.TransformFirst);
            }
        }

        private static void AssertStaggered(ComboPlan plan)
        {
            Assert.Greater(plan.Steps.Count, 0);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                float delay = plan.Steps[i].Delay;
                Assert.IsTrue(delay >= RocketRainStep - Tolerance, "a mass transformation is staggered in time");
                Assert.IsTrue(delay <= HeavyRainStep + Tolerance, "the stagger stays inside 0.06-0.08 s");
            }
        }

        private static bool Contains(IReadOnlyList<GridPos> cells, GridPos cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
