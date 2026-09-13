using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Match3.EditorTools
{
    /// <summary>
    /// Packs the sprites that ship into atlases. uGUI cannot batch two Images drawn from two
    /// textures, and the HUD alternates between pack widgets and hand-drawn ones on every row, so
    /// the atlas is what keeps a screen at a handful of draw calls. Grouping follows what is on
    /// screen together: a page is loaded whole, so a shared atlas would make the loading screen
    /// pay for the board and the board pay for the mascot.
    /// </summary>
    internal static class AtlasAuthoring
    {
        /// <summary>
        /// `art-direction.md` §3.3. Mip-maps bleed across a tight seam: on the low mips a
        /// neighbour's colour creeps in as a rim along the sprite edge.
        /// </summary>
        private const int Padding = 16;

        private const string DefaultPlatform = "DefaultTexturePlatform";

        private const string CatAnimationFolder = ArtAuthoring.HudArtFolder + "/Animations/CatAnimation";
        private const string UiSpriteFolder = ArtPackAuthoring.UiPackFolder + "/PNG/Grey/Double";
        private const string VfxSpriteFolder = ArtPackAuthoring.VfxPackFolder + "/PNG";

        private const string GameplayAtlasPath = ArtAuthoring.GameplayArtFolder + "/SA_Gameplay.spriteatlas";
        private const string FxAtlasPath = FxAuthoring.FxFolder + "/SA_Fx.spriteatlas";
        private const string HudAtlasPath = ArtAuthoring.HudArtFolder + "/SA_Hud.spriteatlas";
        private const string CatAtlasPath = CatAnimationFolder + "/SA_CatAnimation.spriteatlas";

        /// <summary>
        /// Every PNG in the folder is a board sprite and every one of them ships, so the folder
        /// itself is the member: a sprite added later joins without a second edit here.
        /// </summary>
        private static readonly string[] GameplayMembers = { ArtAuthoring.GameplayArtFolder };

        private static readonly string[] CatMembers = { CatAnimationFolder };

        /// <summary>
        /// The two packs ship 244 PNGs and the game draws 11 of them, so these are listed one by
        /// one - a folder member would drag the whole catalogue into the build.
        /// </summary>
        private static readonly string[] FxMembers =
        {
            VfxSpriteFolder + "/circle_03.png",
            VfxSpriteFolder + "/light_02.png",
            VfxSpriteFolder + "/light_03.png",
            VfxSpriteFolder + "/magic_03.png",
            VfxSpriteFolder + "/smoke_09.png",
            VfxSpriteFolder + "/star_01.png",
            VfxSpriteFolder + "/star_09.png",
            VfxSpriteFolder + "/Rotated/spark_06_rotated.png",
        };

        /// <summary>
        /// The room itself - background, armchair, blanket, lamp, mascot - and the boot screen's
        /// logo and loading bar stay out: each is large, each is drawn once, and an atlas would
        /// only round their combined 7 Mpx up to a page of its own (§3.3).
        /// `check_square_color_cross` is reachable only from the cheats prefab, and T32 asks that
        /// no atlas drag a cheat asset into a build.
        /// </summary>
        private static readonly string[] HudMembers =
        {
            ArtAuthoring.HudArtFolder + "/T_Ui_Panel_2D.png",
            ArtAuthoring.HudArtFolder + "/T_UI_Sound_Enabled_2D.png",
            ArtAuthoring.HudArtFolder + "/T_UI_Sound_Disabled_2D.png",
            UiSpriteFolder + "/button_square_flat.png",
            UiSpriteFolder + "/button_rectangle_depth_flat.png",
            UiSpriteFolder + "/icon_checkmark.png",
        };

        private static readonly string[][] AllMembers =
        {
            GameplayMembers, FxMembers, HudMembers, CatMembers,
        };

        /// <summary>
        /// True for a sprite some atlas packs, folder members included. The two importers ask this
        /// before choosing a compression: a packed sprite's own texture never reaches the build -
        /// the page does - so compressing the source only costs quality. The packer would
        /// decompress it, repack and compress again, and that second pass over the first one's
        /// artefacts is what Unity warns about.
        /// </summary>
        internal static bool IsAtlasMember(string assetPath)
        {
            for (int i = 0; i < AllMembers.Length; i++)
            {
                string[] members = AllMembers[i];
                for (int j = 0; j < members.Length; j++)
                {
                    string member = members[j];
                    if (string.Equals(assetPath, member, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (assetPath.Length > member.Length
                        && assetPath[member.Length] == '/'
                        && assetPath.StartsWith(member, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [MenuItem("Match3/Authoring/Generate Sprite Atlases")]
        public static void GenerateAtlases()
        {
            // An atlas asset nothing packs is a lie on disk: with the packer disabled every sprite
            // still ships as its own texture and the batching never happens.
            if (EditorSettings.spritePackerMode != SpritePackerMode.AlwaysOnAtlas)
            {
                EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;
            }

            var claimed = new Dictionary<string, string>(StringComparer.Ordinal);

            // 2048 is a ceiling, not a target - Unity packs into the smallest page that fits, and
            // the board sprites import at 128 (the fallback of §3.2), so this page lands at 1024.
            Build(GameplayAtlasPath, GameplayMembers, mipmaps: true, maxTextureSize: 2048, claimed);

            // Mips are off for the rest: the HUD and the glows are drawn at or above their texel
            // density and would pay 33 % of the page for a level nothing samples (§3.2).
            Build(FxAtlasPath, FxMembers, mipmaps: false, maxTextureSize: 1024, claimed);
            Build(HudAtlasPath, HudMembers, mipmaps: false, maxTextureSize: 1024, claimed);
            Build(CatAtlasPath, CatMembers, mipmaps: false, maxTextureSize: 2048, claimed);

            // Before the packing run, not after: the page is built from whatever the importers
            // last produced.
            DropSourceCompression(claimed.Keys);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);

            Debug.Log("[Match3] Sprite atlases generated: " + claimed.Count.ToString()
                + " sprites in 4 atlases");
        }

        private static void Build(
            string atlasPath,
            string[] members,
            bool mipmaps,
            int maxTextureSize,
            Dictionary<string, string> claimed)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            var packing = atlas.GetPackingSettings();
            packing.padding = Padding;
            packing.enableTightPacking = false;

            // A rotated entry comes out sideways under a nine-sliced or tiled Image, and three of
            // the HUD members are nine-sliced.
            packing.enableRotation = false;
            atlas.SetPackingSettings(packing);

            var texture = atlas.GetTextureSettings();
            texture.generateMipMaps = mipmaps;
            texture.filterMode = FilterMode.Bilinear;
            texture.sRGB = true;
            texture.readable = false;
            atlas.SetTextureSettings(texture);

            var platform = atlas.GetPlatformSettings(DefaultPlatform);
            platform.maxTextureSize = maxTextureSize;
            platform.textureCompression = TextureImporterCompression.Compressed;
            platform.crunchedCompression = false;
            atlas.SetPlatformSettings(platform);

            atlas.SetIncludeInBuild(true);

            // Rebuilt rather than merged: the member list above is the whole truth about the
            // atlas, and a leftover packable would keep shipping a sprite nothing draws.
            UnityEngine.Object[] previous = atlas.GetPackables();
            if (previous.Length > 0)
            {
                atlas.Remove(previous);
            }

            var packables = new UnityEngine.Object[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                packables[i] = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(members[i]);
                if (packables[i] == null)
                {
                    throw new InvalidOperationException("Atlas member missing: " + members[i]);
                }
            }

            atlas.Add(packables);
            EditorUtility.SetDirty(atlas);
            ClaimMembers(atlasPath, members, claimed);
        }

        /// <summary>
        /// Applies <see cref="IsAtlasMember"/> from this end, so that running this menu item alone
        /// is enough - the two importers apply the same rule when they run on their own.
        /// </summary>
        private static void DropSourceCompression(Dictionary<string, string>.KeyCollection spritePaths)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string path in spritePaths)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null
                        || importer.textureCompression == TextureImporterCompression.Uncompressed)
                    {
                        continue;
                    }

                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        /// <summary>
        /// A sprite in two atlases is packed twice and bound to whichever one loads last, so the
        /// duplicate has to fail here rather than as a doubled page in a build report.
        /// </summary>
        private static void ClaimMembers(string atlasPath, string[] members, Dictionary<string, string> claimed)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(members[i]))
                {
                    ClaimSprite(atlasPath, members[i], claimed);
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(members[i], "*.png", SearchOption.AllDirectories))
                {
                    ClaimSprite(atlasPath, path.Replace('\\', '/'), claimed);
                }
            }
        }

        private static void ClaimSprite(string atlasPath, string spritePath, Dictionary<string, string> claimed)
        {
            if (claimed.TryGetValue(spritePath, out string owner))
            {
                Debug.LogError("[Match3] " + spritePath + " is packed into both " + owner + " and "
                    + atlasPath + " - a sprite belongs to exactly one atlas");
                return;
            }

            claimed.Add(spritePath, atlasPath);
        }
    }
}
