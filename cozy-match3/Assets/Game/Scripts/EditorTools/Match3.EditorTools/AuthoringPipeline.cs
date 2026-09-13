using System;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Regenerates the assets that are still machine-owned, in dependency order. The scenes and
    /// the view prefabs are deliberately absent (see below).
    /// Public so it can be driven headlessly: unity run &lt;project&gt; -- -executeMethod
    /// Match3.EditorTools.AuthoringPipeline.GenerateAllBatch
    /// </summary>
    public static class AuthoringPipeline
    {
        [MenuItem("Match3/Authoring/Generate All")]
        public static void GenerateAll()
        {
            // Order is a dependency chain: the font and the sprite and audio import settings,
            // then the FX prefabs and the profiles that reference them, then the levels.
            FontAuthoring.GenerateFontAsset();
            ArtAuthoring.ApplyImportSettings();
            ArtPackAuthoring.ConfigurePacks();
            AudioAuthoring.ApplyImportSettings();
            FxAuthoring.GenerateFx();
            ContentAuthoring.GenerateProfiles();
            LevelAuthoring.GenerateLevels();
#if MATCH3_CHEATS
            CheatsAuthoring.GenerateCheatsPrefab();
#endif
            // Boot.unity, Game.unity, the view prefabs and VAR_LevelContext are deliberately
            // absent: all four are hand-authored now - the room, the Background and GoalRow prefab
            // instances, the cell tint, the hint sprite - and their generators rebuild them from
            // scratch, which would silently throw that work away. Rebuild one only through its own
            // menu item (Generate Boot Scene / Generate Game Scene / Generate View Prefabs /
            // Generate Level Context Prefab), knowing what it costs.
            SceneAuthoring.RegisterScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!LevelAuthoring.VerifyLevelAssets())
            {
                throw new InvalidOperationException("Level assets did not round-trip; see the errors above.");
            }

            Debug.Log("[Match3] Authoring pipeline complete");
        }

        /// <summary>
        /// Headless variant that turns an exception into a non-zero exit code, so a broken
        /// generator fails the command instead of logging quietly.
        /// </summary>
        public static void GenerateAllBatch()
        {
            try
            {
                GenerateAll();
            }
            catch (Exception e)
            {
                Debug.LogError("[Match3] Authoring pipeline failed: " + e);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }
    }
}
