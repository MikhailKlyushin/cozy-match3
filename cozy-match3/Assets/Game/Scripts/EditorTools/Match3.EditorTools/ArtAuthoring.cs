using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Keeps the import settings of the two art folders under code control. Pixels belong to the
    /// author, but import settings follow from how a sprite is drawn, so they are applied from
    /// here rather than left in hand-edited .meta files where a reimport can quietly lose them.
    /// </summary>
    internal static class ArtAuthoring
    {
        internal const string GameplayArtFolder = "Assets/Game/Content/Gameplay/Art";
        internal const string HudArtFolder = "Assets/Game/Content/Hud/Art";

        /// <summary>
        /// Ceiling, not a target: an asset already imported smaller keeps its size. Authored
        /// masters are 256 and the build ships them at 128 - the fallback of `art-direction.md`
        /// §3.2, applied. Raising anything back is a decision that needs a weight measurement from
        /// a real build (T25/T32), not a guess.
        /// </summary>
        private const int GameplayMaxTextureSize = 128;

        /// <summary>The room background is drawn full-screen and is never atlased.</summary>
        private const int HudMaxTextureSize = 2048;

        /// <summary>
        /// Brings every sprite in the two art folders to the settings §3.2 asks for. Idempotent,
        /// and it never touches a single pixel.
        /// </summary>
        [MenuItem("Match3/Authoring/Apply Texture Import Settings")]
        public static void ApplyImportSettings()
        {
            int gameplay = ApplyFolder(GameplayArtFolder, mipmaps: true, GameplayMaxTextureSize);
            int hud = ApplyFolder(HudArtFolder, mipmaps: false, HudMaxTextureSize);
            ApplyPanelBorder();

            Debug.Log("[Match3] Texture import settings applied: " + gameplay.ToString()
                + " gameplay, " + hud.ToString() + " HUD");
        }

        private static int ApplyFolder(string folder, bool mipmaps, int maxTextureSize)
        {
            if (!Directory.Exists(folder))
            {
                return 0;
            }

            int count = 0;
            foreach (string path in Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
            {
                Configure(path.Replace('\\', '/'), mipmaps, maxTextureSize);
                count++;
            }

            return count;
        }

        /// <summary>
        /// The popup panel is the one sprite here that gets stretched far past its authored size,
        /// so it needs a nine-slice border or its rounded corners turn into an oval. 24 px on the
        /// 128 px panel covers its 18 px corner radius plus the transparent margin around it.
        /// </summary>
        private static void ApplyPanelBorder()
        {
            const float border = 24f;
            string path = HudArtFolder + "/T_Ui_Panel_2D.png";

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Match3] No TextureImporter for " + path);
                return;
            }

            var wanted = new Vector4(border, border, border, border);
            if (importer.spriteBorder == wanted)
            {
                return;
            }

            importer.spriteBorder = wanted;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Mip-maps are on for board sprites and off for HUD sprites, and that is not symmetry for
        /// its own sake: a 9x9 board on a laptop window minifies a cell to roughly a third of its
        /// texels, and without mips the high-frequency detail boils on every fall and cascade
        /// (§3.2). The HUD is drawn at its own size and would only pay the 33 % memory for nothing.
        /// </summary>
        private static void Configure(string path, bool mipmaps, int maxTextureSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Match3] No TextureImporter for " + path);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = mipmaps;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.textureCompression = AtlasAuthoring.IsAtlasMember(path)
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;

            // A ceiling: whoever imported an asset smaller did it for the build weight, and a
            // blanket pass has no measurement with which to argue.
            if (importer.maxTextureSize > maxTextureSize)
            {
                importer.maxTextureSize = maxTextureSize;
            }

            importer.SaveAndReimport();
        }
    }
}
