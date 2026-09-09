using Match3.Board;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Turn
{
    public sealed class TurnRuleTests
    {
        /// <summary>No ready match; swapping (2,0) with (2,1) completes three C1 in the bottom row.</summary>
        private const string OneSwapLayout = @"
            t3 t4 t3 t4 t3
            t4 t3 t4 t3 t4
            t3 t4 t1 t4 t3
            t1 t1 t2 t3 t4";

        private static readonly GridPos SwapFrom = new GridPos(2, 0);
        private static readonly GridPos SwapTo = new GridPos(2, 1);

        [Test]
        public void MoveIsCharged_AtCommitBeforeAnyStep()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            int swapIndex = TurnFixture.IndexOf(transcript, TurnEventKind.SwapPerformed);
            int chargeIndex = TurnFixture.IndexOf(transcript, TurnEventKind.MoveCharged);
            int firstStep = TurnFixture.IndexOf(transcript, TurnEventKind.StepBegin);

            Assert.AreEqual(swapIndex + 1, chargeIndex, "D04: the move is charged right after COMMIT");
            Assert.Less(chargeIndex, firstStep, "E15: charged before resolution begins");
            Assert.AreEqual(9, fixture.TurnRule.MovesLeft);
        }

        [Test]
        public void IllegalSwap_ChargesNothingAndLeavesTheBoard()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);
            ulong before = fixture.Board.ComputeHash();

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(0, 3), new GridPos(1, 3));

            Assert.AreEqual(TurnOutcome.Rejected, transcript.Outcome);
            Assert.AreNotEqual(-1, TurnFixture.IndexOf(transcript, TurnEventKind.SwapRejected));
            Assert.AreEqual(-1, TurnFixture.IndexOf(transcript, TurnEventKind.MoveCharged), "E16");
            Assert.AreEqual(10, fixture.TurnRule.MovesLeft);
            Assert.AreEqual(before, fixture.Board.ComputeHash());
        }

        [Test]
        public void TapOnABooster_ActivatesItInItsOwnCell()
        {
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t3 t4 t3
                rh t2 t3 t4 t2", moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteTap(new GridPos(0, 0));

            int index = TurnFixture.IndexOf(transcript, TurnEventKind.BoosterActivated);
            Assert.AreNotEqual(-1, index);
            Assert.AreEqual(new GridPos(0, 0), transcript.GetEvent(index).A);
            Assert.AreEqual(BoosterActivationSource.Player, transcript.GetEvent(index).ActivationSource);
            Assert.AreEqual(9, fixture.TurnRule.MovesLeft, "D03: a tap costs one move");
        }

        [Test]
        public void TapOnANonBooster_IsRejectedWithoutCharging()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteTap(new GridPos(0, 0));

            Assert.AreEqual(TurnOutcome.Rejected, transcript.Outcome);
            Assert.AreEqual(10, fixture.TurnRule.MovesLeft);
        }

        [Test]
        public void SwapBoosterWithChip_ActivatesInTheNewCell()
        {
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t3 t4 t3
                rh t2 t3 t4 t2", moveLimit: 10);

            // The rocket is dragged from (0,0) into (1,0), so it must fire from (1,0) (§6.1).
            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(0, 0), new GridPos(1, 0));

            int index = TurnFixture.IndexOf(transcript, TurnEventKind.BoosterActivated);
            Assert.AreNotEqual(-1, index);
            Assert.AreEqual(new GridPos(1, 0), transcript.GetEvent(index).A);
        }

        [Test]
        public void SwapBoosterWithBooster_ProducesACombination()
        {
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t3 t4 t3
                rh bm t3 t4 t2", moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(0, 0), new GridPos(1, 0));

            int index = TurnFixture.IndexOf(transcript, TurnEventKind.ComboActivated);
            Assert.AreNotEqual(-1, index, TurnFixture.Describe(transcript));

            TurnEvent combo = transcript.GetEvent(index);
            Assert.AreEqual(new GridPos(1, 0), combo.A, "the epicentre is the swap target cell (§6.1)");
        }

        [Test]
        public void GoalsClosed_WinsTheLevel()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 3) },
                moveLimit: 10,
                spawnPolicy: new ScriptedSpawnPolicy(ChipColor.C2, ChipColor.C3, ChipColor.C4));

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            Assert.AreEqual(TurnOutcome.Won, transcript.Outcome);
            Assert.AreNotEqual(-1, TurnFixture.IndexOf(transcript, TurnEventKind.LevelWon));
        }

        [Test]
        public void MovesExhaustedWithOpenGoals_LosesTheLevel()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 999) },
                moveLimit: 1,
                spawnPolicy: new ScriptedSpawnPolicy(ChipColor.C2, ChipColor.C3, ChipColor.C4));

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            Assert.AreEqual(TurnOutcome.Lost, transcript.Outcome);
            int index = TurnFixture.IndexOf(transcript, TurnEventKind.LevelLost);
            Assert.AreEqual(LevelEndReason.MovesExhausted, transcript.GetEvent(index).EndReason);
        }

        [Test]
        public void WinBeatsLose_WhenBothHappenInTheSameResolution()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 3) },
                moveLimit: 1,
                spawnPolicy: new ScriptedSpawnPolicy(ChipColor.C2, ChipColor.C3, ChipColor.C4));

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            Assert.AreEqual(TurnOutcome.Won, transcript.Outcome, "D14, E14: win takes precedence");
            Assert.AreEqual(-1, TurnFixture.IndexOf(transcript, TurnEventKind.LevelLost));
        }

        [Test]
        public void PostTurnOrder_CyclesColoursBeforeDecidingTheOutcome()
        {
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t1 t4 cx
                t1 t1 t2 t3 t4", moveLimit: 10, colorCount: 4);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            int postTurn = TurnFixture.IndexOf(transcript, TurnEventKind.PostTurnBegin);
            int cycled = TurnFixture.IndexOf(transcript, TurnEventKind.ElementColorCycled);

            Assert.AreNotEqual(-1, cycled, "cx recolours every turn regardless of damage (§7.2)");
            Assert.Greater(cycled, postTurn, "the recolour belongs to POST-TURN, not to resolution");
        }

        [Test]
        public void CyclingBox_AdvancesOneColourPerTurn()
        {
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t1 t4 cx
                t1 t1 t2 t3 t4", moveLimit: 10, colorCount: 4);

            var boxCell = new GridPos(4, 1);
            fixture.Board.TryGetElement(boxCell, out ElementInstance box);
            ChipColor before = box.CurrentColor;

            fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            Assert.AreEqual(ChipColors.NextCycling(before, 4), box.CurrentColor);
        }

        [Test]
        public void FreeMovesCheat_KeepsTheCounter()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);
            fixture.TurnRule.ExecuteCheat(CheatCommand.FreeMoves(true));

            fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            Assert.AreEqual(10, fixture.TurnRule.MovesLeft);
        }

        [Test]
        public void AddMovesCheat_RaisesTheCounter()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);

            fixture.TurnRule.ExecuteCheat(CheatCommand.AddMoves(5));

            Assert.AreEqual(15, fixture.TurnRule.MovesLeft);
        }

        [Test]
        public void PlaceBoosterCheat_DoesNotSpendAMove()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);
            var cell = new GridPos(4, 3);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(
                CheatCommand.PlaceBooster(cell, BoosterType.Bomb));

            Assert.AreEqual(10, fixture.TurnRule.MovesLeft, "§12: placing a booster is free");
            Assert.AreEqual(BoosterType.Bomb, fixture.Board.GetSlot(cell).Booster);
            Assert.AreNotEqual(-1, TurnFixture.IndexOf(transcript, TurnEventKind.BoosterSpawned));
        }

        [Test]
        public void LoseLevelCheat_EndsTheLevelImmediately()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(CheatCommand.LoseLevel());

            Assert.AreEqual(TurnOutcome.Lost, transcript.Outcome);
            Assert.AreEqual(0, fixture.TurnRule.MovesLeft);
            Assert.IsFalse(fixture.TurnRule.CanAcceptInput);
        }

        [Test]
        public void WinLevelCheat_ClosesGoalsAndPlaysTheBonus()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 15) },
                moveLimit: 12);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(CheatCommand.WinLevel());

            Assert.AreEqual(TurnOutcome.Won, transcript.Outcome);
            Assert.IsTrue(fixture.Goals.AllClosed);
            Assert.Greater(
                TurnFixture.CountOf(transcript, TurnEventKind.MovesBonusRocket),
                0,
                "the win cheat plays the full sequence including the leftover-move bonus (§12)");
        }

        [Test]
        public void LeftoverMoveBonus_IsCappedAtTen()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 15) },
                moveLimit: 40);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(CheatCommand.WinLevel());

            Assert.AreEqual(
                ResolveCaps.MaxMovesBonus,
                TurnFixture.CountOf(transcript, TurnEventKind.MovesBonusRocket),
                "§8.4: at most ten conversions");
            Assert.AreEqual(TurnOutcome.Won, transcript.Outcome, "E23: the bonus cannot lose the level");
        }

        [Test]
        public void GoToLevelCheat_IsRejectedByTheTurnRule()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(CheatCommand.GoToLevel(5));

            // It recreates the attempt instead of mutating the board, so the flow layer owns it.
            Assert.AreEqual(TurnOutcome.Rejected, transcript.Outcome);
            Assert.AreEqual(10, fixture.TurnRule.MovesLeft);
        }

        [Test]
        public void EveryTurn_StartsWithTurnBeginAndEndsWithTurnEnd()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            Assert.AreEqual(TurnEventKind.TurnBegin, transcript.GetEvent(0).Kind);
            Assert.AreEqual(
                TurnEventKind.TurnEnd,
                transcript.GetEvent(transcript.EventCount - 1).Kind);
        }

        [Test]
        public void TranscriptSeed_MatchesTheAttemptSeed()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout, seed: 4242);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);

            Assert.AreEqual(4242, transcript.Seed, "D12: the seed travels with the turn");
        }
    }
}
