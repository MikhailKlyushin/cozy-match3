using UnityEngine;

namespace Match3.Hud
{
    /// <summary>
    /// Layers of one goal icon. A coloured box shares its base sprite with the plain ones - the
    /// colour lives in a tinted bow overlay, exactly as it does on the board. Drawn
    /// as a single sprite, every box goal would show the same picture.
    /// </summary>
    public readonly struct GoalIcon
    {
        public readonly Sprite Base;

        /// <summary>Ribbon tinted with <see cref="Tint"/>; null for anything but a coloured box.</summary>
        public readonly Sprite Bow;

        /// <summary>Next-colour pip of the cx box; null for every other goal.</summary>
        public readonly Sprite Pip;

        /// <summary>Bow tint. White when the goal names no colour.</summary>
        public readonly Color Tint;

        public GoalIcon(Sprite baseSprite, Sprite bow, Sprite pip, Color tint)
        {
            Base = baseSprite;
            Bow = bow;
            Pip = pip;
            Tint = tint;
        }

        /// <summary>Single-sprite icon: a chip, a booster, an uncoloured obstacle.</summary>
        public static GoalIcon Flat(Sprite sprite) => new GoalIcon(sprite, null, null, Color.white);
    }
}
