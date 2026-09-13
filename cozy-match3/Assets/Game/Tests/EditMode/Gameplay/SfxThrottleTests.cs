using Match3.Content;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Gameplay
{
    /// <summary>
    /// The rule that keeps a cascade from clicking (GDD §11.4): every chip of a step dies in the
    /// same frame, so the throttle has to let one pop through and swallow the rest, while sounds
    /// that genuinely arrive apart - the landings of a falling column - all get through.
    /// </summary>
    public sealed class SfxThrottleTests
    {
        private SfxThrottle _throttle;

        private const float Interval = 0.05f;

        [SetUp]
        public void SetUp()
        {
            _throttle = new SfxThrottle();
        }

        [Test]
        public void FirstRequest_IsTaken()
        {
            Assert.IsTrue(_throttle.TryTake(SfxId.ChipDestroyed, 0f, Interval));
        }

        [Test]
        public void WholeStepInOneFrame_SoundsOnce()
        {
            Assert.IsTrue(_throttle.TryTake(SfxId.ChipDestroyed, 1f, Interval));

            for (int i = 0; i < 11; i++)
            {
                Assert.IsFalse(
                    _throttle.TryTake(SfxId.ChipDestroyed, 1f, Interval),
                    "a chip of the same step must not add a second pop");
            }
        }

        [Test]
        public void PastTheInterval_SoundsAgain()
        {
            Assert.IsTrue(_throttle.TryTake(SfxId.ChipDestroyed, 1f, Interval));

            Assert.IsFalse(_throttle.TryTake(SfxId.ChipDestroyed, 1f + (Interval * 0.5f), Interval));
            Assert.IsTrue(_throttle.TryTake(SfxId.ChipDestroyed, 1f + Interval, Interval));
        }

        [Test]
        public void SpreadOutLandings_AllGetThrough()
        {
            // What a falling column books: one landing per stagger, all inside a single frame.
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(_throttle.TryTake(SfxId.ChipLand, i * Interval, Interval));
            }
        }

        [Test]
        public void IdsDoNotBlockEachOther()
        {
            Assert.IsTrue(_throttle.TryTake(SfxId.ChipDestroyed, 0f, Interval));
            Assert.IsTrue(_throttle.TryTake(SfxId.Bomb, 0f, Interval));
            Assert.IsTrue(_throttle.TryTake(SfxId.GoalTick, 0f, Interval));
        }

        [Test]
        public void WithoutAnInterval_EveryRequestIsTaken()
        {
            Assert.IsTrue(_throttle.TryTake(SfxId.LevelWon, 2f, 0f));
            Assert.IsTrue(_throttle.TryTake(SfxId.LevelWon, 2f, 0f));
        }

        [Test]
        public void NoneIsNeverTaken()
        {
            Assert.IsFalse(_throttle.TryTake(SfxId.None, 0f, 0f));
        }
    }
}
