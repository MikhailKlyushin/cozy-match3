using System;
using System.Collections.Generic;
using Match3.Core;
using Match3.Goals;

namespace Match3.Levels
{
    /// <summary>
    /// Pure level DTO (GDD §10.1, A05): built from a <c>LevelConfig</c> asset in the game and
    /// from a string layout in tests, so the parser, validator and builder stay asset-free.
    /// Values are taken as given — out-of-range data is reported by <see cref="LevelValidator"/>,
    /// not thrown here.
    /// </summary>
    public sealed class LevelData
    {
        public LevelData(
            int id,
            int width,
            int height,
            int colorCount,
            int moveLimit,
            DifficultyTier tier,
            IReadOnlyList<GoalDefinition> goals,
            IReadOnlyList<string> layoutRows,
            IReadOnlyList<NestedContent> contents,
            IReadOnlyList<int> spawners,
            IReadOnlyList<ColorWeight> spawnWeights,
            float hintDelaySeconds)
        {
            Id = id;
            Width = width;
            Height = height;
            ColorCount = colorCount;
            MoveLimit = moveLimit;
            Tier = tier;
            Goals = Copy(goals);
            LayoutRows = Copy(layoutRows);
            Contents = Copy(contents);
            Spawners = Copy(spawners);
            SpawnWeights = Copy(spawnWeights);
            HintDelaySeconds = hintDelaySeconds;
        }

        /// <summary>Level number, also the progression order (GDD §10.1).</summary>
        public int Id { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>GDD §4.1: valid range [4, 6].</summary>
        public int ColorCount { get; }

        /// <summary>Derived from the goal workload (GDD §9.3, D13).</summary>
        public int MoveLimit { get; }

        public DifficultyTier Tier { get; }

        /// <summary>Order is both the display order and the targeting priority (GDD §8.2).</summary>
        public IReadOnlyList<GoalDefinition> Goals { get; }

        /// <summary>Text rows, still top-down: the first row is y = height - 1 (D01).</summary>
        public IReadOnlyList<string> LayoutRows { get; }

        public IReadOnlyList<NestedContent> Contents { get; }

        /// <summary>Empty means the GDD §3.3 default.</summary>
        public IReadOnlyList<int> Spawners { get; }

        /// <summary>Empty means uniform (GDD §4.1).</summary>
        public IReadOnlyList<ColorWeight> SpawnWeights { get; }

        /// <summary>GDD §5.5; 0 disables the hint.</summary>
        public float HintDelaySeconds { get; }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<T>();
            }

            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
