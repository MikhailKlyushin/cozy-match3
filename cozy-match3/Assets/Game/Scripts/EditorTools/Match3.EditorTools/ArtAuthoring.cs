using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Writes the placeholder sprite set to disk and configures its importers. Art is generated,
    /// not authored, so a clean checkout has readable chips without binary assets in the diff.
    /// </summary>
    internal static class ArtAuthoring
    {
        internal const string GameplayArtFolder = "Assets/Game/Content/Gameplay/Art";
        internal const string HudArtFolder = "Assets/Game/Content/Hud/Art";

        [MenuItem("Match3/Authoring/Generate Placeholder Art")]
        public static void GenerateArt()
        {
            SceneAuthoring.EnsureFolder(GameplayArtFolder);
            SceneAuthoring.EnsureFolder(HudArtFolder);

            int written = 0;

            for (int colorIndex = 1; colorIndex <= 6; colorIndex++)
            {
                written += Save(ProceduralArt.CreateChip(colorIndex), GameplayArtFolder);
            }

            written += Save(ProceduralArt.CreateRocket(horizontal: true), GameplayArtFolder);
            written += Save(ProceduralArt.CreateRocket(horizontal: false), GameplayArtFolder);
            written += Save(ProceduralArt.CreateBomb(), GameplayArtFolder);
            written += Save(ProceduralArt.CreateRainbow(), GameplayArtFolder);
            written += Save(ProceduralArt.CreateAirplane(), GameplayArtFolder);

            // One base per damage stage: §7.1 requires the visual to change on every hit point lost.
            for (int stage = 0; stage < 3; stage++)
            {
                written += Save(
                    ProceduralArt.CreateBox(stage, "T_Element_BoxBase_S" + stage.ToString() + "_2D"),
                    GameplayArtFolder);
            }

            written += Save(ProceduralArt.CreateBoxBow(), GameplayArtFolder);
            written += Save(ProceduralArt.CreateBoxPip(), GameplayArtFolder);
            written += Save(ProceduralArt.CreateBlocker(), GameplayArtFolder);
            written += Save(ProceduralArt.CreateCellTile(), GameplayArtFolder);
            written += Save(ProceduralArt.CreateGlow(), GameplayArtFolder);
            written += Save(ProceduralArt.CreatePanel(), HudArtFolder);

            AssetDatabase.Refresh();
            Debug.Log("[Match3] Placeholder art generated: " + written.ToString() + " sprites");
        }

        private static int Save(Texture2D texture, string folder)
        {
            string path = folder + "/" + texture.name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(path);
            return 1;
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
