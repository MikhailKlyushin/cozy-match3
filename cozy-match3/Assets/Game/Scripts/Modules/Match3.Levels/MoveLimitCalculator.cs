using System;
using Match3.Board;
using Match3.Core;
using Match3.Goals;

namespace Match3.Levels
{
    /// <summary>
    /// GDD §9.3: work = max(colour goals: target x cost) + sum(other goals: target x cost),
    /// moves = ceil(work / thr x multiplier). A validation tool that cross-checks the authored
    /// move limit — never runtime logic (D13).
    /// </summary>
    public static class MoveLimitCalculator
    {
        private static readonly char[] Separators = { ' ', '\t' };

        /// <summary>The §7.2 catalogue is enough for the shipped set; custom element assets are T14's concern.</summary>
        private static readonly ElementCatalog BuiltInCatalog = BuiltInElementCatalog.Create();

        private const float BoxCostPerLife = 4f;
        private const float NestedElementCost = 5f;
        private const float CyclingBoxCost = 6f;
        private const float RocketActivationCost = 14f;
        private const float BombActivationCost = 22f;
        private const float AirplaneActivationCost = 24f;
        private const float RainbowActivationCost = 36f;

        public static int Compute(LevelData level, float throughput)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (throughput <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(throughput), "Throughput must be positive (§9.3 step 2).");
            }

            double colorWork = 0d;
            double otherWork = 0d;

            for (int i = 0; i < level.Goals.Count; i++)
            {
                GoalDefinition goal = level.Goals[i];
                double work = WorkFor(level, goal);

                // Colour goals run in parallel: one clear serves all of them at once (D13).
                if (goal.Type == GoalType.CollectColor)
                {
                    if (work > colorWork)
                    {
                        colorWork = work;
                    }

                    continue;
                }

                otherWork += work;
            }

            double moves = (colorWork + otherWork) / throughput * MultiplierFor(level.Tier);
            return (int)Math.Ceiling(moves);
        }

        public static float CostPerUnit(in GoalDefinition goal, int colorCount, ElementCatalog catalog)
        {
            switch (goal.Type)
            {
                case GoalType.CollectColor:
                    // The share of one colour among clears is 1 / colorCount.
                    return colorCount;
                case GoalType.DestroyAnyColoredBox:
                    return ColoredBoxCost(colorCount);
                case GoalType.ActivateBooster:
                    return BoosterActivationCost(goal.Booster);
                case GoalType.DestroyElement:
                    return ElementCost(goal.Token, colorCount, catalog);
                default:
                    return 0f;
            }
        }

        public static float MultiplierFor(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy:
                    return 1.30f;
                case DifficultyTier.Medium:
                    return 1.10f;
                case DifficultyTier.Hard:
                    return 0.95f;
                case DifficultyTier.SuperHard:
                    return 0.85f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), "Unknown difficulty tier: " + tier);
            }
        }

        /// <summary>Nested units cost more than outer ones, so they are counted separately (§9.3).</summary>
        private static double WorkFor(LevelData level, in GoalDefinition goal)
        {
            double cost = CostPerUnit(goal, level.ColorCount, BuiltInCatalog);
            if (goal.Type != GoalType.DestroyElement && goal.Type != GoalType.DestroyAnyColoredBox)
            {
                return goal.Target * cost;
            }

            int outerUnits = Math.Min(goal.Target, CountInLayout(level, goal));
            int nestedUnits = Math.Min(goal.Target - outerUnits, CountInContents(level, goal));

            // Units the board cannot supply at all: the validator reports them (§3.4 rule 3).
            int missingUnits = goal.Target - outerUnits - nestedUnits;
            return (outerUnits + missingUnits) * cost + nestedUnits * NestedElementCost;
        }

        private static int CountInLayout(LevelData level, in GoalDefinition goal)
        {
            int count = 0;
            for (int row = 0; row < level.LayoutRows.Count; row++)
            {
                string line = level.LayoutRows[row];
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                string[] tokens = line.Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (LevelTokens.ServesGoal(tokens[i], goal, BuiltInCatalog))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountInContents(LevelData level, in GoalDefinition goal)
        {
            int count = 0;
            for (int i = 0; i < level.Contents.Count; i++)
            {
                if (LevelTokens.ServesGoal(level.Contents[i].Token, goal, BuiltInCatalog))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Read off the §7.1 axes, so a new obstacle needs no change here (§10).</summary>
        private static float ElementCost(string token, int colorCount, ElementCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!catalog.TryResolve(token, out ElementDefinition definition))
            {
                // Unknown or non-countable target: the validator reports it as an error.
                return 0f;
            }

            if (definition.GoalRole == GoalRole.NotCountable || definition.IsIndestructible)
            {
                return 0f;
            }

            if (definition.ColorMode == ElementColorMode.Cycling)
            {
                return CyclingBoxCost;
            }

            if (definition.IsColoredBox)
            {
                return ColoredBoxCost(colorCount);
            }

            return BoxCostPerLife * definition.MaxHealth;
        }

        /// <summary>A specific colour is needed but it can be aimed for, hence the divisor 2 (§9.3).</summary>
        private static float ColoredBoxCost(int colorCount) => BoxCostPerLife * colorCount / 2f;

        private static float BoosterActivationCost(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.RocketH:
                case BoosterType.RocketV:
                    return RocketActivationCost;
                case BoosterType.Bomb:
                    return BombActivationCost;
                case BoosterType.Airplane:
                    return AirplaneActivationCost;
                case BoosterType.Rainbow:
                    return RainbowActivationCost;
                default:
                    return 0f;
            }
        }
    }
}
