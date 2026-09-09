#if MATCH3_CHEATS
using System;
using Match3.Cheats;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using Match3.Tests.EditMode.Turn;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Cheats
{
    /// <summary>
    /// The dev-tool dump (beyond §12). It is the only way to read what the model recorded while
    /// the view is still a turn behind (A01), so its lines have to name the right fields: the
    /// meaning of Value and Amount depends on the event kind (§6).
    /// </summary>
    public sealed class CheatTranscriptDumpTests
    {
        /// <summary>Swapping (2,0) with (2,1) completes three C1 in the bottom row.</summary>
        private const string Layout = @"
            t3 t4 t3 t4 t3
            t4 t3 t4 t3 t4
            t3 t4 t1 t4 t3
            t1 t1 t2 t3 t4";

        private static readonly GridPos SwapFrom = new GridPos(2, 0);
        private static readonly GridPos SwapTo = new GridPos(2, 1);

        [Test]
        public void TheHeaderCarriesTheSeedOutcomeAndSize()
        {
            TurnFixture fixture = TurnFixture.Create(Layout, moveLimit: 10, seed: 4242);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(CheatCommand.AddMoves(5));
            string dump = CheatTranscriptDump.Format(transcript);

            StringAssert.StartsWith("[cheat] transcript seed=4242", dump);
            StringAssert.Contains("outcome=Resolved", dump);
            StringAssert.Contains("movesLeft=15", dump);
            StringAssert.Contains("events=" + transcript.EventCount.ToString(), dump);
        }

        [Test]
        public void EveryEventGetsExactlyOneLine()
        {
            TurnFixture fixture = TurnFixture.Create(Layout, moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);
            string[] lines = CheatTranscriptDump.Format(transcript).Split('\n');

            Assert.AreEqual(transcript.EventCount + 1, lines.Length, "one header plus one line per event");
        }

        [Test]
        public void TheCommitLinesNameTheCellsAndTheChargedMove()
        {
            TurnFixture fixture = TurnFixture.Create(Layout, moveLimit: 10);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(SwapFrom, SwapTo);
            string dump = CheatTranscriptDump.Format(transcript);

            StringAssert.Contains("#000 TurnBegin movesLeft=10", dump);
            StringAssert.Contains("SwapPerformed a=(2, 0) b=(2, 1)", dump);
            StringAssert.Contains("MoveCharged movesLeft=9", dump);
        }

        [Test]
        public void APlacedBoosterIsReadableInTheDump()
        {
            TurnFixture fixture = TurnFixture.Create(Layout, moveLimit: 10);
            var cell = new GridPos(4, 3);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(
                CheatCommand.PlaceBooster(cell, BoosterType.Bomb));
            string dump = CheatTranscriptDump.Format(transcript);

            StringAssert.Contains("BoosterSpawned at=(4, 3) booster=Bomb id=", dump);
        }

        [Test]
        public void TheWinCheatDumpShowsTheEndReasonAndTheBonusRockets()
        {
            TurnFixture fixture = TurnFixture.Create(
                Layout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 15) },
                moveLimit: 12);

            TurnTranscript transcript = fixture.TurnRule.ExecuteCheat(CheatCommand.WinLevel());
            string dump = CheatTranscriptDump.Format(transcript);

            StringAssert.Contains("MovesBonusRocket at=", dump);
            StringAssert.Contains("LevelWon", dump);
            StringAssert.Contains("reason=Cheat", dump);
        }

        [Test]
        public void NoTranscriptIsAProgrammerError()
        {
            Assert.Throws<ArgumentNullException>(() => CheatTranscriptDump.Format(null));
        }
    }
}
#endif
