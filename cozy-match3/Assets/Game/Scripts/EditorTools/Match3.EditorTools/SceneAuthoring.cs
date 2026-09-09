using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// Creates Boot.unity and Game.unity from code. Scenes and prefabs in this project are
    /// generated rather than hand-built, so the layout stays reviewable in a diff and
    /// reproducible on a clean checkout.
    /// </summary>
    internal static class SceneAuthoring
    {
        /// <summary>Reference resolution from GDD §13.</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        private const string ScenesFolder = "Assets/Game/Scenes";
        private const string BootScenePath = ScenesFolder + "/Boot.unity";
        private const string GameScenePath = ScenesFolder + "/Game.unity";
        private const float ScreenMatch = 0.5f;

        [MenuItem("Match3/Authoring/Generate Scenes")]
        public static void GenerateScenes()
        {
            EnsureFolder(ScenesFolder);

            CreateBootScene();
            CreateGameScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Scenes generated: " + BootScenePath + ", " + GameScenePath);
        }

        internal static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        /// <summary>Canvas configured per §13: scale with screen size, 1080x1920, match 0.5.</summary>
        internal static Canvas CreateUiCanvas(string name, int sortOrder)
        {
            var root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = ScreenMatch;

            return canvas;
        }

        internal static RectTransform CreateStretchedChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Canvas canvas = CreateUiCanvas("BootCanvas", sortOrder: 0);
            RectTransform panel = CreateStretchedChild(canvas.transform, "LoadingPanel");

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.10f, 0.14f, 1f);
            background.raycastTarget = false;

            CreateLabel(panel, "Title", "MATCH-3", new Vector2(0f, 120f), 96);
            CreateLabel(panel, "Progress", "Loading...", new Vector2(0f, -40f), 48);

            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void CreateGameScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Canvas canvas = CreateUiCanvas("GameCanvas", sortOrder: 0);

            RectTransform background = CreateStretchedChild(canvas.transform, "Background");
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.14f, 0.19f, 1f);
            backgroundImage.raycastTarget = false;

            // Board area is a square centred by BoardLayout at runtime (§13); HUD zones sit above
            // and below it so the 9:16..16:9 range recomposes instead of overlapping.
            CreateStretchedChild(canvas.transform, "BoardArea");
            CreateAnchoredZone(canvas.transform, "HudTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -220f));
            CreateAnchoredZone(canvas.transform, "HudBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 220f));
            CreateStretchedChild(canvas.transform, "PopupRoot");

            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void CreateAnchoredZone(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, size.y >= 0f ? 0f : 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, Mathf.Abs(size.y));
        }

        private static void CreateLabel(Transform parent, string name, string text, Vector2 anchoredPosition, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(900f, 160f);

            var label = go.AddComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            label.font = GetBuiltinFont();
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Font GetBuiltinFont()
        {
            Font font = AssetDatabase.GetBuiltinExtraResource<Font>("Arial.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
