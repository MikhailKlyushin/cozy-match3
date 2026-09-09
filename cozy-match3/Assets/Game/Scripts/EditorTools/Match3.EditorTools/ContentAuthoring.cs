using Match3.Board;
using Match3.Content;
using Match3.Core;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Creates the presentation profile assets and wires them to the generated sprites. Run after
    /// <see cref="ArtAuthoring.GenerateArt"/>.
    /// </summary>
    internal static class ContentAuthoring
    {
        private const string GameplayConfigFolder = "Assets/Game/Content/Gameplay/Configs";
        private const string ArtFolder = ArtAuthoring.GameplayArtFolder;

        [MenuItem("Match3/Authoring/Generate Content Profiles")]
        public static void GenerateProfiles()
        {
            SceneAuthoring.EnsureFolder(GameplayConfigFolder);

            CreateTimingProfile();
            CreateChipProfile();
            CreateElementProfile();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Content profiles generated in " + GameplayConfigFolder);
        }

        private static void CreateTimingProfile()
        {
            // Defaults in TimingProfile already carry every §11.3 value, so the asset only has
            // to exist for the installer to bind.
            CreateOrReplace<TimingProfile>(GameplayConfigFolder + "/TimingProfile.asset");
        }

        private static void CreateChipProfile()
        {
            ChipVisualProfile profile =
                CreateOrReplace<ChipVisualProfile>(GameplayConfigFolder + "/ChipVisualProfile.asset");

            var so = new SerializedObject(profile);

            SerializedProperty chips = so.FindProperty("_chips");
            chips.arraySize = ChipColors.MaxColorCount;
            for (int colorIndex = 1; colorIndex <= ChipColors.MaxColorCount; colorIndex++)
            {
                SerializedProperty entry = chips.GetArrayElementAtIndex(colorIndex - 1);
                entry.FindPropertyRelative("_color").enumValueIndex = colorIndex;
                entry.FindPropertyRelative("_sprite").objectReferenceValue =
                    LoadSprite("T_Chip_Cat0" + colorIndex.ToString() + "_2D");
                entry.FindPropertyRelative("_particleColor").colorValue =
                    ProceduralArt.ChipColorOf(colorIndex);
                entry.FindPropertyRelative("_destroyFx").objectReferenceValue = null;
            }

            var boosters = new[]
            {
                new { Type = BoosterType.RocketH, Sprite = "T_Booster_RocketH_2D" },
                new { Type = BoosterType.RocketV, Sprite = "T_Booster_RocketV_2D" },
                new { Type = BoosterType.Bomb, Sprite = "T_Booster_Bomb_2D" },
                new { Type = BoosterType.Rainbow, Sprite = "T_Booster_Rainbow_2D" },
                new { Type = BoosterType.Airplane, Sprite = "T_Booster_Airplane_2D" },
            };

            SerializedProperty boosterArray = so.FindProperty("_boosters");
            boosterArray.arraySize = boosters.Length;
            for (int i = 0; i < boosters.Length; i++)
            {
                SerializedProperty entry = boosterArray.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_booster").enumValueIndex = (int)boosters[i].Type;
                entry.FindPropertyRelative("_sprite").objectReferenceValue = LoadSprite(boosters[i].Sprite);
                entry.FindPropertyRelative("_activationFx").objectReferenceValue = null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void CreateElementProfile()
        {
            ElementVisualProfile profile =
                CreateOrReplace<ElementVisualProfile>(GameplayConfigFolder + "/ElementVisualProfile.asset");

            var so = new SerializedObject(profile);
            so.FindProperty("_bowOverlay").objectReferenceValue = LoadSprite("T_Element_BoxBow_2D");
            so.FindProperty("_pipOverlay").objectReferenceValue = LoadSprite("T_Element_BoxPip_2D");

            SerializedProperty elements = so.FindProperty("_elements");
            int count = 3 + ChipColors.MaxColorCount + 2;
            elements.arraySize = count;
            int index = 0;

            // Health stages come from the catalogue, so a token with N hit points gets N sprites
            // and the visual changes on every hit (§7.1).
            WriteElement(elements.GetArrayElementAtIndex(index++), ElementTokens.Box1, 1, false, false);
            WriteElement(elements.GetArrayElementAtIndex(index++), ElementTokens.Box2, 2, false, false);
            WriteElement(elements.GetArrayElementAtIndex(index++), ElementTokens.Box3, 3, false, false);

            for (int colorIndex = 1; colorIndex <= ChipColors.MaxColorCount; colorIndex++)
            {
                WriteElement(
                    elements.GetArrayElementAtIndex(index++),
                    ElementTokens.ColoredBox(colorIndex),
                    healthStages: 1,
                    showBow: true,
                    showPip: false);
            }

            WriteElement(
                elements.GetArrayElementAtIndex(index++),
                ElementTokens.ColoredBoxCycling,
                healthStages: 1,
                showBow: true,
                showPip: true);

            SerializedProperty blocker = elements.GetArrayElementAtIndex(index);
            blocker.FindPropertyRelative("_token").stringValue = ElementTokens.Blocker;
            blocker.FindPropertyRelative("_showBow").boolValue = false;
            blocker.FindPropertyRelative("_showNextColorPip").boolValue = false;
            blocker.FindPropertyRelative("_destroyFx").objectReferenceValue = null;
            SerializedProperty blockerStages = blocker.FindPropertyRelative("_healthStages");
            blockerStages.arraySize = 1;
            blockerStages.GetArrayElementAtIndex(0).objectReferenceValue = LoadSprite("T_Element_Blocker_2D");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void WriteElement(
            SerializedProperty entry,
            string token,
            int healthStages,
            bool showBow,
            bool showPip)
        {
            entry.FindPropertyRelative("_token").stringValue = token;
            entry.FindPropertyRelative("_showBow").boolValue = showBow;
            entry.FindPropertyRelative("_showNextColorPip").boolValue = showPip;
            entry.FindPropertyRelative("_destroyFx").objectReferenceValue = null;

            SerializedProperty stages = entry.FindPropertyRelative("_healthStages");
            stages.arraySize = healthStages;
            for (int i = 0; i < healthStages; i++)
            {
                stages.GetArrayElementAtIndex(i).objectReferenceValue =
                    LoadSprite("T_Element_BoxBase_S" + i.ToString() + "_2D");
            }
        }

        private static T CreateOrReplace<T>(string assetPath) where T : ScriptableObject
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

        private static Sprite LoadSprite(string assetName)
        {
            string path = ArtFolder + "/" + assetName + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[Match3] Missing sprite: " + path + " - run Generate Placeholder Art first");
            }

            return sprite;
        }
    }
}
