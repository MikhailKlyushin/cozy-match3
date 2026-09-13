namespace Match3.Resolve
{
    /// <summary>
    /// The level parameters the resolve pipeline needs. A plain value object rather than
    /// LevelData, because Match3.Resolve does not reference Match3.Levels (§3.1).
    /// </summary>
    public sealed class LevelRules
    {
        public LevelRules(int levelId, int colorCount, int moveLimit, float hintDelaySeconds)
        {
            LevelId = levelId;
            ColorCount = colorCount;
            MoveLimit = moveLimit;
            HintDelaySeconds = hintDelaySeconds;
        }

        public int LevelId { get; }

        /// <summary>Drives the cx colour cycle: color = (color mod colorCount) + 1 (§7.2).</summary>
        public int ColorCount { get; }

        public int MoveLimit { get; }

        /// <summary>0 disables the hint entirely (§5.5).</summary>
        public float HintDelaySeconds { get; }
    }
}
