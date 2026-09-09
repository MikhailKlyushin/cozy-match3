using Match3.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace Match3.EditorTools
{
    /// <summary>
    /// Creates Boot.unity and registers both scenes in the build settings. Scenes are generated
    /// rather than hand-built, so the layout stays reviewable in a diff and reproducible on a
    /// clean checkout.
    /// </summary>
    internal static class SceneAuthoring
    {
        /// <summary>Reference resolution from GDD §13.</summary>
        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        internal const string ScenesFolder = "Assets/Game/Scenes";
        internal const string BootScenePath = ScenesFolder + "/Boot.unity";
        internal const string GameScenePath = ScenesFolder + "/Game.unity";

        private const float ScreenMatch = 0.5f;

        [MenuItem("Match3/Authoring/Generate Boot Scene")]
        public static void GenerateBootScene()
        {
            EnsureFolder(ScenesFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Canvas canvas = CreateUiCanvas("BootCanvas", sortOrder: 0);
            RectTransform panel = CreateStretchedChild(canvas.transform, "LoadingPanel");

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.10f, 0.14f, 1f);
            background.raycastTarget = false;

            Text title = PrefabAuthoring.CreateText("Title", panel, "MATCH-3", 96);
            Place(title.rectTransform, new Vector2(0f, 140f), new Vector2(900f, 140f));

            Text progressLabel = PrefabAuthoring.CreateText("Progress", panel, "0%", 48);
            Place(progressLabel.rectTransform, new Vector2(0f, -40f), new Vector2(900f, 80f));

            Image fill = CreateProgressBar(panel);

            var loaderGo = new GameObject("BootLoader");
            loaderGo.transform.SetParent(canvas.transform, false);
            var loader = loaderGo.AddComponent<BootLoader>();
            PrefabAuthoring.Wire(loader, "_progressLabel", progressLabel);
            PrefabAuthoring.Wire(loader, "_progressFill", fill);

            var contextGo = new GameObject("SceneContext", typeof(SceneContext));
            var installer = contextGo.AddComponent<BootInstaller>();
            LevelContextAuthoring.WireInstallers(contextGo.GetComponent<SceneContext>(), installer);

            CreateEventSystemObject();

            EditorSceneManager.SaveScene(scene, BootScenePath);
            Debug.Log("[Match3] Boot scene generated: " + BootScenePath);
        }

        [MenuItem("Match3/Authoring/Register Scenes In Build")]
        public static void RegisterScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            Debug.Log("[Match3] Build settings scenes: Boot, Game");
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

        /// <summary>Canvas per §13: scale with screen size, 1080x1920, match 0.5.</summary>
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

        /// <summary>
        /// InputSystemUIInputModule, not StandaloneInputModule: activeInputHandler is 1, so the
        /// legacy module compiles and then silently never fires.
        /// </summary>
        internal static void CreateEventSystemObject()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Image CreateProgressBar(Transform parent)
        {
            var trackGo = new GameObject("ProgressTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trackGo.transform.SetParent(parent, false);
            var track = (RectTransform)trackGo.transform;
            Place(track, new Vector2(0f, -160f), new Vector2(700f, 24f));

            var trackImage = trackGo.GetComponent<Image>();
            trackImage.color = new Color(1f, 1f, 1f, 0.15f);
            trackImage.raycastTarget = false;

            var fillGo = new GameObject("ProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(track, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.55f, 0.78f, 1f, 1f);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            return fill;
        }

        private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
