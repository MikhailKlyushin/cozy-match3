using System;
using Match3.Content;
using Match3.Core;
using Match3.Goals;
using UnityEngine;

namespace Match3.Hud
{
    /// <summary>
    /// Goal row icon from the visual profiles: chip sprite for a colour goal, booster sprite for a
    /// booster goal, element sprite for an obstacle goal (A12 — the domain knows colour indices).
    /// A coloured box adds the tinted bow the board draws, without which it would be
    /// indistinguishable from a plain box - they share the base sprite.
    /// </summary>
    public sealed class GoalIconResolver
    {
        private readonly ChipVisualProfile _chips;
        private readonly ElementVisualProfile _elements;
        private readonly string _anyColoredBoxToken;

        /// <summary>
        /// Stand-in icon for DestroyAnyColoredBox, which carries no token (§8.1). Spelled out here
        /// because Match3.Hud may not reference Match3.Board and its ElementTokens (§3.1).
        /// </summary>
        public const string CyclingBoxToken = "cx";

        /// <summary>Prefix of the fixed-colour box tokens c1-c6; see <see cref="CyclingBoxToken"/>.</summary>
        private const char ColoredBoxPrefix = 'c';

        public GoalIconResolver(
            ChipVisualProfile chips,
            ElementVisualProfile elements,
            string anyColoredBoxToken = CyclingBoxToken)
        {
            _chips = chips != null ? chips : throw new ArgumentNullException(nameof(chips));
            _elements = elements != null ? elements : throw new ArgumentNullException(nameof(elements));
            _anyColoredBoxToken = string.IsNullOrEmpty(anyColoredBoxToken) ? CyclingBoxToken : anyColoredBoxToken;
        }

        public GoalIcon Resolve(GoalDefinition goal)
        {
            switch (goal.Type)
            {
                case GoalType.CollectColor:
                    return GoalIcon.Flat(_chips.GetChipSprite(goal.Color));
                case GoalType.DestroyElement:
                    return ElementIcon(goal.Token);
                case GoalType.DestroyAnyColoredBox:
                    return ElementIcon(_anyColoredBoxToken);
                case GoalType.ActivateBooster:
                    return GoalIcon.Flat(_chips.GetBoosterSprite(goal.Booster));
                default:
                    return default;
            }
        }

        /// <summary>
        /// Full-health stage: the icon shows an intact obstacle, never a damaged one. The overlays
        /// follow the profile flags, so the goal row and the board agree on what carries a bow.
        /// </summary>
        private GoalIcon ElementIcon(string token)
        {
            if (!_elements.TryGet(token, out ElementVisualProfile.ElementVisual visual))
            {
                return default;
            }

            Sprite baseSprite = visual.SpriteForHealth(1, 1);
            if (!visual.ShowBow)
            {
                return GoalIcon.Flat(baseSprite);
            }

            ChipColor color = BoxColor(token);
            return new GoalIcon(
                baseSprite,
                _elements.BowOverlay,
                visual.ShowNextColorPip ? _elements.PipOverlay : null,
                color != ChipColor.None ? _chips.GetParticleColor(color) : Color.white);
        }

        /// <summary>
        /// Colour of a fixed-colour box token (c1-c6). None for cx, whose colour is runtime state
        /// (§7.2): its goal keeps the untinted bow and the pip, the way the box reads on the board.
        /// </summary>
        private static ChipColor BoxColor(string token)
        {
            if (token == null || token.Length != 2 || token[0] != ColoredBoxPrefix)
            {
                return ChipColor.None;
            }

            int index = token[1] - '0';
            return index >= 1 && index <= ChipColors.MaxColorCount ? ChipColors.FromIndex(index) : ChipColor.None;
        }
    }
}
