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
