using Match3.Core;
using Match3.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// Builds the FX prefabs the transcript players rent (`art-direction.md` §5.2). Every effect
    /// is a single <c>Image</c> on an <see cref="FxView"/>: a ParticleSystem nested in a
    /// ScreenSpaceOverlay canvas does not render at all (§5.1), and no prefab here carries an
    /// Animator or a behaviour of its own.
    /// <para>
    /// The textures are greyscale, so the colour sits in the prefab's <c>Image</c> and
    /// <see cref="FxView"/> multiplies the rented tint into it.
    /// </para>
    /// </summary>
    internal static class FxAuthoring
    {
        internal const string FxFolder = "Assets/Game/Content/Gameplay/FX";

        /// <summary>Effect of a chip's death; one prefab serves all six colours.</summary>
        internal const string ChipDestroyPrefab = "VFX_ChipDestroy";

        /// <summary>Effect of an obstacle's death.</summary>
        internal const string ElementDestroyPrefab = "VFX_ElementDestroy";

        [MenuItem("Match3/Authoring/Generate FX Prefabs")]
        public static void GenerateFx()
        {
            SceneAuthoring.EnsureFolder(FxFolder);

            for (int i = 0; i < BoosterRoles.Length; i++)
            {
                BoosterFx booster = BoosterRoles[i];
                Create(NameOf(booster.Booster, FxRole.Activation), booster.Sprite, booster.Color);
                Create(NameOf(booster.Booster, FxRole.Beam), BeamSprite, booster.Color);
                Create(NameOf(booster.Booster, FxRole.Burst), BurstSprite, booster.Color);
                Create(NameOf(booster.Booster, FxRole.Impact), ImpactSprite, booster.Color);
            }

            Create(ChipDestroyPrefab, "star_01", Match3Palette.HintStroke);
            Create(ElementDestroyPrefab, "smoke_02", Match3Palette.BoardPanel);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] FX prefabs generated in " + FxFolder);
        }

        internal static GameObject Load(string prefabName)
        {
            string path = FxFolder + "/" + prefabName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("[Match3] Missing FX prefab " + path
                    + " - run Match3/Authoring/Generate FX Prefabs");
            }

            return prefab;
        }

        internal static string NameOf(BoosterType booster, FxRole role)
            => "VFX_Booster_" + booster.ToString() + role.ToString();

        /// <summary>Role of an effect, which is also the suffix of its prefab name.</summary>
        internal enum FxRole
        {
            /// <summary>Heads and flares: shown where the booster sits.</summary>
            Activation = 0,

            /// <summary>Trails and rays: stretched into a line between two cells.</summary>
            Beam = 1,

            /// <summary>Shockwaves: expanded from a point.</summary>
            Burst = 2,

            /// <summary>The hit at the far end of a flight.</summary>
            Impact = 3,
        }

        /// <summary>A horizontal streak; the player rotates and stretches it along the ray.</summary>
        private const string BeamSprite = "Rotated/trace_01_rotated";

        /// <summary>A soft ring, so an expanding shockwave stays readable as a wave.</summary>
        private const string BurstSprite = "circle_05";

        /// <summary>A dense flash for the moment of contact.</summary>
        private const string ImpactSprite = "muzzle_03";

        private static readonly BoosterFx[] BoosterRoles =
        {
            // Colours are the booster glows of `art-direction.md` §2.2.
            new BoosterFx(BoosterType.RocketH, "light_01", Match3Palette.HintGlow),
            new BoosterFx(BoosterType.RocketV, "light_01", Match3Palette.HintGlow),
            new BoosterFx(BoosterType.Bomb, "flare_01", BombGlow),
            new BoosterFx(BoosterType.Rainbow, "magic_04", RainbowGlow),
            new BoosterFx(BoosterType.Airplane, "light_02", AirplaneGlow),
        };

        private static Color BombGlow => Parse("#FF9A5C");

        private static Color RainbowGlow => Parse("#FFF3DC");

        private static Color AirplaneGlow => Parse("#BFE3FF");

        private static void Create(string prefabName, string spriteName, Color color)
        {
            var root = new GameObject(prefabName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);

            var image = root.GetComponent<Image>();
            image.sprite = ArtPackAuthoring.LoadVfxSprite(spriteName);
            image.color = color;
            image.raycastTarget = false;

            var view = root.AddComponent<FxView>();
            PrefabAuthoring.Wire(view, "_rect", rect);
            PrefabAuthoring.Wire(view, "_image", image);

            PrefabAuthoring.SaveAndCleanUp(root, FxFolder + "/" + prefabName + ".prefab");
        }

        private static Color Parse(string html)
        {
            if (!ColorUtility.TryParseHtmlString(html, out Color color))
            {
                Debug.LogError("[Match3] Bad FX colour: " + html);
                return Color.magenta;
            }

            return color;
        }

        private readonly struct BoosterFx
        {
            internal readonly BoosterType Booster;
            internal readonly string Sprite;
            internal readonly Color Color;

            internal BoosterFx(BoosterType booster, string sprite, Color color)
            {
                Booster = booster;
                Sprite = sprite;
                Color = color;
            }
        }
    }
}
