using System;
using Match3.Progression;

namespace Match3.Bootstrap
{
    /// <summary>
    /// Draws the seed of a new attempt. Deliberately the ONE place that reads the clock: the seed
    /// is random per attempt, and everything downstream of it is deterministic (D12, §13). This
    /// runs at attempt setup, never inside a turn, so it is outside the gameplay path where
    /// DateTime.Now is banned.
    /// </summary>
    public sealed class SystemSeedSource : ILevelSeedSource
    {
        private int _counter;

        public int NextSeed()
        {
            // Mixing in a counter keeps two attempts started in the same tick distinct.
            long ticks = DateTime.UtcNow.Ticks;
            int mixed = (int)(ticks ^ (ticks >> 32)) + (++_counter * 2654435761u).GetHashCode();
            return mixed == int.MinValue ? 1 : Math.Abs(mixed);
        }
    }
}
