using System;
using System.Collections.Generic;
using Match3.Core;

namespace Match3.Tests.EditMode.Levels
{
    /// <summary>
    /// Returns a scripted sequence and throws once it runs out, so an unexpected extra draw
    /// fails the test instead of silently shifting the §13 consumption order.
    /// </summary>
    internal sealed class ScriptedRandom : IRandom
    {
        private readonly int[] _values;

        public ScriptedRandom(params int[] values)
        {
            _values = values ?? Array.Empty<int>();
        }

        public int Draws { get; private set; }

        public int Seed => 0;

        public int NextInt(int maxExclusive)
        {
            if (Draws >= _values.Length)
            {
                throw new InvalidOperationException("Scripted random ran out after " + Draws + " draws.");
            }

            return _values[Draws++];
        }

        public int NextInt(int minInclusive, int maxExclusive) => minInclusive + NextInt(maxExclusive - minInclusive);

        public void Shuffle<T>(IList<T> list)
        {
        }
    }
}
