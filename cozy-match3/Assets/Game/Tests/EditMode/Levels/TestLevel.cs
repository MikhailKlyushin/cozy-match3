using System;
using System.Collections.Generic;
using Match3.Core;
using Match3.Goals;
using Match3.Levels;

namespace Match3.Tests.EditMode.Levels
{
    /// <summary>
    /// Builds a <see cref="LevelData"/> from a string layout so level tests need neither a
    /// ScriptableObject nor a scene. Rows stay top-down exactly like a config: the first line is
    /// y = height - 1 and only <see cref="LayoutParser"/> flips them (D01).
    /// </summary>
    public static class TestLevel
    {
        private static readonly char[] Separators = { ' ', '\t' };

        /// <summary>
        /// <paramref name="width"/> and <paramref name="height"/> default to the layout's own
        /// size; pass them explicitly to author a size mismatch for the validator.
        /// </summary>
        public static LevelData From(
            string layout,
            int colorCount = 4,
            int moveLimit = 20,
            DifficultyTier tier = DifficultyTier.Easy,
            IReadOnlyList<GoalDefinition> goals = null,
            IReadOnlyList<NestedContent> contents = null,
            IReadOnlyList<int> spawners = null,
            IReadOnlyList<ColorWeight> spawnWeights = null,
            float hintDelaySeconds = 5f,
            int id = 1,
            int width = 0,
            int height = 0)
        {
            List<string> rows = ReadRows(layout);

            return new LevelData(
                id,
                width > 0 ? width : CountTokens(rows[0]),
                height > 0 ? height : rows.Count,
                colorCount,
                moveLimit,
                tier,
                goals,
                rows,
                contents,
                spawners,
                spawnWeights,
                hintDelaySeconds);
        }

        /// <summary>Trimmed non-empty rows, same as <c>LevelConfig.ReadLayoutRows</c>.</summary>
        public static List<string> ReadRows(string layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var rows = new List<string>(9);
            string[] lines = layout.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length > 0)
                {
                    rows.Add(line);
                }
            }

            if (rows.Count == 0)
            {
                throw new ArgumentException("Layout is empty.", nameof(layout));
            }

            return rows;
        }

        private static int CountTokens(string row) => row.Split(Separators, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
