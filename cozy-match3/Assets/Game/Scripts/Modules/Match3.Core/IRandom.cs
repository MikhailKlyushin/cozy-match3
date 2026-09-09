using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The only source of randomness in gameplay: one injected instance per level attempt
    /// (GDD D12, §13; invariant I4). Consumption order is fixed, see <see cref="RandomConsumer"/>.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Attempt seed: logged on level start and shown in the cheat panel (D12).</summary>
        int Seed { get; }

        int NextInt(int maxExclusive);

        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>In-place Fisher-Yates; the call order is part of the contract (GDD §5.4).</summary>
        void Shuffle<T>(IList<T> list);
    }
}
