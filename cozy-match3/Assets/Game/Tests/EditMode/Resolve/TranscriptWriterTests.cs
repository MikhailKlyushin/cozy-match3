using Match3.Core;
using Match3.Resolve;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Resolve
{
    public sealed class TranscriptWriterTests
    {
        [Test]
        public void TurnBegin_ResetsAndStampsHeader()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(seed: 555, movesLeft: 18);

            Assert.AreEqual(555, transcript.Seed);
            Assert.AreEqual(18, transcript.MovesLeft);
            Assert.AreEqual(1, transcript.EventCount);
            Assert.AreEqual(TurnEventKind.TurnBegin, transcript.GetEvent(0).Kind);
        }

        [Test]
        public void MoveCharged_FollowsSwapPerformedImmediately()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 10);
            writer.SwapPerformed(new GridPos(1, 1), new GridPos(1, 2));
            writer.MoveCharged(9);

            Assert.AreEqual(TurnEventKind.SwapPerformed, transcript.GetEvent(1).Kind);
            Assert.AreEqual(TurnEventKind.MoveCharged, transcript.GetEvent(2).Kind);
            Assert.AreEqual(9, transcript.GetEvent(2).Value);
            Assert.AreEqual(9, transcript.MovesLeft);
        }

        [Test]
        public void StepAndWave_AreStampedOntoEveryEvent()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 10);
            writer.StepBegin(2);
            writer.WaveBarrier(3);
            writer.ChipDestroyed(new GridPos(0, 0), ChipColor.C1, instanceId: 7);

            TurnEvent destroyed = transcript.GetEvent(transcript.EventCount - 1);
            Assert.AreEqual(2, destroyed.Step);
            Assert.AreEqual(3, destroyed.Wave);
        }

        [Test]
        public void StepBegin_TracksMaxCascadeDepth()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 10);
            writer.StepBegin(0);
            writer.StepBegin(1);
            writer.StepBegin(2);
            writer.StepBegin(1);

            Assert.AreEqual(2, transcript.MaxDepth);
        }

        [Test]
        public void BoosterEffectCells_WritesSliceIntoSharedBuffer()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);
            var cells = new CellBuffer();
            cells.Configure(8, 8);
            cells.Add(new GridPos(1, 1));
            cells.Add(new GridPos(2, 1));
            cells.Add(new GridPos(3, 1));

            writer.TurnBegin(1, 10);
            writer.BoosterEffectCells(new GridPos(2, 1), BoosterType.RocketH, cells);

            TurnEvent e = transcript.GetEvent(1);
            Assert.AreEqual(0, e.CellsOffset);
            Assert.AreEqual(3, e.CellsCount);
            Assert.AreEqual(new GridPos(2, 1), transcript.GetCell(e.CellsOffset + 1));
        }

        [Test]
        public void CellSlices_OfTwoEventsDoNotOverlap()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);
            var cells = new CellBuffer();
            cells.Configure(8, 8);
            cells.Add(new GridPos(0, 0));
            cells.Add(new GridPos(1, 0));

            writer.TurnBegin(1, 10);
            writer.BoosterEffectCells(new GridPos(0, 0), BoosterType.Bomb, cells);
            writer.BoosterEffectCells(new GridPos(1, 0), BoosterType.Bomb, cells);

            Assert.AreEqual(0, transcript.GetEvent(1).CellsOffset);
            Assert.AreEqual(2, transcript.GetEvent(2).CellsOffset);
            Assert.AreEqual(4, transcript.CellCount);
        }

        [Test]
        public void LevelWon_SetsOutcomeAndReason()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 0);
            writer.LevelWon(LevelEndReason.GoalsClosed);

            Assert.AreEqual(TurnOutcome.Won, transcript.Outcome);
            Assert.AreEqual(LevelEndReason.GoalsClosed, transcript.GetEvent(1).EndReason);
        }

        [Test]
        public void CapHit_CarriesCapKind()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 5);
            writer.CapHit(CapKind.WaveCap);

            Assert.AreEqual(CapKind.WaveCap, transcript.GetEvent(1).Cap);
        }

        [Test]
        public void ComboActivated_CarriesBothParticipants()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 5);
            writer.ComboActivated(new GridPos(3, 3), new GridPos(3, 4), BoosterType.Bomb, BoosterType.Rainbow);

            TurnEvent e = transcript.GetEvent(1);
            Assert.AreEqual(BoosterType.Bomb, e.Booster);
            Assert.AreEqual(BoosterType.Rainbow, e.ComboBoosterB);
            Assert.AreEqual(new GridPos(3, 3), e.A);
        }

        [Test]
        public void ChipMoved_RoundTripsMoveFlags()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 5);
            writer.ChipMoved(new GridPos(2, 5), new GridPos(2, 4), ChipMoveFlags.Fall, instanceId: 3);
            writer.ChipMoved(new GridPos(3, 5), new GridPos(2, 4), ChipMoveFlags.Slide, instanceId: 4);

            Assert.AreEqual(ChipMoveFlags.Fall, transcript.GetEvent(1).MoveFlags);
            Assert.AreEqual(ChipMoveFlags.Slide, transcript.GetEvent(2).MoveFlags);
        }

        [Test]
        public void Reset_ReusesBuffersWithoutShrinking()
        {
            var transcript = new TurnTranscript(eventCapacity: 8, cellCapacity: 8);
            var writer = new TranscriptWriter(transcript);

            writer.TurnBegin(1, 10);
            for (int i = 0; i < 500; i++)
            {
                writer.ChipDestroyed(new GridPos(0, 0), ChipColor.C1, i);
            }

            int eventCapacity = transcript.EventCapacity;
            transcript.Reset();

            Assert.AreEqual(0, transcript.EventCount);
            Assert.AreEqual(eventCapacity, transcript.EventCapacity);
        }

        [Test]
        public void Writing500Events_DoesNotGrowBuffersAfterWarmUp()
        {
            var transcript = new TurnTranscript();
            var writer = new TranscriptWriter(transcript);

            WriteBatch(writer, 500);
            int warmEventCapacity = transcript.EventCapacity;
            int warmCellCapacity = transcript.CellCapacity;

            for (int repeat = 0; repeat < 5; repeat++)
            {
                WriteBatch(writer, 500);
                Assert.AreEqual(warmEventCapacity, transcript.EventCapacity, "event buffer regrew");
                Assert.AreEqual(warmCellCapacity, transcript.CellCapacity, "cell buffer regrew");
            }
        }

        private static void WriteBatch(TranscriptWriter writer, int count)
        {
            var cells = new CellBuffer();
            cells.Configure(9, 9);
            cells.Add(new GridPos(0, 0));

            writer.TurnBegin(1, 10);
            for (int i = 0; i < count; i++)
            {
                writer.ChipDestroyed(new GridPos(i % 9, (i / 9) % 9), ChipColor.C2, i);
                if (i % 25 == 0)
                {
                    writer.BoosterEffectCells(new GridPos(0, 0), BoosterType.Bomb, cells);
                }
            }
        }
    }
}
