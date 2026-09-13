using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Configures the importers of the third-party art packs (UI and VFX). The packs arrive as
    /// plain textures, which uGUI cannot draw: every PNG has to become a sprite, and the stretched
    /// widgets need a nine-slice border or their corners smear. Settings live here rather than in
    /// hand-written .meta files so a reimport cannot quietly lose them.
    /// </summary>
    internal static class ArtPackAuthoring
    {
        internal const string UiPackFolder = "Assets/Game/Content/Hud/UI_Pack";
        internal const string VfxPackFolder = "Assets/Game/Content/Hud/VFX_Pack";

        /// <summary>Pack sheets that document the pack itself and never ship in a build.</summary>
        private static readonly string[] SkippedFiles = { "Preview.png", "Sample.png" };

        [MenuItem("Match3/Authoring/Configure Art Pack Importers")]
        public static void ConfigurePacks()
        {
            int uiCount = ConfigureFolder(UiPackFolder, isUi: true);
            int vfxCount = ConfigureFolder(VfxPackFolder, isUi: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Art pack importers configured: " + uiCount.ToString() + " UI, "
                + vfxCount.ToString() + " VFX sprites");
        }

        private static int ConfigureFolder(string folder, bool isUi)
        {
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning("[Match3] Art pack folder missing: " + folder);
                return 0;
            }

            List<string> paths = new List<string>();
            foreach (string path in Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                if (!IsSkipped(assetPath))
                {
                    paths.Add(assetPath);
                }
            }

            paths.Sort(System.StringComparer.Ordinal);

            int configured = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < paths.Count; i++)
                {
                    if (Configure(paths[i], isUi))
                    {
                        configured++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return configured;
        }

        private static bool IsSkipped(string assetPath)
        {
            string fileName = Path.GetFileName(assetPath);
            for (int i = 0; i < SkippedFiles.Length; i++)
            {
                if (string.Equals(fileName, SkippedFiles[i], System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Configure(string assetPath, bool isUi)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[Match3] No TextureImporter for " + assetPath);
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.textureCompression = TextureImporterCompression.Compressed;

            // HUD sits in a screen-space overlay canvas and the glows are drawn far above their
            // texel density: neither is minified enough to need mips, and both would pay for them.
            importer.mipmapEnabled = false;
            importer.maxTextureSize = isUi ? 512 : 256;
            importer.spriteBorder = isUi ? BorderFor(assetPath) : Vector4.zero;

            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// Nine-slice border in texture pixels. The packs ship every widget twice — `Default` and
        /// a `Double` folder at exactly 2x — so one measurement scales to both. Values come from
        /// the sprites themselves: an 8 px corner radius over a 4 px outline, plus the 16 px lip
        /// the `depth` variants carry along their bottom edge.
        /// </summary>
        private static Vector4 BorderFor(string assetPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            bool isDouble = assetPath.Contains("/Double/");
            float scale = isDouble ? 1f : 0.5f;

            if (fileName.StartsWith("button_rectangle", System.StringComparison.Ordinal)
                || fileName.StartsWith("button_square", System.StringComparison.Ordinal))
            {
                float side = 14f * scale;
                float bottom = fileName.Contains("_depth_") ? 22f * scale : side;
                return new Vector4(side, bottom, side, side);
            }

            // Sliders are pills: only the round caps must stay unstretched.
            if (fileName.StartsWith("slide_horizontal", System.StringComparison.Ordinal))
            {
                float cap = 16f * scale;
                return new Vector4(cap, 0f, cap, 0f);
            }

            if (fileName.StartsWith("slide_vertical", System.StringComparison.Ordinal))
            {
                float cap = 16f * scale;
                return new Vector4(0f, cap, 0f, cap);
            }

            return Vector4.zero;
        }
    }
}
