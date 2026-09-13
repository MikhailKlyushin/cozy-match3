using System;
using System.Collections.Generic;
using Match3.Core;
using Match3.Goals;

namespace Match3.Levels.Authoring
{
    /// <summary>
    /// Turns the authoring asset into the pure DTO the rules work on (A05). The whole point of
    /// the split is that everything downstream stays asset-free and testable without Unity.
    /// </summary>
    public static class LevelConfigConverter
    {
        public static LevelData ToLevelData(LevelConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var goals = new List<GoalDefinition>(config.Goals.Count);
            for (int i = 0; i < config.Goals.Count; i++)
            {
                goals.Add(config.Goals[i].ToDefinition());
            }

            var contents = new List<NestedContent>(config.Contents.Count);
            for (int i = 0; i < config.Contents.Count; i++)
            {
                LevelConfig.NestedContentEntry entry = config.Contents[i];
                contents.Add(new NestedContent(entry.X, entry.Y, entry.Token));
            }

            var spawners = new List<int>(config.Spawners.Count);
            for (int i = 0; i < config.Spawners.Count; i++)
            {
                spawners.Add(config.Spawners[i]);
            }

            var weights = new List<ColorWeight>(config.SpawnWeights.Count);
            for (int i = 0; i < config.SpawnWeights.Count; i++)
            {
                weights.Add(config.SpawnWeights[i].ToColorWeight());
            }

            return new LevelData(
                config.Id,
                config.Width,
                config.Height,
                config.ColorCount,
                config.MoveLimit,
                config.Tier,
                goals,
                config.ReadLayoutRows(),
                contents,
                spawners,
                weights,
                config.HintDelaySeconds);
        }
    }
}
