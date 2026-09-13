using Match3.Core;
using Match3.Goals;
using Match3.Levels;

namespace Match3.Tests.EditMode.Levels
{
    /// <summary>
    /// Levels quoted verbatim from the GDD (§10.3, §10.4). They are the reference cases for the
    /// validator and for the move-limit formula, and they stay here rather than in the module so
    /// Match3.Levels never depends on the shipped assets.
    /// </summary>
    internal static class GddLevels
    {
        /// <summary>Walls split the board into regions, §9.3 step 2.</summary>
        internal const float Level5Throughput = 3.4f;

        /// <summary>Blocked 15 % or less, §9.3 step 2.</summary>
        internal const float Level9Throughput = 4.2f;

        internal const float Level11Throughput = 4.2f;

        /// <summary>GDD §10.3, the fully worked example.</summary>
        internal static LevelData Level5()
        {
            return TestLevel.From(
                @"
                __ .. .. .. .. .. .. __
                .. .. .. .. .. .. .. ..
                .. .. ## .. .. ## .. ..
                .. bx ## .. .. ## bx ..
                .. bx ## bx bx ## bx ..
                .. bx ## bx bx ## bx ..
                .. .. ## .. .. ## .. ..
                .. .. .. .. .. .. .. ..",
                colorCount: 4,
                moveLimit: 26,
                tier: DifficultyTier.Medium,
                goals: new[]
                {
                    GoalDefinition.DestroyElement("bx", 10),
                    GoalDefinition.CollectColor(ChipColor.C2, 10)
                },
                spawners: new[] { 1, 2, 3, 4, 5, 6 },
                id: 5);
        }

        /// <summary>GDD §10.4: five outer boxes, each holding one more (nested cost 5, work 45).</summary>
        internal static LevelData Level9()
        {
            return TestLevel.From(
                @"
                .. .. .. .. .. .. .. ..
                .. .. .. .. .. .. .. ..
                .. .. bx .. .. bx .. ..
                .. .. .. .. .. .. .. ..
                .. .. .. bx .. .. .. ..
                .. .. .. .. .. .. .. ..
                .. .. bx .. .. bx .. ..
                .. .. .. .. .. .. .. ..",
                colorCount: 5,
                moveLimit: 14,
                tier: DifficultyTier.Easy,
                goals: new[] { GoalDefinition.DestroyElement("bx", 10) },
                contents: new[]
                {
                    new NestedContent(2, 5, "bx"),
                    new NestedContent(5, 5, "bx"),
                    new NestedContent(3, 3, "bx"),
                    new NestedContent(2, 1, "bx"),
                    new NestedContent(5, 1, "bx")
                },
                id: 9);
        }

        /// <summary>GDD §10.4: the one level whose move limit is raised by hand (formula 17, config 18).</summary>
        internal static LevelData Level11()
        {
            return TestLevel.From(
                @"
                .. .. .. .. .. .. .. ..
                .. .. .. .. .. .. .. ..
                ## .. .. .. .. .. .. ##
                ## .. .. .. .. .. .. ##
                .. .. .. .. .. .. .. ..
                .. .. ## .. .. ## .. ..
                .. .. ## .. .. ## .. ..
                .. .. .. .. .. .. .. ..",
                colorCount: 5,
                moveLimit: 18,
                tier: DifficultyTier.Medium,
                goals: new[]
                {
                    // Orientation is irrelevant to the goal cost: both rockets cost 14 (§9.3).
                    GoalDefinition.ActivateBooster(BoosterType.RocketH, 3),
                    GoalDefinition.ActivateBooster(BoosterType.Bomb, 1)
                },
                id: 11);
        }
    }
}
