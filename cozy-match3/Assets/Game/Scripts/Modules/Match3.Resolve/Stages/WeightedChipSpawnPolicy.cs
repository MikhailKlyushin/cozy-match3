using System;
using System.Collections.Generic;
using Match3.Core;

namespace Match3.Resolve
{
    /// <summary>
    /// Spawn colours from the per-level <c>spawnWeights</c> (GDD §4.1, §10.1). Exactly one RNG
    /// draw per chip, so the consumption order stays reproducible (D12, §13).
    /// </summary>
    public sealed class WeightedChipSpawnPolicy : IChipSpawnPolicy
    {
        private readonly IRandom _random;
        private readonly int _colorCount;

        /// <summary>Cumulative weight per colour index; empty when the policy is uniform.</summary>
        private readonly int[] _cumulative;

        /// <summary>Zero means uniform over <see cref="_colorCount"/>.</summary>
        private readonly int _totalWeight;

        public WeightedChipSpawnPolicy(IRandom random, int colorCount, IReadOnlyList<ColorWeight> weights)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (colorCount < 1 || colorCount > ChipColors.MaxColorCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(colorCount),
                    "Colour count must be within [1, " + ChipColors.MaxColorCount.ToString() + "].");
            }

            _random = random;
            _colorCount = colorCount;

            if (weights == null || weights.Count == 0)
            {
                _cumulative = Array.Empty<int>();
                _totalWeight = 0;
                return;
            }

            var cumulative = new int[colorCount];
            for (int i = 0; i < weights.Count; i++)
            {
                ColorWeight weight = weights[i];
                int index = ChipColors.ToIndex(weight.Color);
                if (index < 1 || index > colorCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(weights),
                        "Spawn weight colour is outside colorCount: " + index.ToString());
                }

                if (weight.Weight < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(weights), "Spawn weight must be non-negative.");
                }

                cumulative[index - 1] += weight.Weight;
            }

            int total = 0;
            for (int i = 0; i < cumulative.Length; i++)
            {
                total += cumulative[i];
                cumulative[i] = total;
            }

            // Every weight zero would make selection impossible: fall back to uniform (§4.1).
            _cumulative = total == 0 ? Array.Empty<int>() : cumulative;
            _totalWeight = total;
        }

        /// <summary>Weights are per level, so the column does not affect the colour (§10.1).</summary>
        public ChipColor NextColor(int x)
        {
            if (_totalWeight == 0)
            {
                return ChipColors.FromIndex(_random.NextInt(_colorCount) + 1);
            }

            int roll = _random.NextInt(_totalWeight);
            for (int i = 0; i < _cumulative.Length; i++)
            {
                if (roll < _cumulative[i])
                {
                    return ChipColors.FromIndex(i + 1);
                }
            }

            // Unreachable: roll is below the last cumulative weight.
            return ChipColors.FromIndex(_colorCount);
        }
    }
}
