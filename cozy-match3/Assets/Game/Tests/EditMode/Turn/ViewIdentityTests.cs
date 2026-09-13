using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using Match3.Tests.EditMode.Support;
using NUnit.Framework;
using BoardModel = Match3.Board.Board;

namespace Match3.Tests.EditMode.Turn
{
    /// <summary>
    /// Whatever replaces a slot must either keep the identity that was there or destroy it first.
    /// The view maps events by InstanceId (§6, rules T5/V1), so a fresh id dropped over a live chip
    /// leaves that chip on screen with nothing left to remove it - two chips drawn in one cell.
    /// </summary>
    public sealed class ViewIdentityTests
    {
        private const string Layout = @"
            t3 t4 t3 t4 t3
            t4 t3 t4 t3 t4
            t3 t4 t1 t4 t3
            t1 t1 t2 t3 t4";

        private const string EmptyCellLayout = @"
            t3 t4 t3 t4 t3
            t4 t3 t4 t3 t4
            t3 t4 t1 t4 t3
            .. t1 t2 t3 t4";

        [Test]
        public void PlaceBoosterCheat_KeepsTheIdentityOfTheChipItReplaces()
        {
            TurnFixture fixture = TurnFixture.Create(Layout, moveLimit: 10);
            var cell = new GridPos(4, 3);
            int before = fixture.Board.GetSlot(cell).InstanceId;
            var view = new TranscriptViewSimulator(fixture.Board);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(
                CheatCommand.PlaceBooster(cell, BoosterType.Bomb));
            view.Apply(transcript);

            Assert.AreEqual(BoosterType.Bomb, fixture.Board.GetSlot(cell).Booster);
            Assert.AreEqual(
                before,
                fixture.Board.GetSlot(cell).InstanceId,
                "§12: the chip becomes the booster, so the view morphs it instead of stacking a "
                + "second chip on top of one nothing can remove");
            AssertViewMatchesBoard(view, fixture.Board);
        }

        [Test]
        public void PlaceBoosterCheat_OnAnEmptyCell_SpawnsAFreshIdentity()
        {
            TurnFixture fixture = TurnFixture.Create(EmptyCellLayout, moveLimit: 10);
            var cell = new GridPos(0, 0);
            var view = new TranscriptViewSimulator(fixture.Board);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(
                CheatCommand.PlaceBooster(cell, BoosterType.Rainbow));
            view.Apply(transcript);

            Assert.AreNotEqual(0, fixture.Board.GetSlot(cell).InstanceId);
            AssertViewMatchesBoard(view, fixture.Board);
        }

        [Test]
        public void LeftoverMoveBonus_TurnsChipsIntoRocketsKeepingTheirIdentities()
        {
            TurnFixture fixture = TurnFixture.Create(
                Layout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 15) },
                moveLimit: 6);
            var view = new TranscriptViewSimulator(fixture.Board);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(CheatCommand.WinLevel());
            view.Apply(transcript);

            Assert.Greater(
                TurnFixture.CountOf(transcript, TurnEventKind.MovesBonusRocket),
                0,
                "§8.4: every leftover move becomes a rocket");
            AssertViewMatchesBoard(view, fixture.Board);
        }

        [Test]
        public void ASwapWithACascade_LeavesTheViewHoldingExactlyTheBoard()
        {
            TurnFixture fixture = TurnFixture.Create(Layout, moveLimit: 10);
            var view = new TranscriptViewSimulator(fixture.Board);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(2, 0), new GridPos(2, 1));
            view.Apply(transcript);

            Assert.AreEqual(TurnOutcome.Resolved, transcript.Outcome);
            AssertViewMatchesBoard(view, fixture.Board);
        }

        private static void AssertViewMatchesBoard(TranscriptViewSimulator view, BoardModel board)
        {
            if (view.TryFindMismatch(board, out string message))
            {
                Assert.Fail(message);
            }
        }
    }
}
