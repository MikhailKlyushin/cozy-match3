using Match3.Board;
using Match3.Content;
using Match3.Core;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    /// <summary>
    /// Creates the presentation profile assets and wires them to the sprites in the art folder.
    /// <para>
    /// Structure — entry count, token, hit-point stage count — is always rewritten, because it
    /// follows the rules and not the art. References that an author fills in (sprites, FX prefabs,
    /// particle colour) are written only while empty: regenerating levels or scenes must not undo
    /// a hand-made assignment. <see cref="ResetProfiles"/> is the way back to placeholders.
    /// </para>
    /// </summary>
    internal static class ContentAuthoring
    {
        private const string GameplayConfigFolder = "Assets/Game/Content/Gameplay/Configs";
        private const string ArtFolder = ArtAuthoring.GameplayArtFolder;

        [MenuItem("Match3/Authoring/Generate Content Profiles")]
        public static void GenerateProfiles()
        {
            Generate(preserveAuthored: true);
        }

        [MenuItem("Match3/Authoring/Reset Content Profiles")]
        public static void ResetProfiles()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Reset content profiles?",
                "Every sprite, FX prefab and particle colour in the chip and element profiles goes "
                + "back to the generated placeholder wiring. Hand-made assignments are lost.",
                "Reset",
                "Cancel");

            if (confirmed)
            {
                Generate(preserveAuthored: false);
            }
        }

        /// <summary>
        /// The audio half of <see cref="GenerateProfiles"/> on its own, for the batch path that
        /// runs after a clip is added and has no reason to touch the art profiles.
        /// </summary>
        internal static void GenerateAudioProfile()
        {
            SceneAuthoring.EnsureFolder(GameplayConfigFolder);
            CreateAudioProfile(preserveAuthored: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Audio profile generated in " + GameplayConfigFolder);
        }

        private static void Generate(bool preserveAuthored)
        {
            SceneAuthoring.EnsureFolder(GameplayConfigFolder);

            CreateTimingProfile();
            CreateChipProfile(preserveAuthored);
            CreateElementProfile(preserveAuthored);
            CreateAudioProfile(preserveAuthored);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Content profiles " + (preserveAuthored ? "generated" : "reset")
                + " in " + GameplayConfigFolder);
        }

        private static void CreateTimingProfile()
        {
            // Defaults in TimingProfile already carry every §11.3 value, so the asset only has
            // to exist for the installer to bind.
            CreateOrReplace<TimingProfile>(GameplayConfigFolder + "/TimingProfile.asset");
        }

        /// <summary>
        /// Ambience volume and fade default in <see cref="AudioProfile"/> itself, so only the clip
        /// reference is written here - and only while empty, like every other authored field.
        /// </summary>
        private static void CreateAudioProfile(bool preserveAuthored)
        {
            AudioProfile profile =
                CreateOrReplace<AudioProfile>(GameplayConfigFolder + "/AudioProfile.asset");

            var so = new SerializedObject(profile);
            SerializedProperty ambient = so.FindProperty("_ambient");
            if (!preserveAuthored || ambient.objectReferenceValue == null)
            {
                ambient.objectReferenceValue = AudioAuthoring.LoadAmbient();
            }

            WriteSfxEntries(so, preserveAuthored);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// One entry per <see cref="SfxId"/>, in enum order. The id follows from the enum and the
        /// clip from the file name, so both are structural; the mix values are authored, and an
        /// entry that already exists keeps the ones it has.
        /// </summary>
        private static void WriteSfxEntries(SerializedObject so, bool preserveAuthored)
        {
            SerializedProperty entries = so.FindProperty("_sfx");
            const int first = (int)SfxId.Swap;
            const int last = (int)SfxId.LevelLost;

            int existing = entries.arraySize;
            entries.arraySize = last - first + 1;

            for (int i = 0; i < entries.arraySize; i++)
            {
                var id = (SfxId)(first + i);
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_id").enumValueIndex = (int)id;

                SerializedProperty clip = entry.FindPropertyRelative("_clip");
                if (!preserveAuthored || clip.objectReferenceValue == null)
                {
                    clip.objectReferenceValue = AudioAuthoring.LoadSfx(id);
                }

                // A grown array copies its last element, so a new entry starts on someone else's
                // mix: the defaults are written over it rather than left to be noticed later.
                if (preserveAuthored && i < existing)
                {
                    continue;
                }

                AudioAuthoring.SfxDefault defaults = AudioAuthoring.DefaultsFor(id);
                entry.FindPropertyRelative("_volume").floatValue = defaults.Volume;
                entry.FindPropertyRelative("_minInterval").floatValue = defaults.MinInterval;
                entry.FindPropertyRelative("_pitchJitter").floatValue = defaults.PitchJitter;
            }
        }

        private static void CreateChipProfile(bool preserveAuthored)
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

                WriteSprite(
                    entry.FindPropertyRelative("_sprite"),
                    ChipArtRegistry.ChipAssetNameOf(colorIndex),
                    preserveAuthored);
                SerializedProperty particleColor = entry.FindPropertyRelative("_particleColor");
                WriteParticleColor(
                    particleColor,
                    ChipArtRegistry.ChipColorOf(colorIndex),
                    preserveAuthored);

                // Derived from the colour just above, not from the registry fallback: the profile
                // usually carries a hand-authored chip colour, and the flash has to match THAT.
                WriteParticleColor(
                    entry.FindPropertyRelative("_destroyFxTint"),
                    Match3Palette.FlashTint(particleColor.colorValue),
                    preserveAuthored);
                WriteFx(
                    entry.FindPropertyRelative("_destroyFx"),
                    FxAuthoring.ChipDestroyPrefab,
                    preserveAuthored);
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

                WriteSprite(entry.FindPropertyRelative("_sprite"), boosters[i].Sprite, preserveAuthored);
                WriteBoosterFx(entry, boosters[i].Type, preserveAuthored);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void CreateElementProfile(bool preserveAuthored)
        {
            ElementVisualProfile profile =
                CreateOrReplace<ElementVisualProfile>(GameplayConfigFolder + "/ElementVisualProfile.asset");

            var so = new SerializedObject(profile);
            WriteSprite(so.FindProperty("_bowOverlay"), "T_Element_BoxBow_2D", preserveAuthored);
            WriteSprite(so.FindProperty("_pipOverlay"), "T_Element_BoxPip_2D", preserveAuthored);

            SerializedProperty elements = so.FindProperty("_elements");
            int count = 3 + ChipColors.MaxColorCount + 2;
            elements.arraySize = count;
            int index = 0;

            // Health stages come from the catalogue, so a token with N hit points gets N sprites
            // and the visual changes on every hit (§7.1).
            WriteElement(elements.GetArrayElementAtIndex(index++), ElementTokens.Box1, 1, false, false, preserveAuthored);
            WriteElement(elements.GetArrayElementAtIndex(index++), ElementTokens.Box2, 2, false, false, preserveAuthored);
            WriteElement(elements.GetArrayElementAtIndex(index++), ElementTokens.Box3, 3, false, false, preserveAuthored);

            for (int colorIndex = 1; colorIndex <= ChipColors.MaxColorCount; colorIndex++)
            {
                WriteElement(
                    elements.GetArrayElementAtIndex(index++),
                    ElementTokens.ColoredBox(colorIndex),
                    healthStages: 1,
                    showBow: true,
                    showPip: false,
                    preserveAuthored: preserveAuthored);
            }

            WriteElement(
                elements.GetArrayElementAtIndex(index++),
                ElementTokens.ColoredBoxCycling,
                healthStages: 1,
                showBow: true,
                showPip: true,
                preserveAuthored: preserveAuthored);

            SerializedProperty blocker = elements.GetArrayElementAtIndex(index);
            blocker.FindPropertyRelative("_token").stringValue = ElementTokens.Blocker;
            blocker.FindPropertyRelative("_showBow").boolValue = false;
            blocker.FindPropertyRelative("_showNextColorPip").boolValue = false;
            WriteFx(
                blocker.FindPropertyRelative("_destroyFx"),
                FxAuthoring.ElementDestroyPrefab,
                preserveAuthored);
            SerializedProperty blockerStages = blocker.FindPropertyRelative("_healthStages");
            blockerStages.arraySize = 1;
            WriteSprite(blockerStages.GetArrayElementAtIndex(0), "T_Element_Blocker_2D", preserveAuthored);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void WriteElement(
            SerializedProperty entry,
            string token,
            int healthStages,
            bool showBow,
            bool showPip,
            bool preserveAuthored)
        {
            entry.FindPropertyRelative("_token").stringValue = token;
            entry.FindPropertyRelative("_showBow").boolValue = showBow;
            entry.FindPropertyRelative("_showNextColorPip").boolValue = showPip;
            WriteFx(
                entry.FindPropertyRelative("_destroyFx"),
                FxAuthoring.ElementDestroyPrefab,
                preserveAuthored);

            SerializedProperty stages = entry.FindPropertyRelative("_healthStages");
            stages.arraySize = healthStages;
            for (int i = 0; i < healthStages; i++)
            {
                WriteSprite(
                    stages.GetArrayElementAtIndex(i),
                    "T_Element_BoxBase_S" + i.ToString() + "_2D",
                    preserveAuthored);
            }
        }

        /// <summary>
        /// Assigns the generated sprite unless the field already points at something. The lookup
        /// itself is skipped in that case, so a project whose art was renamed does not log a
        /// missing-asset error for a name nobody uses any more.
        /// </summary>
        private static void WriteSprite(SerializedProperty property, string assetName, bool preserveAuthored)
        {
            if (preserveAuthored && property.objectReferenceValue != null)
            {
                return;
            }

            property.objectReferenceValue = LoadSprite(assetName);
        }

        /// <summary>
        /// A colour has no null, so "authored" means "not fully transparent" - the value a fresh
        /// serialized field carries.
        /// </summary>
        private static void WriteParticleColor(SerializedProperty property, Color generated, bool preserveAuthored)
        {
            if (preserveAuthored && property.colorValue.a > 0f)
            {
                return;
            }

            property.colorValue = generated;
        }

        /// <summary>
        /// One FX role of one booster. The three role fields are optional by design (T31): a
        /// profile that leaves them empty falls back to the activation effect, so they are filled
        /// only because the generated prefabs exist for all four.
        /// </summary>
        private static void WriteBoosterFx(SerializedProperty entry, BoosterType booster, bool preserveAuthored)
        {
            WriteFx(
                entry.FindPropertyRelative("_activationFx"),
                FxAuthoring.NameOf(booster, FxAuthoring.FxRole.Activation),
                preserveAuthored);
            WriteFx(
                entry.FindPropertyRelative("_beamFx"),
                FxAuthoring.NameOf(booster, FxAuthoring.FxRole.Beam),
                preserveAuthored);
            WriteFx(
                entry.FindPropertyRelative("_burstFx"),
                FxAuthoring.NameOf(booster, FxAuthoring.FxRole.Burst),
                preserveAuthored);
            WriteFx(
                entry.FindPropertyRelative("_impactFx"),
                FxAuthoring.NameOf(booster, FxAuthoring.FxRole.Impact),
                preserveAuthored);
        }

        private static void WriteFx(SerializedProperty property, string prefabName, bool preserveAuthored)
        {
            if (preserveAuthored && property.objectReferenceValue != null)
            {
                return;
            }

            property.objectReferenceValue = FxAuthoring.Load(prefabName);
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
                Debug.LogError("[Match3] Missing sprite: " + path + " - the art folder is missing it");
            }

            return sprite;
        }
    }
}
