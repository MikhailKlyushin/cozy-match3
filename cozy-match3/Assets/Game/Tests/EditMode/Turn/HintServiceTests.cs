using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Turn
{
    public sealed class HintServiceTests
    {
        /// <summary>Swapping (2,0) with (2,1) makes three C1; nothing better exists.</summary>
        private const string PlainLayout = @"
            t3 t4 t3 t4 t3
            t4 t3 t4 t3 t4
            t3 t4 t1 t4 t3
            t1 t1 t2 t3 t4";

        [Test]
        public void HintDisabled_ReturnsNoHint()
        {
            TurnFixture fixture = TurnFixture.Create(PlainLayout, hintDelaySeconds: 0f);

            Assert.IsFalse(fixture.TurnRule.GetHint().HasHint, "§5.5: 0 disables the hint");
        }

        [Test]
        public void PlainBoard_SuggestsALegalSwap()
        {
            TurnFixture fixture = TurnFixture.Create(PlainLayout);

            HintPlan hint = fixture.TurnRule.GetHint();

            Assert.IsTrue(hint.HasHint);
            Assert.AreEqual(HintKind.Swap, hint.Kind);
            Assert.IsTrue(hint.A.IsOrthogonalNeighbourOf(hint.B), "a hint is always a neighbour swap");
        }

        [Test]
        public void BoosterCreatingSwap_TakesPriorityOverAPlainOne()
        {
            // (3,1) completes a line of FOUR C1 in the bottom row - a rocket - while (0,3)/(1,3)
            // is only a plain three.
            TurnFixture fixture = TurnFixture.Create(@"
                t1 t2 t1 t4 t3
                t1 t3 t4 t3 t4
                t2 t4 t3 t1 t3
                t1 t1 t1 t2 t4");

            HintPlan hint = fixture.TurnRule.GetHint();

            Assert.IsTrue(hint.HasHint);
            Assert.AreEqual(1, hint.RulePriority, "§5.5 priority 1: a swap that creates a booster");
        }

        [Test]
        public void Hint_IsDeterministicAcrossCalls()
        {
            TurnFixture fixture = TurnFixture.Create(PlainLayout);

            HintPlan first = fixture.TurnRule.GetHint();
            HintPlan second = fixture.TurnRule.GetHint();

            Assert.AreEqual(first.A, second.A, "no RNG: the hint must be reproducible");
            Assert.AreEqual(first.B, second.B);
            Assert.AreEqual(first.RulePriority, second.RulePriority);
        }

        [Test]
        public void Hint_LeavesTheBoardUntouched()
        {
            TurnFixture fixture = TurnFixture.Create(PlainLayout);
            ulong before = fixture.Board.ComputeHash();

            fixture.TurnRule.GetHint();

            Assert.AreEqual(before, fixture.Board.ComputeHash(), "the probe swaps and swaps back");
        }

        [Test]
        public void BoosterPairSwap_IsAvoidedWhileAnythingElseExists()
        {
            // A rocket and a bomb sit side by side, and an ordinary three is also available.
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                rh bm t4 t3 t4
                t3 t4 t1 t4 t3
                t1 t1 t2 t3 t4");

            HintPlan hint = fixture.TurnRule.GetHint();

            Assert.IsTrue(hint.HasHint);
            bool isBoosterPair = hint.A == new GridPos(0, 2) && hint.B == new GridPos(1, 2);
            Assert.IsFalse(isBoosterPair, "D15: never advise spending an accumulated combination");
        }

        [Test]
        public void GoalAdvancingSwap_IsPreferredOverAnIndifferentOne()
        {
            // Swapping (2,0) with (2,1) makes three C3, which is exactly what the goal wants, and
            // three cells earn no booster, so priority 1 cannot pre-empt it.
            TurnFixture fixture = TurnFixture.Create(
                @"
                t2 t4 t2 t4 t2
                t4 t2 t4 t2 t4
                t2 t4 t3 t4 t2
                t3 t3 t1 t2 t4",
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C3, 10) });

            HintPlan hint = fixture.TurnRule.GetHint();

            Assert.IsTrue(hint.HasHint);
            Assert.LessOrEqual(hint.RulePriority, 2, "§5.5 priority 2 or better");
        }

        [Test]
        public void NoLegalSwapButABooster_FallsBackToATap()
        {
            // Chips are locked in by blockers, so only the booster tap remains (§5.5 priority 4).
            TurnFixture fixture = TurnFixture.Create(@"
                ## ## ##
                ## rh ##
                ## ## ##");

            HintPlan hint = fixture.TurnRule.GetHint();

            Assert.IsTrue(hint.HasHint);
            Assert.AreEqual(HintKind.TapBooster, hint.Kind);
            Assert.AreEqual(4, hint.RulePriority);
            Assert.AreEqual(new GridPos(1, 1), hint.A);
        }

        [Test]
        public void NoLegalMoveAndNoBooster_ReturnsNoHintAndTriggersNothing()
        {
            TurnFixture fixture = TurnFixture.Create(@"
                ## ## ##
                ## t1 ##
                ## ## ##");

            HintPlan hint = fixture.TurnRule.GetHint();

            Assert.IsFalse(hint.HasHint);
            Assert.IsEmpty(
                fixture.Logger.Errors,
                "§5.5: a missing hint never triggers a shuffle, so nothing is logged");
        }
    }
}
