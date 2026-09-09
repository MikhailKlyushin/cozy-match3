using System;
using Match3.Board;
using Match3.Core;
using Match3.Goals;

namespace Match3.Levels
{
    /// <summary>
    /// Classifies the grid tokens of GDD §10.2. Obstacle tokens are never enumerated here: they
    /// are resolved through <see cref="ElementCatalog"/>, so a new obstacle is a catalogue entry
    /// and not a case in a switch (§10).
    /// </summary>
    internal static class LevelTokens
    {
        internal static bool IsPlayable(string token) => token == ElementTokens.PlayableCell;

        internal static bool IsHole(string token) => token == ElementTokens.Hole;

        internal static bool TryParseFixedChip(string token, out ChipColor color)
        {
            color = ChipColor.None;
            if (token == null || token.Length != ElementTokens.TokenLength)
            {
                return false;
            }

            if (token[0] != ElementTokens.FixedChipPrefix)
            {
                return false;
            }

            int index = token[1] - '0';
            if (index < 1 || index > ChipColors.MaxColorCount)
            {
                return false;
            }

            color = ChipColors.FromIndex(index);
            return true;
        }

        internal static bool TryParseBooster(string token, out BoosterType booster)
        {
            switch (token)
            {
                case ElementTokens.RocketH:
                    booster = BoosterType.RocketH;
                    return true;
                case ElementTokens.RocketV:
                    booster = BoosterType.RocketV;
                    return true;
                case ElementTokens.Bomb:
                    booster = BoosterType.Bomb;
                    return true;
                case ElementTokens.Rainbow:
                    booster = BoosterType.Rainbow;
                    return true;
                case ElementTokens.Airplane:
                    booster = BoosterType.Airplane;
                    return true;
                default:
                    booster = BoosterType.None;
                    return false;
            }
        }

        internal static bool IsKnown(string token, ElementCatalog catalog)
        {
            if (IsPlayable(token) || IsHole(token))
            {
                return true;
            }

            if (TryParseFixedChip(token, out ChipColor _) || TryParseBooster(token, out BoosterType _))
            {
                return true;
            }

            return catalog.TryResolve(token, out ElementDefinition _);
        }

        /// <summary>
        /// GDD §3.4 rule 1: only permanent obstructions block the flow graph — the hole and the
        /// indestructible element. Destructible obstacles are passable, they block the flow only
        /// until they are destroyed.
        /// </summary>
        internal static bool IsPermanentObstruction(string token, ElementCatalog catalog)
        {
            if (IsHole(token))
            {
                return true;
            }

            return catalog.TryResolve(token, out ElementDefinition definition) && definition.IsIndestructible;
        }

        /// <summary>Does an element of this token count towards the goal (GDD §8.1)?</summary>
        internal static bool ServesGoal(string token, in GoalDefinition goal, ElementCatalog catalog)
        {
            switch (goal.Type)
            {
                case GoalType.DestroyElement:
                    return string.Equals(token, goal.Token, StringComparison.Ordinal);
                case GoalType.DestroyAnyColoredBox:
                    return catalog.TryResolve(token, out ElementDefinition definition) && definition.IsColoredBox;
                default:
                    return false;
            }
        }

        internal static ChipColor InitialElementColor(ElementDefinition definition)
        {
            switch (definition.ColorMode)
            {
                case ElementColorMode.Fixed:
                    return definition.FixedColor;
                default:
                    // Cycling boxes get their starting colour from the RNG after board generation (§13).
                    return ChipColor.None;
            }
        }
    }
}
