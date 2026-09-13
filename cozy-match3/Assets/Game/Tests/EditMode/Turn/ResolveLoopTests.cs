using System.Text;
using Match3.Core;
using Match3.Goals;
using Match3.Resolve;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Turn
{
    public sealed class ResolveLoopTests
    {
        /// <summary>
        /// No ready-made match; swapping (2,0) with (2,1) puts a third C1 into the bottom row.
        /// </summary>
        private const string OneSwapLayout = @"
            t3 t4 t3 t4 t3
            t4 t3 t4 t3 t4
            t3 t4 t1 t4 t3
            t1 t1 t2 t3 t4";

        [Test]
        public void AnimateBarrier_SeparatesDestructionFromEveryFall()
        {
            TurnFixture fixture = TurnFixture.Create(OneSwapLayout);

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(2, 0), new GridPos(2, 1));

            AssertBarrierOrdering(transcript);
        }

        [Test]
        public void Cascade_KeepsBarrierOrderingInEveryStep()
        {
            // Every refilled chip is C1, so the board keeps matching and the cascade runs deep.
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                spawnPolicy: new FixedColorSpawnPolicy(ChipColor.C1));

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(2, 0), new GridPos(2, 1));

            Assert.Greater(transcript.MaxDepth, 0, "expected a cascade");
            AssertBarrierOrdering(transcript);
        }

        [Test]
        public void MatchDuringTheFall_ResolvesByTheOrdinaryRules()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                spawnPolicy: new FixedColorSpawnPolicy(ChipColor.C1));

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(2, 0), new GridPos(2, 1));

            // A cascade step exists that the player did not cause: that is the automatic match
            // the client asked for.
            Assert.GreaterOrEqual(TurnFixture.CountOf(transcript, TurnEventKind.StepBegin), 2);
        }

        [Test]
        public void CascadeDepthCap_ReportsCapHitAndLogsAnError()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                spawnPolicy: new FixedColorSpawnPolicy(ChipColor.C1));

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(2, 0), new GridPos(2, 1));

            int capIndex = TurnFixture.IndexOf(transcript, TurnEventKind.CapHit);
            Assert.AreNotEqual(-1, capIndex, "a board that always matches must hit the depth cap");
            Assert.AreEqual(CapKind.DepthCap, transcript.GetEvent(capIndex).Cap);
            Assert.IsNotEmpty(fixture.Logger.Errors, "a cap is always a bug and must be logged (E05)");
        }

        [Test]
        public void BoosterCreatedInACascade_SpawnsWithoutFiringInTheSameStep()
        {
            // Swapping (3,0) with (3,1) completes a horizontal line of exactly four C1.
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t3 t1 t3
                t1 t1 t1 t2 t4");

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(3, 0), new GridPos(3, 1));

            int spawnIndex = TurnFixture.IndexOf(transcript, TurnEventKind.BoosterSpawned);
            Assert.AreNotEqual(-1, spawnIndex, "a line of four must create a rocket");

            byte spawnStep = transcript.GetEvent(spawnIndex).Step;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.BoosterActivated && e.Step == spawnStep)
                {
                    Assert.Fail("D06: a booster created in a step must not fire in that step");
                }
            }
        }

        [Test]
        public void RocketCoveringABomb_FiresItAsAChainLink()
        {
            // The rocket is tapped at (0,0); the bomb sits in the same row and is caught (E04).
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t3 t4 t3
                rh t2 bm t3 t4");

            TurnTranscript transcript = fixture.TurnRule.ExecuteTap(new GridPos(0, 0));

            bool bombFired = false;
            bool bombOnLaterWave = false;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.BoosterActivated && e.Booster == BoosterType.Bomb)
                {
                    bombFired = true;
                    bombOnLaterWave = e.Wave > 0 && e.ActivationSource == BoosterActivationSource.Chain;
                }
            }

            Assert.IsTrue(bombFired, "E04: a booster inside a blast fires");
            Assert.IsTrue(bombOnLaterWave, "the bomb must fire on a later wave, as a chain link");
        }

        [Test]
        public void TwoRocketsCoveringEachOther_DoNotLoop()
        {
            TurnFixture fixture = TurnFixture.Create(@"
                t3 t4 t3 t4 t3
                t4 t3 t4 t3 t4
                t3 t4 t3 t4 t3
                rh t2 rh t3 t4");

            TurnTranscript transcript = fixture.TurnRule.ExecuteTap(new GridPos(0, 0));

            // D08: consumed stops the ping-pong, so each rocket fires exactly once.
            int activations = 0;
            for (int i = 0; i < transcript.EventCount; i++)
            {
                if (transcript.GetEvent(i).Kind == TurnEventKind.BoosterActivated)
                {
                    activations++;
                }
            }

            Assert.AreEqual(2, activations);
            Assert.AreEqual(-1, TurnFixture.IndexOf(transcript, TurnEventKind.CapHit));
        }

        [Test]
        public void WaveCap_ReportsCapHitAndLogsAnError()
        {
            // A ladder of bombs two rows apart: each blast reaches exactly the next bomb, so the
            // chain advances one booster per wave and runs past the 20-wave cap.
            TurnFixture fixture = TurnFixture.Create(BuildBombLadder(rows: 45));

            TurnTranscript transcript = fixture.TurnRule.ExecuteTap(new GridPos(0, 0));

            int capIndex = TurnFixture.IndexOf(transcript, TurnEventKind.CapHit);
            Assert.AreNotEqual(-1, capIndex, TurnFixture.Describe(transcript));
            Assert.AreEqual(CapKind.WaveCap, transcript.GetEvent(capIndex).Cap);
            Assert.IsNotEmpty(fixture.Logger.Errors);
        }

        [Test]
        public void GoalProgress_IsNeverWrittenWithAZeroDelta()
        {
            TurnFixture fixture = TurnFixture.Create(
                OneSwapLayout,
                goals: new[] { GoalDefinition.CollectColor(ChipColor.C1, 1) },
                spawnPolicy: new ScriptedSpawnPolicy(ChipColor.C2, ChipColor.C3, ChipColor.C4));

            TurnTranscript transcript = fixture.TurnRule.ExecuteSwap(new GridPos(2, 0), new GridPos(2, 1));

            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);
                if (e.Kind == TurnEventKind.GoalProgress)
                {
                    Assert.AreNotEqual(0, e.Amount, "a zero-delta tick is noise in the HUD");
                }
            }
        }

        /// <summary>
        /// Within every step: nothing falls before the barrier and nothing is destroyed after it
        /// (§5.1 stages 6-8, rule T1). This is the client's explicit requirement.
        /// </summary>
        private static void AssertBarrierOrdering(TurnTranscript transcript)
        {
            bool seenBarrierInStep = false;
            int step = -1;
            int barriers = 0;

            for (int i = 0; i < transcript.EventCount; i++)
            {
                TurnEvent e = transcript.GetEvent(i);

                if (e.Kind == TurnEventKind.StepBegin)
                {
                    step = e.Step;
                    seenBarrierInStep = false;
                    continue;
                }

                switch (e.Kind)
                {
                    case TurnEventKind.AnimateBarrier:
                        seenBarrierInStep = true;
                        barriers++;
                        break;

                    case TurnEventKind.ChipMoved when e.MoveFlags != ChipMoveFlags.Shuffle:
                    case TurnEventKind.ChipSpawned:
                        Assert.IsTrue(
                            seenBarrierInStep,
                            "step " + step + ": a fall started before the ANIMATE barrier");
                        break;

                    case TurnEventKind.ChipDestroyed:
                    case TurnEventKind.ElementDestroyed:
                        Assert.IsFalse(
                            seenBarrierInStep,
                            "step " + step + ": something was destroyed after the ANIMATE barrier");
                        break;
                }
            }

            Assert.Greater(barriers, 0, "a resolved turn must contain at least one ANIMATE barrier");
        }

        private static string BuildBombLadder(int rows)
        {
            var text = new StringBuilder(rows * 12);

            // Rows are written top-down, so the ladder is built from the top and the tapped bomb
            // ends up at (0, 0). Columns 1 and 2 alternate their colours, otherwise 45 identical
            // rows would be one enormous ready-made match.
            for (int row = 0; row < rows; row++)
            {
                int y = rows - 1 - row;
                text.Append(y % 2 == 0 ? "bm" : "t1")
                    .Append(y % 2 == 0 ? " t2 t3\n" : " t3 t2\n");
            }

            return text.ToString();
        }
    }
}
