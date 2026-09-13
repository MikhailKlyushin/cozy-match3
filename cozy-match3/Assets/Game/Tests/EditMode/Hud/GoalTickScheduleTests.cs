using System;
using Match3.Content;
using Match3.Hud;
using NUnit.Framework;
using UnityEngine;

namespace Match3.Tests.EditMode.Hud
{
    /// <summary>Goal tick schedule of GDD §11.3: 0.15 s per unit, never longer than 1.0 s in total.</summary>
    public sealed class GoalTickScheduleTests
    {
        private const float Tolerance = 1e-4f;

        private TimingProfile _timings;

        [SetUp]
        public void SetUp()
        {
            _timings = ScriptableObject.CreateInstance<TimingProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_timings);
            _timings = null;
        }

        [Test]
        public void Between_OneUnit_TicksAtThePerUnitStep()
        {
            GoalTickSchedule schedule = GoalTickSchedule.Between(0, 1, _timings);

            Assert.AreEqual(1, schedule.Units);
            Assert.AreEqual(_timings.GoalTickPerUnit, schedule.Step, Tolerance);
            Assert.AreEqual(_timings.GoalTickPerUnit, schedule.Total, Tolerance);
        }

        [Test]
        public void Between_FiveUnits_StillTicksAtThePerUnitStep()
        {
            GoalTickSchedule schedule = GoalTickSchedule.Between(0, 5, _timings);

            Assert.AreEqual(5, schedule.Units);
            Assert.AreEqual(_timings.GoalTickPerUnit, schedule.Step, Tolerance);
            Assert.AreEqual(5f * _timings.GoalTickPerUnit, schedule.Total, Tolerance);
            Assert.LessOrEqual(schedule.Total, _timings.GoalTickTotalCap + Tolerance);
        }

        [Test]
        public void Between_FortyUnits_CompressesTheStepToFitTheCap()
        {
            GoalTickSchedule schedule = GoalTickSchedule.Between(0, 40, _timings);

            Assert.AreEqual(40, schedule.Units);
            Assert.AreEqual(_timings.GoalTickTotalCap / 40f, schedule.Step, Tolerance);
            Assert.AreEqual(_timings.GoalTickTotalCap, schedule.Total, Tolerance);
            Assert.Less(schedule.Step, _timings.GoalTickPerUnit);
        }

        [Test]
        public void Between_JustOverTheCap_CompressesToExactlyTheCap()
        {
            // 7 * 0.15 = 1.05 s, the first jump that does not fit the 1.0 s budget.
            GoalTickSchedule schedule = GoalTickSchedule.Between(3, 10, _timings);

            Assert.AreEqual(7, schedule.Units);
            Assert.AreEqual(_timings.GoalTickTotalCap, schedule.Total, Tolerance);
        }

        [Test]
        public void Between_NoProgress_ProducesAnEmptySchedule()
        {
            GoalTickSchedule schedule = GoalTickSchedule.Between(4, 4, _timings);

            Assert.AreEqual(0, schedule.Units);
            Assert.AreEqual(0f, schedule.Step, Tolerance);
            Assert.AreEqual(0f, schedule.Total, Tolerance);
        }

        [Test]
        public void Between_BackwardsProgress_ProducesAnEmptySchedule()
        {
            GoalTickSchedule schedule = GoalTickSchedule.Between(6, 2, _timings);

            Assert.AreEqual(0, schedule.Units);
            Assert.AreEqual(0f, schedule.Total, Tolerance);
        }

        [Test]
        public void Between_AnyJump_FitsTheTotalCap()
        {
            int[] units = { 1, 5, 40 };

            for (int i = 0; i < units.Length; i++)
            {
                GoalTickSchedule schedule = GoalTickSchedule.Between(0, units[i], _timings);
                Assert.LessOrEqual(
                    schedule.Total,
                    _timings.GoalTickTotalCap + Tolerance,
                    "Jump of " + units[i] + " units exceeded the total cap.");
            }
        }

        [Test]
        public void ValueAt_WalksEveryValueUpToTheClampedTotal()
        {
            GoalTickSchedule schedule = GoalTickSchedule.Between(7, 10, _timings);

            Assert.AreEqual(8, schedule.ValueAt(0));
            Assert.AreEqual(9, schedule.ValueAt(1));
            Assert.AreEqual(10, schedule.ValueAt(2));
        }

        [Test]
        public void ValueAt_OutsideTheSchedule_Throws()
        {
            GoalTickSchedule schedule = GoalTickSchedule.Between(0, 2, _timings);

            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.ValueAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.ValueAt(-1));
        }

        [Test]
        public void Between_WithoutTimings_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GoalTickSchedule.Between(0, 3, null));
        }
    }
}
