using System;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Regenerates every generated asset in dependency order. Public so it can be driven
    /// headlessly: unity run &lt;project&gt; -- -executeMethod Match3.EditorTools.AuthoringPipeline.GenerateAllBatch
    /// </summary>
    public static class AuthoringPipeline
    {
        [MenuItem("Match3/Authoring/Generate All")]
        public static void GenerateAll()
        {
            // Order is a dependency chain: sprites, then the profiles and prefabs that reference
            // them, then the level context, then the scenes that reference all of it.
            ArtAuthoring.GenerateArt();
            ContentAuthoring.GenerateProfiles();
            LevelAuthoring.GenerateLevels();
            PrefabAuthoring.GeneratePrefabs();
            LevelContextAuthoring.GenerateLevelContext();
#if MATCH3_CHEATS
            CheatsAuthoring.GenerateCheatsPrefab();
#endif
            SceneAuthoring.GenerateBootScene();
            GameSceneAuthoring.GenerateGameScene();
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
