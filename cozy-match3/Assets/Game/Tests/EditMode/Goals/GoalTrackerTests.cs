using System;
using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Goals
{
    public sealed class GoalTrackerTests
    {
        private PooledList<GoalDelta> _deltas;
        private ElementCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _deltas = new PooledList<GoalDelta>();
            _catalog = BuiltInElementCatalog.Create();
        }

        [Test]
        public void CollectColor_CountsChipsOfItsColorOnly()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C2, 3) });

            tracker.CreditChip(ChipColor.C2, _deltas);
            tracker.CreditChip(ChipColor.C5, _deltas);

            Assert.AreEqual(1, tracker.GetState(0).Value);
            Assert.IsFalse(tracker.GetState(0).Closed);
            Assert.AreEqual(1, _deltas.Count);
            Assert.AreEqual(0, _deltas[0].GoalIndex);
            Assert.AreEqual(1, _deltas[0].Delta);
        }

        [Test]
        public void DestroyElement_CountsMatchingTokenOnly()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box1, 2) });

            tracker.CreditElement(Definition(ElementTokens.Box1), _deltas);
            tracker.CreditElement(Definition(ElementTokens.Box2), _deltas);

            Assert.AreEqual(1, tracker.GetState(0).Value);
            Assert.AreEqual(1, _deltas.Count);
        }

        [Test]
        public void CreditElement_CountsAtZeroHealthOnly_NotPartialDamage()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t1
                b3 t3 t2
                t2 t1 t3");
            var cell = new GridPos(0, 1);
            var tracker = new GoalTracker(new[] { GoalDefinition.DestroyElement(ElementTokens.Box3, 1) });

            Assert.IsTrue(board.TryGetElement(cell, out ElementInstance box));
            ElementDefinition definition = board.GetDefinition(cell);

            for (int damage = 0; damage < 3; damage++)
            {
                box.Health -= 1;
                if (box.Health == 0)
                {
                    tracker.CreditElement(definition, _deltas);
                }
                else
                {
                    Assert.AreEqual(0, tracker.GetState(0).Value);
                }
            }

            Assert.AreEqual(1, tracker.GetState(0).Value);
            Assert.AreEqual(1, _deltas.Count);
        }

        [Test]
        public void DestroyAnyColoredBox_CountsEveryFixedColoredBox()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.DestroyAnyColoredBox(ChipColors.MaxColorCount) });

            for (int colorIndex = 1; colorIndex <= ChipColors.MaxColorCount; colorIndex++)
            {
                tracker.CreditElement(Definition(ElementTokens.ColoredBox(colorIndex)), _deltas);
            }

            Assert.AreEqual(ChipColors.MaxColorCount, tracker.GetState(0).Value);
            Assert.IsTrue(tracker.GetState(0).Closed);
        }

        [Test]
        public void DestroyAnyColoredBox_CountsTheCyclingBox()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.DestroyAnyColoredBox(2) });

            tracker.CreditElement(Definition(ElementTokens.ColoredBoxCycling), _deltas);

            Assert.AreEqual(1, tracker.GetState(0).Value);
        }

        [Test]
        public void DestroyAnyColoredBox_RejectsPlainBoxes()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.DestroyAnyColoredBox(3) });

            tracker.CreditElement(Definition(ElementTokens.Box1), _deltas);
            tracker.CreditElement(Definition(ElementTokens.Box2), _deltas);
            tracker.CreditElement(Definition(ElementTokens.Box3), _deltas);

            Assert.AreEqual(0, tracker.GetState(0).Value);
            Assert.AreEqual(0, _deltas.Count);
        }

        [Test]
        public void NotCountableElement_NeverCountsForAnyGoal()
        {
            var tracker = new GoalTracker(new[]
            {
                GoalDefinition.DestroyElement(ElementTokens.Blocker, 1),
                GoalDefinition.DestroyAnyColoredBox(1)
            });

            tracker.CreditElement(Definition(ElementTokens.Blocker), _deltas);

            Assert.AreEqual(0, tracker.GetState(0).Value);
            Assert.AreEqual(0, tracker.GetState(1).Value);
            Assert.AreEqual(0, _deltas.Count);
        }

        [Test]
        public void CreditElement_AdvancesEveryMatchingGoal_InConfigOrder()
        {
            string token = ElementTokens.ColoredBox(1);
            var tracker = new GoalTracker(new[]
            {
                GoalDefinition.DestroyElement(token, 2),
                GoalDefinition.DestroyAnyColoredBox(2)
            });

            tracker.CreditElement(Definition(token), _deltas);

            Assert.AreEqual(2, _deltas.Count);
            Assert.AreEqual(0, _deltas[0].GoalIndex);
            Assert.AreEqual(1, _deltas[1].GoalIndex);
            Assert.AreEqual(1, tracker.GetState(0).Value);
            Assert.AreEqual(1, tracker.GetState(1).Value);
        }

        [Test]
        public void ActivateBooster_CountsChainLinksLikePlayerActivations()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Bomb, 3) });

            tracker.CreditBoosterActivation(BoosterType.Bomb, _deltas);
            tracker.CreditBoosterActivation(BoosterType.Bomb, _deltas);
            tracker.CreditBoosterActivation(BoosterType.Bomb, _deltas);

            Assert.AreEqual(3, tracker.GetState(0).Value);
            Assert.IsTrue(tracker.GetState(0).Closed);
        }

        [Test]
        public void ActivateBooster_IgnoresBoosterThatWasCreatedButNeverFired()
        {
            BoardModel board = BoardFixture.From(@"
                t1 t2 t1
                rh t3 t2
                t2 t1 t3");
            var tracker = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.RocketH, 2) });

            Assert.AreEqual(BoosterType.RocketH, board.GetSlot(new GridPos(0, 1)).Booster);
            Assert.AreEqual(0, tracker.GetState(0).Value);

            tracker.CreditBoosterActivation(BoosterType.RocketH, _deltas);

            Assert.AreEqual(1, tracker.GetState(0).Value);
        }

        [Test]
        public void ActivateBooster_CombinationCountsOneActivationPerParticipant()
        {
            var tracker = new GoalTracker(new[]
            {
                GoalDefinition.ActivateBooster(BoosterType.RocketH, 3),
                GoalDefinition.ActivateBooster(BoosterType.Bomb, 1)
            });

            tracker.CreditBoosterActivation(BoosterType.RocketH, _deltas);
            tracker.CreditBoosterActivation(BoosterType.Bomb, _deltas);

            Assert.AreEqual(1, tracker.GetState(0).Value);
            Assert.AreEqual(1, tracker.GetState(1).Value);
            Assert.AreEqual(2, _deltas.Count);
        }

        [Test]
        public void ActivateBooster_IgnoresOtherBoosterTypes()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.Bomb, 2) });

            tracker.CreditBoosterActivation(BoosterType.Rainbow, _deltas);
            tracker.CreditBoosterActivation(BoosterType.Airplane, _deltas);

            Assert.AreEqual(0, tracker.GetState(0).Value);
            Assert.AreEqual(0, _deltas.Count);
        }

        [Test]
        public void ActivateBooster_RocketGoal_CountsBothOrientations()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.ActivateBooster(BoosterType.RocketH, 2) });

            tracker.CreditBoosterActivation(BoosterType.RocketV, _deltas);
            tracker.CreditBoosterActivation(BoosterType.RocketH, _deltas);

            Assert.AreEqual(2, tracker.GetState(0).Value);
        }

        [Test]
        public void Credit_BeyondTarget_DiscardsOverflow()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C1, 12) });

            for (int i = 0; i < 14; i++)
            {
                tracker.CreditChip(ChipColor.C1, _deltas);
            }

            Assert.AreEqual(12, tracker.GetState(0).Value);
            Assert.IsTrue(tracker.GetState(0).Closed);
            Assert.AreEqual(12, SumDeltas(_deltas));
            Assert.AreEqual(12, _deltas[13].NewValue);
        }

        [Test]
        public void Credit_OnClosedGoal_ReportsZeroDelta()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C3, 1) });
            tracker.CreditChip(ChipColor.C3, _deltas);
            _deltas.Clear();

            tracker.CreditChip(ChipColor.C3, _deltas);

            Assert.AreEqual(1, _deltas.Count);
            Assert.AreEqual(0, _deltas[0].Delta);
            Assert.AreEqual(1, _deltas[0].NewValue);
            Assert.AreEqual(1, tracker.GetState(0).Value);
        }

        [Test]
        public void Credit_AppendsToCallerBuffer_WithoutClearingIt()
        {
            var tracker = new GoalTracker(new[] { GoalDefinition.CollectColor(ChipColor.C4, 5) });
            _deltas.Add(new GoalDelta(-1, 0, 0));

            tracker.CreditChip(ChipColor.C4, _deltas);
            tracker.CreditChip(ChipColor.C4, _deltas);

            Assert.AreEqual(3, _deltas.Count);
            Assert.AreEqual(-1, _deltas[0].GoalIndex);
            Assert.AreEqual(1, _deltas[1].NewValue);
            Assert.AreEqual(2, _deltas[2].NewValue);
        }

        [Test]
        public void FirstUnclosedIndex_FollowsConfigOrder_AndNeverSorts()
        {
            var tracker = new GoalTracker(new[]
            {
                GoalDefinition.CollectColor(ChipColor.C1, 10),
                GoalDefinition.DestroyElement(ElementTokens.Box1, 1)
            });

            Assert.AreEqual(0, tracker.FirstUnclosedIndex);

            tracker.CreditElement(Definition(ElementTokens.Box1), _deltas);

            Assert.AreEqual(0, tracker.FirstUnclosedIndex);
            Assert.IsTrue(tracker.GetState(1).Closed);
        }

        [Test]
        public void FirstUnclosedIndex_IsMinusOne_WhenEveryGoalIsClosed()
        {
            var tracker = new GoalTracker(new[]
            {
                GoalDefinition.CollectColor(ChipColor.C1, 1),
                GoalDefinition.ActivateBooster(BoosterType.Airplane, 1)
            });

            tracker.CreditChip(ChipColor.C1, _deltas);

            Assert.AreEqual(1, tracker.FirstUnclosedIndex);
            Assert.IsFalse(tracker.AllClosed);

            tracker.CreditBoosterActivation(BoosterType.Airplane, _deltas);

            Assert.AreEqual(-1, tracker.FirstUnclosedIndex);
            Assert.IsTrue(tracker.AllClosed);
        }

        [Test]
        public void CloseAll_ClosesEveryGoalAndReportsTheDeltas()
        {
            var tracker = new GoalTracker(new[]
            {
                GoalDefinition.CollectColor(ChipColor.C1, 4),
                GoalDefinition.DestroyElement(ElementTokens.Box1, 2)
            });
            tracker.CreditChip(ChipColor.C1, _deltas);
            _deltas.Clear();

            tracker.CloseAll(_deltas);

            Assert.IsTrue(tracker.AllClosed);
            Assert.AreEqual(4, tracker.GetState(0).Value);
            Assert.AreEqual(2, tracker.GetState(1).Value);
            Assert.AreEqual(2, _deltas.Count);
            Assert.AreEqual(3, _deltas[0].Delta);
            Assert.AreEqual(2, _deltas[1].Delta);
        }

        [Test]
        public void GetDefinition_ReturnsGoalsInConfigOrder()
        {
            var tracker = new GoalTracker(new[]
            {
                GoalDefinition.DestroyElement(ElementTokens.Box2, 4),
                GoalDefinition.CollectColor(ChipColor.C2, 10)
            });

            Assert.AreEqual(2, tracker.Count);
            Assert.AreEqual(GoalType.DestroyElement, tracker.GetDefinition(0).Type);
            Assert.AreEqual(ElementTokens.Box2, tracker.GetDefinition(0).Token);
            Assert.AreEqual(4, tracker.GetDefinition(0).Target);
            Assert.AreEqual(GoalType.CollectColor, tracker.GetDefinition(1).Type);
            Assert.AreEqual(ChipColor.C2, tracker.GetDefinition(1).Color);
            Assert.AreEqual(10, tracker.GetDefinition(1).Target);
        }

        [Test]
        public void EmptyTracker_HasNoUnclosedGoals()
        {
            var tracker = new GoalTracker(Array.Empty<GoalDefinition>());

            Assert.AreEqual(0, tracker.Count);
            Assert.AreEqual(-1, tracker.FirstUnclosedIndex);
            Assert.IsTrue(tracker.AllClosed);
        }

        [Test]
        public void GoalDefinition_RejectsNonPositiveTarget()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GoalDefinition.CollectColor(ChipColor.C1, 0));
        }

        [Test]
        public void GoalTracker_RejectsUnconfiguredGoal()
        {
            Assert.Throws<ArgumentException>(() => new GoalTracker(new[] { default(GoalDefinition) }));
        }

        private static int SumDeltas(PooledList<GoalDelta> deltas)
        {
            int sum = 0;
            for (int i = 0; i < deltas.Count; i++)
            {
                sum += deltas[i].Delta;
            }

            return sum;
        }

        private ElementDefinition Definition(string token)
        {
            Assert.IsTrue(_catalog.TryResolve(token, out ElementDefinition definition), token);
            return definition;
        }
    }
}
