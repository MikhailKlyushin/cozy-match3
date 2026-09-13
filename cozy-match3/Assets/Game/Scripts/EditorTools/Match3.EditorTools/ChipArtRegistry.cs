using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Maps a chip colour index 1..6 to the asset it is drawn with and to a fallback particle
    /// colour. The names are the objects the final art draws (`art-direction.md` §4.1), not the
    /// colours, and they are the single place the profile wiring and the view prefab agree on.
    /// </summary>
    internal static class ChipArtRegistry
    {
        /// <summary>
        /// Fallback tint per colour index, used only while a profile's `_particleColor` is still
        /// empty; an authored colour is never overwritten. The six values differ in lightness as
        /// well as hue, because §17 requires the chips to stay apart in greyscale.
        /// </summary>
        private static readonly Color[] ChipFill =
        {
            new Color(1.00f, 0.86f, 0.72f), // 1 peach, lightest
            new Color(0.47f, 0.68f, 0.92f), // 2 blue, mid
            new Color(0.13f, 0.42f, 0.42f), // 3 teal, dark
            new Color(0.83f, 0.79f, 0.96f), // 4 lavender, light
            new Color(0.40f, 0.72f, 0.44f), // 5 green, mid
            new Color(0.44f, 0.16f, 0.29f), // 6 wine, darkest
        };

        private static readonly string[] ChipAssetNames =
        {
            "T_Chip_Ball_2D",   // 1 pink yarn ball
            "T_Chip_Bell_2D",   // 2 purple collar bell
            "T_Chip_Bowl_2D",   // 3 yellow food bowl
            "T_Chip_Mouse_2D",  // 4 blue plush mouse
            "T_Chip_Paw_2D",    // 5 orange paw
            "T_Chip_Pillow_2D", // 6 green fish pillow
        };

        internal static Color ChipColorOf(int colorIndex) => ChipFill[Mathf.Clamp(colorIndex, 1, 6) - 1];

        internal static string ChipAssetNameOf(int colorIndex) =>
            ChipAssetNames[Mathf.Clamp(colorIndex, 1, 6) - 1];
    }
}
