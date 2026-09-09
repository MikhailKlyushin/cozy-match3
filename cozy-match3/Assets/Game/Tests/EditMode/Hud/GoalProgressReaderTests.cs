using Match3.Content;
using Match3.Hud;
using Match3.Resolve;
using NUnit.Framework;
using UnityEngine;

namespace Match3.Tests.EditMode.Hud
{
    /// <summary>
    /// The HUD ticks goal counters from GoalProgress events, not from goal state (rule T4). These
    /// tests feed a hand-built transcript and check the per-goal totals the panel presenter uses.
    /// </summary>
    public sealed class GoalProgressReaderTests
    {
        private const float Tolerance = 1e-4f;
        private const int Seed = 12345;
        private const int MovesLeft = 9;

        private TurnTranscript _transcript;
        private TranscriptWriter _writer;

        [SetUp]
        public void SetUp()
        {
            _transcript = new TurnTranscript();
            _writer = new TranscriptWriter(_transcript);
            _writer.TurnBegin(Seed, MovesLeft);
        }

        [Test]
        public void Read_SumsDeltasAndKeepsTheLastClampedTotalPerGoal()
        {
            _writer.GoalProgress(0, 1, 3);
            _writer.GoalProgress(1, 2, 2);
            _writer.GoalProgress(0, 2, 5);
            _writer.TurnEnd();

            var reader = new GoalProgressReader(2);
            reader.Read(_transcript);

            Assert.IsTrue(reader.HasProgress(0));
            Assert.AreEqual(3, reader.GetDelta(0));
            Assert.AreEqual(5, reader.GetValue(0));

            Assert.IsTrue(reader.HasProgress(1));
            Assert.AreEqual(2, reader.GetDelta(1));
            Assert.AreEqual(2, reader.GetValue(1));
        }

        [Test]
        public void Read_DerivesTheStartingValueFromTheFirstEvent()
        {
            _writer.GoalProgress(0, 1, 8);
            _writer.GoalProgress(0, 1, 9);
            _writer.TurnEnd();

            var reader = new GoalProgressReader(1);
            reader.Read(_transcript);

            Assert.AreEqual(7, reader.GetFrom(0));
            Assert.AreEqual(9, reader.GetValue(0));
            Assert.AreEqual(2, reader.GetDelta(0));
        }

        [Test]
        public void Read_GoalWithoutProgress_HasNothingToTick()
        {
            _writer.GoalProgress(1, 1, 1);
            _writer.TurnEnd();

            var reader = new GoalProgressReader(3);
            reader.Read(_transcript);

            Assert.IsFalse(reader.HasProgress(0));
            Assert.IsFalse(reader.HasProgress(2));
            Assert.AreEqual(0, reader.GetDelta(0));
        }

        [Test]
        public void Read_ClampedCredit_StopsAtTheTarget()
        {
            // E18: the writer already reports the clamped part, so the HUD never overshoots.
            _writer.GoalProgress(0, 2, 4);
            _writer.GoalProgress(0, 1, 5);
            _writer.TurnEnd();

            var reader = new GoalProgressReader(1);
            reader.Read(_transcript);

            Assert.AreEqual(2, reader.GetFrom(0));
            Assert.AreEqual(5, reader.GetValue(0));
            Assert.AreEqual(3, reader.GetDelta(0));
        }

        [Test]
        public void Read_GoalIndexOutsideTheLevel_IsSkippedAndCounted()
        {
            _writer.GoalProgress(0, 1, 1);
            _writer.GoalProgress(4, 1, 1);
            _writer.TurnEnd();

            var reader = new GoalProgressReader(1);
            reader.Read(_transcript);

            Assert.AreEqual(1, reader.SkippedEventCount);
            Assert.AreEqual(1, reader.GetDelta(0));
        }

        [Test]
        public void Read_ForgetsThePreviousTurn()
        {
            _writer.GoalProgress(0, 3, 3);
            _writer.TurnEnd();

            var reader = new GoalProgressReader(1);
            reader.Read(_transcript);
            Assert.AreEqual(3, reader.GetDelta(0));

            var secondTurn = new TurnTranscript();
            var secondWriter = new TranscriptWriter(secondTurn);
            secondWriter.TurnBegin(Seed, MovesLeft - 1);
            secondWriter.GoalProgress(0, 1, 4);
            secondWriter.TurnEnd();

            reader.Read(secondTurn);

            Assert.AreEqual(1, reader.GetDelta(0));
            Assert.AreEqual(3, reader.GetFrom(0));
            Assert.AreEqual(4, reader.GetValue(0));
        }

        [Test]
        public void Read_NoProgressEvents_LeavesEveryGoalIdle()
        {
            _writer.TurnEnd();

            var reader = new GoalProgressReader(2);
            reader.Read(_transcript);

            Assert.IsFalse(reader.HasProgress(0));
            Assert.IsFalse(reader.HasProgress(1));
            Assert.AreEqual(0, reader.SkippedEventCount);
        }

        [Test]
        public void Read_LargeJump_SchedulesInsideTheTotalCap()
        {
            // 40 units in one turn (a rainbow chain): the tick step must compress, not overrun.
            for (int i = 0; i < 40; i++)
            {
                _writer.GoalProgress(0, 1, i + 1);
            }

            _writer.TurnEnd();

            var reader = new GoalProgressReader(1);
            reader.Read(_transcript);

            TimingProfile timings = ScriptableObject.CreateInstance<TimingProfile>();
            try
            {
                GoalTickSchedule schedule = GoalTickSchedule.Between(reader.GetFrom(0), reader.GetValue(0), timings);

                Assert.AreEqual(40, reader.GetDelta(0));
                Assert.AreEqual(40, schedule.Units);
                Assert.AreEqual(timings.GoalTickTotalCap, schedule.Total, Tolerance);
                Assert.AreEqual(40, schedule.ValueAt(schedule.Units - 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(timings);
            }
        }
    }
}
