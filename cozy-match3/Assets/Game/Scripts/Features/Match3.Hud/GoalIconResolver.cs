using System;
using Match3.Content;
using Match3.Goals;
using UnityEngine;

namespace Match3.Hud
{
    /// <summary>
    /// Goal row icon from the visual profiles: chip sprite for a colour goal, booster sprite for a
    /// booster goal, element sprite for an obstacle goal (A12 — the domain knows colour indices).
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

        public GoalIconResolver(
            ChipVisualProfile chips,
            ElementVisualProfile elements,
            string anyColoredBoxToken = CyclingBoxToken)
        {
            _chips = chips != null ? chips : throw new ArgumentNullException(nameof(chips));
            _elements = elements != null ? elements : throw new ArgumentNullException(nameof(elements));
            _anyColoredBoxToken = string.IsNullOrEmpty(anyColoredBoxToken) ? CyclingBoxToken : anyColoredBoxToken;
        }

        public Sprite Resolve(GoalDefinition goal)
        {
            switch (goal.Type)
            {
                case GoalType.CollectColor:
                    return _chips.GetChipSprite(goal.Color);
                case GoalType.DestroyElement:
                    return ElementSprite(goal.Token);
                case GoalType.DestroyAnyColoredBox:
                    return ElementSprite(_anyColoredBoxToken);
                case GoalType.ActivateBooster:
                    return _chips.GetBoosterSprite(goal.Booster);
                default:
                    return null;
            }
        }

        /// <summary>Full-health stage: the icon shows an intact obstacle, never a damaged one.</summary>
        private Sprite ElementSprite(string token)
        {
            return _elements.TryGet(token, out ElementVisualProfile.ElementVisual visual)
                ? visual.SpriteForHealth(1, 1)
                : null;
        }
    }
}
