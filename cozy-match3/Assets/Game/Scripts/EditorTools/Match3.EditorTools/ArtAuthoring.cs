using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Writes the placeholder sprite set to disk and configures its importers. Art is generated,
    /// not authored, so a clean checkout has readable chips without binary assets in the diff.
    /// A file that already exists is left alone: once final art lands under the same name, a
    /// routine regeneration of levels or scenes must not repaint it.
    /// </summary>
    internal static class ArtAuthoring
    {
        internal const string GameplayArtFolder = "Assets/Game/Content/Gameplay/Art";
        internal const string HudArtFolder = "Assets/Game/Content/Hud/Art";

        [MenuItem("Match3/Authoring/Generate Placeholder Art")]
        public static void GenerateArt()
        {
            Generate(overwrite: false);
        }

        [MenuItem("Match3/Authoring/Generate Placeholder Art (Force Overwrite)")]
        public static void GenerateArtForced()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Overwrite art with placeholders?",
                "Every T_*_2D.png the generator owns will be replaced by a placeholder. Final art "
                + "under those names is lost and only git can bring it back.",
                "Overwrite",
                "Cancel");

            if (confirmed)
            {
                Generate(overwrite: true);
            }
        }

        private static void Generate(bool overwrite)
        {
            SceneAuthoring.EnsureFolder(GameplayArtFolder);
            SceneAuthoring.EnsureFolder(HudArtFolder);

            int written = 0;
            int skipped = 0;

            for (int colorIndex = 1; colorIndex <= 6; colorIndex++)
            {
                Save(ProceduralArt.CreateChip(colorIndex), GameplayArtFolder, overwrite, ref written, ref skipped);
            }

            Save(ProceduralArt.CreateRocket(horizontal: true), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateRocket(horizontal: false), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateBomb(), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateRainbow(), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateAirplane(), GameplayArtFolder, overwrite, ref written, ref skipped);

            // One base per damage stage: §7.1 requires the visual to change on every hit point lost.
            for (int stage = 0; stage < 3; stage++)
            {
                Save(
                    ProceduralArt.CreateBox(stage, "T_Element_BoxBase_S" + stage.ToString() + "_2D"),
                    GameplayArtFolder,
                    overwrite,
                    ref written,
                    ref skipped);
            }

            Save(ProceduralArt.CreateBoxBow(), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateBoxPip(), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateBlocker(), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateCellTile(), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreateGlow(), GameplayArtFolder, overwrite, ref written, ref skipped);
            Save(ProceduralArt.CreatePanel(), HudArtFolder, overwrite, ref written, ref skipped);

            ApplyPanelBorder();

            AssetDatabase.Refresh();
            Debug.Log("[Match3] Placeholder art: " + written.ToString() + " written, "
                + skipped.ToString() + " kept");
        }

        private static void Save(Texture2D texture, string folder, bool overwrite, ref int written, ref int skipped)
        {
            string path = folder + "/" + texture.name + ".png";

            if (!overwrite && File.Exists(path))
            {
                Object.DestroyImmediate(texture);
                skipped++;
                return;
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(path);
            written++;
        }

        /// <summary>
        /// The popup panel is the one sprite here that gets stretched far past its authored size,
        /// so it needs a nine-slice border or its rounded corners turn into an oval. 24 px on the
        /// 128 px panel covers its 18 px corner radius plus the transparent margin around it.
        /// Applied whether or not the file was rewritten: the border follows from how the sprite
        /// is drawn, not from its pixels.
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

        private static void ConfigureSpriteImporter(string path)
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
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }
}
