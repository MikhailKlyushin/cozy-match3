using System;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Single entry point that regenerates every generated asset. Public so it can be driven
    /// headlessly: unity run &lt;project&gt; -- -executeMethod Match3.EditorTools.AuthoringPipeline.GenerateAll
    /// </summary>
    public static class AuthoringPipeline
    {
        [MenuItem("Match3/Authoring/Generate All")]
        public static void GenerateAll()
        {
            ArtAuthoring.GenerateArt();
            ContentAuthoring.GenerateProfiles();
            LevelAuthoring.GenerateLevels();
            SceneAuthoring.GenerateScenes();

            AssetDatabase.SaveAssets();

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
