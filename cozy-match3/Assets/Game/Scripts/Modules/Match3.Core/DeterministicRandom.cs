using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// Per-attempt RNG over <see cref="Random"/>. Seeded <c>Random(int)</c> keeps its legacy
    /// algorithm for compatibility, so Mono (Editor) and IL2CPP (WebGL) produce the same stream.
    /// </summary>
    public sealed class DeterministicRandom : IRandom
    {
        private readonly Random _random;

        public DeterministicRandom(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Upper bound must be positive.");
            }

            return _random.Next(maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Empty range.");
            }

            return _random.Next(minInclusive, maxExclusive);
        }

        public void Shuffle<T>(IList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
