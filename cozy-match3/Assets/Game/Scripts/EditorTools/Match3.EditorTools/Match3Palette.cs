using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// The palette of `docs/art-direction.md` §2.2 in one place. Scene and prefab generators read
    /// it instead of carrying their own literals: a colour spelled out in four methods is a colour
    /// that drifts in three of them.
    /// </summary>
    internal static class Match3Palette
    {
        /// <summary>Linen wall, the upper two thirds of the room and the letterbox fill.</summary>
        internal static readonly Color Wall = Hex("#F3E7D6");

        /// <summary>Floor boards, the lower third of the room.</summary>
        internal static readonly Color Floor = Hex("#E5C39C");

        /// <summary>Outline of drawn objects.</summary>
        internal static readonly Color ObjectOutline = Hex("#5B3A2E");

        /// <summary>Outline of UI panels and buttons.</summary>
        internal static readonly Color PanelOutline = Hex("#8B6B55");

        /// <summary>Fill of HUD plates and popups.</summary>
        internal static readonly Color PanelFill = Hex("#FBF0DE");

        /// <summary>Plate under the grid.</summary>
        internal static readonly Color BoardPanel = Hex("#DFC7A6");

        /// <summary>Board cell, and its alternate for the checker pattern.</summary>
        internal static readonly Color BoardCell = Hex("#EEDFC8");

        internal static readonly Color BoardCellAlternate = Hex("#E7D4B8");

        internal static readonly Color TextPrimary = Hex("#5B3A2E");

        internal static readonly Color TextSecondary = Hex("#8B6B55");

        /// <summary>"Success" accent: the tick of a closed goal.</summary>
        internal static readonly Color Success = Hex("#7FB069");

        /// <summary>"Alarm" accent: the moves counter at five or fewer.</summary>
        internal static readonly Color Danger = Hex("#D9646E");

        /// <summary>"Hint" accent: stroke of the hint marker and the swap arrow.</summary>
        internal static readonly Color HintStroke = Hex("#FFF3DC");

        /// <summary>"Hint" accent: the warm glow around it, and warm FX glows in general.</summary>
        internal static readonly Color HintGlow = Hex("#FFC978");

        /// <summary>
        /// Saturation an FX tint is pushed to. The board is desaturated (the cell sits near 0.18),
        /// so saturation is the axis it cannot answer on.
        /// </summary>
        private const float FlashSaturation = 0.75f;

        /// <summary>
        /// Luminance an FX tint is brought to, against a cell at 0.75. Below it by enough to read,
        /// not so far that a cheerful board grows a set of dark holes.
        /// </summary>
        private const float FlashLuminance = 0.67f;

        /// <summary>
        /// The colour a chip drawn in <paramref name="chip"/> dies under: that hue, saturated and
        /// carried to one shared lightness. Six chip colours span the board's own lightness in
        /// both directions, so tinting a flash with the chip colour itself loses three of them;
        /// one lightness for all six loses none, and the hue still says which chip died.
        /// </summary>
        internal static Color FlashTint(Color chip)
        {
            Color.RGBToHSV(chip, out float hue, out float saturation, out _);
            saturation = Mathf.Max(saturation, FlashSaturation);

            // Luminance is linear in value at a fixed hue and saturation, so one division lands
            // on the target exactly - no search.
            float atFull = Luminance(Color.HSVToRGB(hue, saturation, 1f));
            float value = atFull > 0f ? Mathf.Min(1f, FlashLuminance / atFull) : 1f;

            return Color.HSVToRGB(hue, saturation, value);
        }

        private static float Luminance(Color color)
            => (0.299f * color.r) + (0.587f * color.g) + (0.114f * color.b);

        private static Color Hex(string html)
        {
            if (!ColorUtility.TryParseHtmlString(html, out Color color))
            {
                Debug.LogError("[Match3] Bad palette colour: " + html);
                return Color.magenta;
            }

            return color;
        }
    }
}
