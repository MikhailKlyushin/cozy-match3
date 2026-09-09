using System.Collections.Generic;
using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests.EditMode.Core
{
    public sealed class DeterministicRandomTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalSequences()
        {
            var a = new DeterministicRandom(12345);
            var b = new DeterministicRandom(12345);

            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(a.NextInt(1000), b.NextInt(1000), "divergence at draw " + i);
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);

            bool anyDifference = false;
            for (int i = 0; i < 50 && !anyDifference; i++)
            {
                anyDifference = a.NextInt(1000) != b.NextInt(1000);
            }

            Assert.IsTrue(anyDifference);
        }

        [Test]
        public void Seed_IsExposedForCheatPanelAndLogs()
        {
            Assert.AreEqual(777, new DeterministicRandom(777).Seed);
        }

        [Test]
        public void NextIntRange_StaysWithinBounds()
        {
            var random = new DeterministicRandom(99);

            for (int i = 0; i < 500; i++)
            {
                int value = random.NextInt(10, 20);
                Assert.GreaterOrEqual(value, 10);
                Assert.Less(value, 20);
            }
        }

        [Test]
        public void Shuffle_IsDeterministicForTheSameSeed()
        {
            List<int> first = Sequence(16);
            List<int> second = Sequence(16);

            new DeterministicRandom(4242).Shuffle(first);
            new DeterministicRandom(4242).Shuffle(second);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Shuffle_IsAPermutation()
        {
            List<int> values = Sequence(32);
            new DeterministicRandom(7).Shuffle(values);

            values.Sort();
            Assert.AreEqual(Sequence(32), values);
        }

        [Test]
        public void Shuffle_ActuallyReorders()
        {
            List<int> values = Sequence(32);
            new DeterministicRandom(7).Shuffle(values);

            Assert.AreNotEqual(Sequence(32), values);
        }

        private static List<int> Sequence(int count)
        {
            var list = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(i);
            }

            return list;
        }
    }
}
