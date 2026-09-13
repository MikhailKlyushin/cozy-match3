using Match3.Content;
using NUnit.Framework;
using UnityEngine;

namespace Match3.Tests.EditMode.Gameplay
{
    /// <summary>
    /// The pure half of the §11.3 timing contract: the profile's own arithmetic and the fall
    /// profile the transcript player derives its tweens from. Everything that needs a scene
    /// belongs to the play-mode smoke test.
    /// </summary>
    public sealed class PlaybackTimingTests
    {
        private const float Tolerance = 1e-4f;

        private TimingProfile _timings;

        [SetUp]
        public void SetUp() => _timings = ScriptableObject.CreateInstance<TimingProfile>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_timings);

        [Test]
        public void DestroyTotalDuration_IsThePunchPlusTheFade()
        {
            Assert.AreEqual(
                _timings.DestroyPunchDuration + _timings.DestroyFadeDuration,
                _timings.DestroyTotalDuration,
                Tolerance);

            // §11.3: 0.06 s punch plus 0.14 s fade, 0.20 s in total.
            Assert.AreEqual(0.20f, _timings.DestroyTotalDuration, Tolerance);
        }

        [Test]
        public void GoalTickStep_KeepsThePerUnitStep_WhileTheTotalFitsUnderTheCap()
        {
            int units = (int)(_timings.GoalTickTotalCap / _timings.GoalTickPerUnit);

            Assert.AreEqual(_timings.GoalTickPerUnit, _timings.GoalTickStep(units), Tolerance);
        }

        [Test]
        public void GoalTickStep_CompressesALargeJump_IntoTheTotalCap()
        {
            const int units = 20;

            float step = _timings.GoalTickStep(units);

            Assert.Less(step, _timings.GoalTickPerUnit);
            Assert.AreEqual(_timings.GoalTickTotalCap, step * units, Tolerance);
        }

        [Test]
        public void GoalTickStep_IsZero_WhenNothingWasCollected()
        {
            Assert.AreEqual(0f, _timings.GoalTickStep(0), Tolerance);
            Assert.AreEqual(0f, _timings.GoalTickStep(-3), Tolerance);
        }

        [Test]
        public void FallProfile_AcceleratesForFourCellsBeforeReachingMaxSpeed()
        {
            // §11.3: 40 cells/s^2 up to 18 cells/s. On a board of at most nine rows the clamp
            // therefore only affects falls longer than about four cells.
            Assert.AreEqual(40f, _timings.FallAcceleration, Tolerance);
            Assert.AreEqual(18f, _timings.FallMaxSpeed, Tolerance);

            float timeToMaxSpeed = _timings.FallMaxSpeed / _timings.FallAcceleration;
            float acceleratedCells = 0.5f * _timings.FallAcceleration * timeToMaxSpeed * timeToMaxSpeed;

            Assert.AreEqual(0.45f, timeToMaxSpeed, Tolerance);
            Assert.AreEqual(4.05f, acceleratedCells, 1e-3f);
        }

        [Test]
        public void Profile_CarriesTheGdd113Durations()
        {
            Assert.AreEqual(0.15f, _timings.SwapDuration, Tolerance);
            Assert.AreEqual(0.10f, _timings.RejectedSwapOut, Tolerance);
            Assert.AreEqual(0.10f, _timings.RejectedSwapBack, Tolerance);
            Assert.AreEqual(0.06f, _timings.LandingSquashDuration, Tolerance);
            Assert.AreEqual(0.03f, _timings.NeighbourFallStagger, Tolerance);
            Assert.AreEqual(0.08f, _timings.StepPause, Tolerance);
            Assert.AreEqual(0.12f, _timings.WaveBarrier, Tolerance);
            Assert.AreEqual(0.25f, _timings.CyclingBoxMorph, Tolerance);
            Assert.AreEqual(0.35f, _timings.ShuffleScatter, Tolerance);
            Assert.AreEqual(0.35f, _timings.ShuffleGather, Tolerance);
        }
    }
}
