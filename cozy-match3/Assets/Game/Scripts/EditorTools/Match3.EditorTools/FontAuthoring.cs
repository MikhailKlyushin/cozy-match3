using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Match3.EditorTools
{
    /// <summary>
    /// Bakes the TMP font asset the whole UI renders with. Generated rather than baked by hand in
    /// the Font Asset Creator so a clean checkout reproduces the same atlas and the character set
    /// stays reviewable in a diff.
    /// </summary>
    internal static class FontAuthoring
    {
        internal const string FontFolder = "Assets/Game/Content/Hud/Fonts";
        internal const string UiFontPath = FontFolder + "/F_Nunito_ExtraBold_SDF.asset";

        private const string AssetName = "F_Nunito_ExtraBold_SDF";
        private const string SourceFontPath = FontFolder + "/Nunito/static/Nunito-ExtraBold.ttf";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // Padding is 10% of the sampling size; every font asset in the project has to keep that
        // ratio or the same line renders at two different stroke weights.
        private const int SamplingPointSize = 60;
        private const int AtlasPadding = 6;

        // One atlas, never two: multi-atlas support is off, so an overflow is a loud error here
        // instead of a silent second texture and a second draw call in the build.
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        private const string Digits = "0123456789";
        private const string LatinUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string LatinLower = "abcdefghijklmnopqrstuvwxyz";
        private const string CyrillicUpper = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
        private const string CyrillicLower = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        private const string Punctuation = " !\"#$%&'()*+,-./:;<=>?@[\\]^_{|}~«»—–…№";

        [MenuItem("Match3/Authoring/Generate Font Asset")]
        public static void GenerateFontAsset()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError("[Match3] Missing source font " + SourceFontPath);
                return;
            }

            // Created dynamic so TryAddCharacters can rasterise, then switched to static below.
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                source,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDF16,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);

            if (asset == null)
            {
                Debug.LogError("[Match3] TMP could not load the face of " + SourceFontPath
                    + " - check that Include Font Data is on in its import settings.");
                return;
            }

            asset.name = AssetName;

            string characters = Digits + LatinUpper + LatinLower
                + CyrillicUpper + CyrillicLower + Punctuation;

            if (!asset.TryAddCharacters(characters, out string missing))
            {
                Debug.LogError("[Match3] Font atlas is short of room or glyphs: '" + missing
                    + "'. Raise the atlas size or lower the sampling point size.");
            }

            // Static drops the runtime reference to the .ttf, so the player ships the baked atlas
            // and not the 250 KB font file.
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.ReadFontAssetDefinition();

            SceneAuthoring.EnsureFolder(FontFolder);
            AssetDatabase.CreateAsset(asset, UiFontPath);

            Texture2D atlas = asset.atlasTextures[0];
            atlas.name = AssetName + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);

            Material material = asset.material;
            material.name = AssetName + " Material";
            AssetDatabase.AddObjectToAsset(material, asset);

            MakeAtlasUnreadable(atlas);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetProjectDefault(asset);

            Debug.Log("[Match3] Font asset generated: " + UiFontPath + " ("
                + asset.characterTable.Count + " glyphs, "
                + AtlasWidth + "x" + AtlasHeight + ")");
        }

        internal static TMP_FontAsset LoadUiFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontPath);
            if (font == null)
            {
                Debug.LogError("[Match3] Missing " + UiFontPath
                    + " - run Match3/Authoring/Generate Font Asset first");
            }

            return font;
        }

        /// <summary>
        /// A static atlas is never sampled on the CPU, so the readable copy is a megabyte of RAM
        /// for nothing. TMP's own utility for this is internal to its editor assembly.
        /// </summary>
        private static void MakeAtlasUnreadable(Texture2D atlas)
        {
            var so = new SerializedObject(atlas);
            SerializedProperty readable = so.FindProperty("m_IsReadable");
            if (readable == null)
            {
                Debug.LogWarning("[Match3] Could not clear m_IsReadable on the font atlas.");
                return;
            }

            readable.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Repoints TMP's default font asset. LiberationSans, the package default, has no
        /// Cyrillic at all, so any label that forgets its font would render the Russian UI blank.
        /// </summary>
        private static void SetProjectDefault(TMP_FontAsset asset)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[Match3] Missing " + TmpSettingsPath
                    + " - the TMP default font asset was left as it was.");
                return;
            }

            var so = new SerializedObject(settings);
            SerializedProperty property = so.FindProperty("m_defaultFontAsset");
            if (property == null)
            {
                Debug.LogWarning("[Match3] TMP Settings has no m_defaultFontAsset field.");
                return;
            }

            property.objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
