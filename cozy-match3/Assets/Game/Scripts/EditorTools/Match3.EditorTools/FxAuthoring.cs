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
                Create(NameOf(booster.Booster, FxRole.Activation), booster.Activation, booster.Color);
                Create(NameOf(booster.Booster, FxRole.Beam), booster.Beam, booster.Color);
                Create(NameOf(booster.Booster, FxRole.Burst), BurstSprite, booster.Color);
                Create(NameOf(booster.Booster, FxRole.Impact), ImpactSprite, booster.Color);
            }

            Create(ChipDestroyPrefab, ChipDestroySprite, ChipFlash);
            Create(ElementDestroyPrefab, ElementDestroySprite, ElementPuff);

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

        /// <summary>
        /// The densest round glow in the pack: the head of a ray, its trail, and the flash of a
        /// blast. The pack's soft streaks carry almost no alpha at all - `light_03` averages 78
        /// of 255 against `trace_01`'s 4.6 - and on a board this busy that difference is the
        /// difference between an effect and nothing (§5.3).
        /// </summary>
        private static readonly FxSprite GlowSprite = new FxSprite("light_03", 0.83f, 0.84f);

        /// <summary>A ring with four spikes: the rainbow's own flare, and every impact.</summary>
        private static readonly FxSprite MagicSprite = new FxSprite("magic_03", 0.90f, 0.89f);

        /// <summary>A dense ring, so an expanding shockwave stays readable as a wave.</summary>
        private static readonly FxSprite BurstSprite = new FxSprite("circle_03", 0.88f, 0.88f);

        /// <summary>
        /// A bolt drawn edge to edge. Its fill across is 1.0, so a stretched beam ends exactly on
        /// its target instead of stopping short of it the way a margined streak does.
        /// </summary>
        private static readonly FxSprite BoltSprite = new FxSprite("Rotated/spark_06_rotated", 1f, 0.19f);

        /// <summary>Sky-wide glow, wider than it is tall: the airplane.</summary>
        private static readonly FxSprite SkySprite = new FxSprite("light_02", 0.78f, 0.85f);

        /// <summary>A star with a solid core; the chip that dies under it is gone in 0.20 s.</summary>
        private static readonly FxSprite ChipDestroySprite = new FxSprite("star_09", 0.60f, 0.65f);

        /// <summary>A thick puff of smoke, against obstacles that are the darkest thing on board.</summary>
        private static readonly FxSprite ElementDestroySprite = new FxSprite("smoke_09", 0.68f, 0.68f);

        /// <summary>The impact flash is radial, so a square SizeTo does not stretch it.</summary>
        private static FxSprite ImpactSprite => MagicSprite;

        private static readonly BoosterFx[] BoosterRoles =
        {
            // Colours are the booster glows of `art-direction.md` §2.2.
            new BoosterFx(BoosterType.RocketH, GlowSprite, GlowSprite, RocketGlow),
            new BoosterFx(BoosterType.RocketV, GlowSprite, GlowSprite, RocketGlow),
            new BoosterFx(BoosterType.Bomb, GlowSprite, GlowSprite, BombGlow),
            new BoosterFx(BoosterType.Rainbow, MagicSprite, BoltSprite, RainbowGlow),
            new BoosterFx(BoosterType.Airplane, SkySprite, GlowSprite, AirplaneGlow),
        };

        /// <summary>
        /// Booster glows, saturated well past the pastels of §2.2. The board they play on is not
        /// the palette's: the cell ships at `#D1BCAC`, not `#EEDFC8`, and against it the old
        /// amber `#FFC978` was one luminance step - an effect and its background of the same
        /// brightness is an effect nobody sees. Each of these clears the cell by 40 steps or more
        /// and carries saturation of 0.7 or more, which the desaturated board cannot answer.
        /// </summary>
        private static Color RocketGlow => Parse("#FF8A2B");

        private static Color BombGlow => Parse("#FF5A2B");

        /// <summary>Violet: the board runs warm at hue 25, so this is the far side of the wheel.</summary>
        private static Color RainbowGlow => Parse("#9B4DFF");

        private static Color AirplaneGlow => Parse("#3DA5E8");

        /// <summary>
        /// White, and deliberately not a tint: the flash plays on top of the dying chip, which is
        /// darker than the cell, and white is the one value that clears every chip colour at once.
        /// </summary>
        private static Color ChipFlash => Color.white;

        /// <summary>Warm white smoke - obstacles are the darkest thing on the board.</summary>
        private static Color ElementPuff => Parse("#FFF6EA");

        private static void Create(string prefabName, FxSprite sprite, Color color)
        {
            var root = new GameObject(prefabName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);

            var image = root.GetComponent<Image>();
            image.sprite = ArtPackAuthoring.LoadVfxSprite(sprite.Name);
            image.color = color;
            image.raycastTarget = false;

            var view = root.AddComponent<FxView>();
            PrefabAuthoring.Wire(view, "_rect", rect);
            PrefabAuthoring.Wire(view, "_image", image);
            PrefabAuthoring.WireVector2(view, "_spriteFill", sprite.Fill);

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

        /// <summary>
        /// A texture of the pack together with the share of its frame the drawing covers. The two
        /// travel as one because they are useless apart: a size in cells means nothing until it is
        /// divided by the fill of the sprite that has to show it (<see cref="FxView"/>).
        /// </summary>
        private readonly struct FxSprite
        {
            internal readonly string Name;
            internal readonly Vector2 Fill;

            internal FxSprite(string name, float fillX, float fillY)
            {
                Name = name;
                Fill = new Vector2(fillX, fillY);
            }
        }

        private readonly struct BoosterFx
        {
            internal readonly BoosterType Booster;
            internal readonly FxSprite Activation;
            internal readonly FxSprite Beam;
            internal readonly Color Color;

            internal BoosterFx(BoosterType booster, FxSprite activation, FxSprite beam, Color color)
            {
                Booster = booster;
                Activation = activation;
                Beam = beam;
                Color = color;
            }
        }
    }
}
