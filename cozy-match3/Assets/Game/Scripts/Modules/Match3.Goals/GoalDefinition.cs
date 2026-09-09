using System;
using Match3.Core;

namespace Match3.Goals
{
    /// <summary>Immutable goal descriptor from the level config (GDD §8.1).</summary>
    public readonly struct GoalDefinition
    {
        public readonly GoalType Type;
        public readonly int Target;

        /// <summary>Set for <see cref="GoalType.CollectColor"/>.</summary>
        public readonly ChipColor Color;

        /// <summary>Set for <see cref="GoalType.DestroyElement"/>.</summary>
        public readonly string Token;

        /// <summary>Set for <see cref="GoalType.ActivateBooster"/>.</summary>
        public readonly BoosterType Booster;

        private GoalDefinition(GoalType type, int target, ChipColor color, string token, BoosterType booster)
        {
            if (target <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(target), "Goal target must be positive.");
            }

            Type = type;
            Target = target;
            Color = color;
            Token = token;
            Booster = booster;
        }

        public static GoalDefinition CollectColor(ChipColor color, int target)
        {
            if (color == ChipColor.None)
            {
                throw new ArgumentOutOfRangeException(nameof(color), "CollectColor needs a colour index 1-6.");
            }

            return new GoalDefinition(GoalType.CollectColor, target, color, null, BoosterType.None);
        }

        public static GoalDefinition DestroyElement(string token, int target)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("DestroyElement needs an element token.", nameof(token));
            }

            return new GoalDefinition(GoalType.DestroyElement, target, ChipColor.None, token, BoosterType.None);
        }

        public static GoalDefinition DestroyAnyColoredBox(int target)
            => new GoalDefinition(GoalType.DestroyAnyColoredBox, target, ChipColor.None, null, BoosterType.None);

        public static GoalDefinition ActivateBooster(BoosterType booster, int target)
        {
            if (booster == BoosterType.None)
            {
                throw new ArgumentOutOfRangeException(nameof(booster), "ActivateBooster needs a booster type.");
            }

            return new GoalDefinition(GoalType.ActivateBooster, target, ChipColor.None, null, booster);
        }
    }
}
