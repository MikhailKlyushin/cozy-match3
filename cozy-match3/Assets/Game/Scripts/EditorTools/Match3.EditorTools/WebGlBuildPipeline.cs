using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Headless WebGL builds (T25). Two configurations: Development keeps MATCH3_CHEATS and
    /// exception support, Release drops both, which is also the check that no cheat asset leaks
    /// into the shipped build (§15).
    /// </summary>
    public static class WebGlBuildPipeline
    {
        private const string CheatsDefine = "MATCH3_CHEATS";
        private const string BootScene = "Assets/Game/Scenes/Boot.unity";
        private const string GameScene = "Assets/Game/Scenes/Game.unity";
        private const string WebGlTemplate = "PROJECT:Match3";
        private const int ReferenceWidth = 1080;
        private const int ReferenceHeight = 1920;

        /// <summary>unity run . -- -executeMethod Match3.EditorTools.WebGlBuildPipeline.BuildDevelopmentBatch</summary>
        public static void BuildDevelopmentBatch() => RunBatch(development: true);

        /// <summary>unity run . -- -executeMethod Match3.EditorTools.WebGlBuildPipeline.BuildReleaseBatch</summary>
        public static void BuildReleaseBatch() => RunBatch(development: false);

        [MenuItem("Match3/Build/WebGL Development")]
        public static void BuildDevelopment() => Build(development: true);

        [MenuItem("Match3/Build/WebGL Release")]
        public static void BuildRelease() => Build(development: false);

        private static void RunBatch(bool development)
        {
            try
            {
                Build(development);
            }
            catch (Exception e)
            {
                Debug.LogError("[Match3] Build failed: " + e);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void Build(bool development)
        {
            string outputPath = development ? "Build/WebGL-Development" : "Build/WebGL-Release";
            Directory.CreateDirectory(outputPath);

            string previousDefines = PlayerSettings.GetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.WebGL);

            try
            {
                ApplySettings(development);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { BootScene, GameScene },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                Summarise(report, development);
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(
                    UnityEditor.Build.NamedBuildTarget.WebGL, previousDefines);
            }
        }

        private static void ApplySettings(bool development)
        {
            var webgl = UnityEditor.Build.NamedBuildTarget.WebGL;

            string defines = PlayerSettings.GetScriptingDefineSymbols(webgl);
            List<string> parts = new List<string>(defines.Split(';', StringSplitOptions.RemoveEmptyEntries));
            parts.Remove(CheatsDefine);
            if (development)
            {
                parts.Add(CheatsDefine);
            }

            PlayerSettings.SetScriptingDefineSymbols(webgl, string.Join(";", parts));

            // Exception support costs size and speed, so only the dev build keeps it (§14).
            PlayerSettings.WebGL.exceptionSupport = development
                ? WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly
                : WebGLExceptionSupport.None;

            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            ApplyPageSettings();
        }

        /// <summary>
        /// Template and reference resolution of the page (§13 GDD, `art-direction.md` §3.1). Kept
        /// apart from the build so a checkout can be brought to the right settings without
        /// building, and applied on every build so it cannot inherit whatever template the
        /// previous developer happened to leave selected.
        /// </summary>
        [MenuItem("Match3/Build/Apply WebGL Player Settings")]
        public static void ApplyPageSettings()
        {
            PlayerSettings.WebGL.template = WebGlTemplate;
            PlayerSettings.defaultWebScreenWidth = ReferenceWidth;
            PlayerSettings.defaultWebScreenHeight = ReferenceHeight;

            AssetDatabase.SaveAssets();
            Debug.Log("[Match3] WebGL page settings: template " + WebGlTemplate + ", "
                + ReferenceWidth.ToString() + "x" + ReferenceHeight.ToString());
        }

        /// <summary>
        /// Logs the size and the biggest assets, and fails a release build that contains a cheat
        /// asset - the check §15 asks for.
        /// </summary>
        private static void Summarise(BuildReport report, bool development)
        {
            if (report == null)
            {
                throw new InvalidOperationException("BuildPipeline returned no report.");
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Build result: " + summary.result + ", errors: " + summary.totalErrors);
            }

            Debug.Log("[Match3] Build succeeded: " + (summary.totalSize / 1024UL / 1024UL).ToString()
                      + " MB, output " + summary.outputPath);

            PackedAssetInfo[] biggest = CollectBiggestAssets(report, 10);
            for (int i = 0; i < biggest.Length; i++)
            {
                Debug.Log("[Match3] asset " + (i + 1).ToString() + ": "
                          + (biggest[i].packedSize / 1024UL).ToString() + " KB "
                          + biggest[i].sourceAssetPath);
            }

            if (development)
            {
                return;
            }

            List<string> leaked = FindCheatAssets(report);
            if (leaked.Count > 0)
            {
                throw new InvalidOperationException(
                    "Release build contains cheat assets (§15): " + string.Join(", ", leaked));
            }

            Debug.Log("[Match3] Release build contains no cheat assets");
        }

        private static PackedAssetInfo[] CollectBiggestAssets(BuildReport report, int count)
        {
            var all = new List<PackedAssetInfo>(256);
            PackedAssets[] packed = report.packedAssets;
            for (int i = 0; i < packed.Length; i++)
            {
                all.AddRange(packed[i].contents);
            }

            all.Sort((a, b) => b.packedSize.CompareTo(a.packedSize));

            int take = all.Count < count ? all.Count : count;
            var result = new PackedAssetInfo[take];
            for (int i = 0; i < take; i++)
            {
                result[i] = all[i];
            }

            return result;
        }

        private static List<string> FindCheatAssets(BuildReport report)
        {
            var leaked = new List<string>();
            PackedAssets[] packed = report.packedAssets;

            for (int i = 0; i < packed.Length; i++)
            {
                PackedAssetInfo[] contents = packed[i].contents;
                for (int c = 0; c < contents.Length; c++)
                {
                    string path = contents[c].sourceAssetPath;
                    if (!string.IsNullOrEmpty(path)
                        && path.IndexOf("Content/Cheats", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        leaked.Add(path);
                    }
                }
            }

            return leaked;
        }
    }
}
