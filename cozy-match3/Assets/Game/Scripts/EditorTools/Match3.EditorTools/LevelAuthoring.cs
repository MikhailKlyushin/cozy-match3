using System;
using Match3.Core;
using Match3.Goals;
using Match3.Levels.Authoring;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Generates the 12 level assets and the catalog from <see cref="LevelSeedData"/>, so the
    /// GDD §10.4 table is transcribed exactly once and the assets can be regenerated.
    /// </summary>
    internal static class LevelAuthoring
    {
        private const string LevelsFolder = "Assets/Game/Content/Levels";

        [MenuItem("Match3/Authoring/Generate Level Assets")]
        public static void GenerateLevels()
        {
            SceneAuthoring.EnsureFolder(LevelsFolder);

            LevelSeed[] seeds = LevelSeedData.CreateAll();
            var configs = new LevelConfig[seeds.Length];

            for (int i = 0; i < seeds.Length; i++)
            {
                configs[i] = WriteLevel(seeds[i]);
            }

            LevelCatalog catalog = LoadOrCreate<LevelCatalog>(LevelsFolder + "/LevelCatalog.asset");
            catalog.SetLevels(configs);
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Level assets generated: " + seeds.Length.ToString()
                      + ", last level id " + catalog.LastLevelId.ToString());
        }

        /// <summary>
        /// Reads every generated asset back and checks the grid survived serialisation: Unity
        /// folds long strings in YAML, so a layout that round-trips wrong would silently merge
        /// rows. Logs an error per mismatch and returns false.
        /// </summary>
        [MenuItem("Match3/Authoring/Verify Level Assets")]
        public static bool VerifyLevelAssets()
        {
            LevelSeed[] seeds = LevelSeedData.CreateAll();
            bool ok = true;

            for (int i = 0; i < seeds.Length; i++)
            {
                LevelSeed seed = seeds[i];
                string path = LevelsFolder + "/Level" + seed.Id.ToString("00") + ".asset";
                var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
                if (config == null)
                {
                    Debug.LogError("[Match3] Missing level asset: " + path);
                    ok = false;
                    continue;
                }

                ok &= VerifyLevel(config, seed);
            }

            Debug.Log(ok
                ? "[Match3] Level assets verified: " + seeds.Length.ToString() + " levels round-trip correctly"
                : "[Match3] Level asset verification FAILED");
            return ok;
        }

        private static bool VerifyLevel(LevelConfig config, LevelSeed seed)
        {
            bool ok = true;
            string prefix = "[Match3] Level " + seed.Id.ToString() + ": ";

            if (config.MoveLimit != seed.MoveLimit || config.ColorCount != seed.ColorCount)
            {
                Debug.LogError(prefix + "scalar fields do not match the seed");
                ok = false;
            }

            System.Collections.Generic.List<string> rows = config.ReadLayoutRows();
            if (rows.Count != seed.Height)
            {
                Debug.LogError(prefix + "read " + rows.Count.ToString() + " rows, expected "
                               + seed.Height.ToString());
                return false;
            }

            for (int y = 0; y < rows.Count; y++)
            {
                string[] tokens = rows[y].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != seed.Width)
                {
                    Debug.LogError(prefix + "row " + y.ToString() + " has " + tokens.Length.ToString()
                                   + " tokens, expected " + seed.Width.ToString() + ": " + rows[y]);
                    ok = false;
                    continue;
                }

                if (rows[y] != seed.Layout[y])
                {
                    Debug.LogError(prefix + "row " + y.ToString() + " differs from the seed: got '"
                                   + rows[y] + "', expected '" + seed.Layout[y] + "'");
                    ok = false;
                }
            }

            if (config.Goals.Count != seed.Goals.Length)
            {
                Debug.LogError(prefix + "goal count mismatch");
                ok = false;
            }

            if (config.Contents.Count != seed.Contents.Length)
            {
                Debug.LogError(prefix + "nested content count mismatch");
                ok = false;
            }

            if (config.Spawners.Count != seed.Spawners.Length)
            {
                Debug.LogError(prefix + "spawner list mismatch");
                ok = false;
            }

            return ok;
        }

        private static LevelConfig WriteLevel(LevelSeed seed)
        {
            string path = LevelsFolder + "/Level" + seed.Id.ToString("00") + ".asset";
            LevelConfig config = LoadOrCreate<LevelConfig>(path);

            var so = new SerializedObject(config);
            so.FindProperty("_id").intValue = seed.Id;
            so.FindProperty("_width").intValue = seed.Width;
            so.FindProperty("_height").intValue = seed.Height;
            so.FindProperty("_colorCount").intValue = seed.ColorCount;
            so.FindProperty("_moveLimit").intValue = seed.MoveLimit;
            so.FindProperty("_tier").enumValueIndex = (int)ParseTier(seed.Tier);
            so.FindProperty("_layout").stringValue = string.Join("\n", seed.Layout);
            so.FindProperty("_hintDelaySeconds").floatValue = seed.HintDelaySeconds;
            so.FindProperty("_balanceNotes").stringValue = seed.MoveLimitNote ?? string.Empty;

            SerializedProperty goals = so.FindProperty("_goals");
            goals.arraySize = seed.Goals.Length;
            for (int i = 0; i < seed.Goals.Length; i++)
            {
                WriteGoal(goals.GetArrayElementAtIndex(i), seed.Goals[i]);
            }

            SerializedProperty contents = so.FindProperty("_contents");
            contents.arraySize = seed.Contents.Length;
            for (int i = 0; i < seed.Contents.Length; i++)
            {
                SerializedProperty entry = contents.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_x").intValue = seed.Contents[i].X;
                entry.FindPropertyRelative("_y").intValue = seed.Contents[i].Y;
                entry.FindPropertyRelative("_token").stringValue = seed.Contents[i].Token;
            }

            SerializedProperty spawners = so.FindProperty("_spawners");
            spawners.arraySize = seed.Spawners.Length;
            for (int i = 0; i < seed.Spawners.Length; i++)
            {
                spawners.GetArrayElementAtIndex(i).intValue = seed.Spawners[i];
            }

            // Q10: every shipped level spawns uniformly; the field exists for tuning only.
            so.FindProperty("_spawnWeights").arraySize = 0;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void WriteGoal(SerializedProperty entry, GoalSeed seed)
        {
            GoalType type = ParseGoalType(seed.Type);
            entry.FindPropertyRelative("_type").enumValueIndex = (int)type;
            entry.FindPropertyRelative("_target").intValue = seed.Target;
            entry.FindPropertyRelative("_color").enumValueIndex = seed.Color;
            entry.FindPropertyRelative("_token").stringValue = seed.Token ?? string.Empty;
            entry.FindPropertyRelative("_booster").enumValueIndex = (int)ParseBooster(seed.Booster);
        }

        private static DifficultyTier ParseTier(string tier)
        {
            switch (tier)
            {
                case "Easy":
                    return DifficultyTier.Easy;
                case "Medium":
                    return DifficultyTier.Medium;
                case "Hard":
                    return DifficultyTier.Hard;
                case "SuperHard":
                    return DifficultyTier.SuperHard;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), "Unknown tier: " + tier);
            }
        }

        private static GoalType ParseGoalType(string type)
        {
            switch (type)
            {
                case "CollectColor":
                    return GoalType.CollectColor;
                case "DestroyElement":
                    return GoalType.DestroyElement;
                case "DestroyAnyColoredBox":
                    return GoalType.DestroyAnyColoredBox;
                case "ActivateBooster":
                    return GoalType.ActivateBooster;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "Unknown goal type: " + type);
            }
        }

        /// <summary>
        /// A rocket goal is stored as RocketH; GoalTracker counts both orientations, because the
        /// player never chooses one (§8.1, level 11).
        /// </summary>
        private static BoosterType ParseBooster(string booster)
        {
            if (string.IsNullOrEmpty(booster))
            {
                return BoosterType.None;
            }

            switch (booster)
            {
                case "Rocket":
                    return BoosterType.RocketH;
                case "Bomb":
                    return BoosterType.Bomb;
                case "Rainbow":
                    return BoosterType.Rainbow;
                case "Airplane":
                    return BoosterType.Airplane;
                default:
                    throw new ArgumentOutOfRangeException(nameof(booster), "Unknown booster: " + booster);
            }
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }
    }
}
