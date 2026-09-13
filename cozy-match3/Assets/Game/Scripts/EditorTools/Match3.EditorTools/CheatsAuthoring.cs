#if MATCH3_CHEATS
using Match3.Cheats;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// Builds the cheat panel prefab. It lives under Assets/Game/Content/Cheats/, never in
    /// Assets/Resources, because anything there ships in a release build whatever the define
    /// says (§15). The whole file compiles out without MATCH3_CHEATS.
    /// </summary>
    internal static class CheatsAuthoring
    {
        private const string PrefabFolder = "Assets/Game/Content/Cheats/Prefabs";
        private const string PrefabPath = PrefabFolder + "/VAR_CheatsRoot.prefab";

        private const float RowHeight = 54f;
        private const float RowGap = 6f;
        private const float PanelWidth = 420f;

        /// <summary>
        /// The window is deliberately far shorter than its content. Twenty-odd rows do not fit a
        /// 9:16 screen at any font size worth reading, and a window taller than the viewport puts
        /// its own controls out of reach, so everything below the header scrolls.
        /// </summary>
        private const float PanelHeight = 700f;

        private const float HeaderHeight = 64f;
        private const float ScrollbarWidth = 12f;
        private const float WindowPadding = 12f;
        private const float ContentPadding = 8f;
        private const int ButtonFontSize = 26;

        /// <summary>The window is dark, so labels cannot take the light-background text colour.</summary>
        private static readonly Color LabelColor = new Color(0.92f, 0.94f, 0.98f, 1f);

        private static float _cursor;

        [MenuItem("Match3/Authoring/Generate Cheats Prefab")]
        public static void GenerateCheatsPrefab()
        {
            SceneAuthoring.EnsureFolder(PrefabFolder);

            var root = new GameObject(
                "VAR_CheatsRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the game canvas so the panel is never hidden behind the board or a popup.
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            CheatBoardTapCatcher catcher = CreateTapCatcher(root.transform);
            CheatGridOverlayView overlay = CreateGridOverlay(root.transform);
            CheatPanelView panel = CreatePanel(root.transform);

            var rootView = root.AddComponent<CheatsRootView>();
            PrefabAuthoring.Wire(rootView, "_panel", panel);
            PrefabAuthoring.Wire(rootView, "_gridOverlay", overlay);
            PrefabAuthoring.Wire(rootView, "_tapCatcher", catcher);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Cheats prefab generated: " + PrefabPath);
        }

        private static CheatBoardTapCatcher CreateTapCatcher(Transform parent)
        {
            RectTransform rect = LevelContextAuthoring.CreateStretched("TapCatcher", parent);
            var image = rect.gameObject.AddComponent<Image>();

            // Fully transparent, and raycastable only while a booster is armed: the presenter
            // arms the Graphic itself, so the object stays active - a disabled GameObject is not
            // in the GraphicRegistry and would never deliver the placement tap at all.
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            image.enabled = false;

            var catcher = rect.gameObject.AddComponent<CheatBoardTapCatcher>();
            PrefabAuthoring.Wire(catcher, "_raycastArea", image);
            return catcher;
        }

        private static CheatGridOverlayView CreateGridOverlay(Transform parent)
        {
            RectTransform rect = LevelContextAuthoring.CreateStretched("GridOverlay", parent);
            RectTransform labelRoot = LevelContextAuthoring.CreateStretched("LabelRoot", rect);

            TextMeshProUGUI template = PrefabAuthoring.CreateText("LabelTemplate", labelRoot, "(0, 0)", 22);
            template.color = new Color(1f, 1f, 0.6f, 0.85f);
            var templateRect = (RectTransform)template.transform;
            templateRect.anchorMin = new Vector2(0.5f, 0.5f);
            templateRect.anchorMax = new Vector2(0.5f, 0.5f);
            templateRect.pivot = new Vector2(0.5f, 0.5f);
            templateRect.sizeDelta = new Vector2(90f, 30f);
            template.gameObject.SetActive(false);

            var overlay = rect.gameObject.AddComponent<CheatGridOverlayView>();
            PrefabAuthoring.Wire(overlay, "_labelRoot", labelRoot);
            PrefabAuthoring.Wire(overlay, "_labelPrefab", template);
            PrefabAuthoring.WireInt(overlay, "_labelPrewarm", 64);

            // Stays active: the overlay builds and repositions its labels from LateUpdate, which
            // a disabled GameObject never reaches. With no labels rented it draws nothing.
            return overlay;
        }

        private static CheatPanelView CreatePanel(Transform parent)
        {
            var panelGo = new GameObject("CheatPanel", typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);

            var window = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            window.transform.SetParent(panelGo.transform, false);

            var windowRect = (RectTransform)window.transform;
            windowRect.anchorMin = new Vector2(1f, 0.5f);
            windowRect.anchorMax = new Vector2(1f, 0.5f);
            windowRect.pivot = new Vector2(1f, 0.5f);
            windowRect.anchoredPosition = new Vector2(-20f, 0f);
            windowRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var windowImage = window.GetComponent<Image>();
            windowImage.color = new Color(0.10f, 0.12f, 0.17f, 0.96f);

            // Title and Close stay pinned: closing the panel must never depend on scroll position.
            TextMeshProUGUI title = CreateHeaderTitle(windowRect);
            Button close = CreateHeaderClose(windowRect);
            RectTransform content = CreateScrollView(windowRect);

            _cursor = -ContentPadding;

            TMP_InputField levelInput = AddInput(content, "LevelInput", "Level #");
            Button goTo = AddButton(content, "GoToLevelButton", "Go");
            TextMeshProUGUI levelStatus = AddLabel(content, "LevelStatus", string.Empty, 24);

            Button win = AddButton(content, "WinLevelButton", "Win level");
            Button lose = AddButton(content, "LoseLevelButton", "Lose level");
            Button addMoves = AddButton(content, "AddMovesButton", "+5 moves");

            Button rocket = AddButton(content, "RocketBoosterButton", "Rocket");
            Button bomb = AddButton(content, "BombBoosterButton", "Bomb");
            Button rainbow = AddButton(content, "RainbowBoosterButton", "Rainbow ball");
            Button airplane = AddButton(content, "AirplaneBoosterButton", "Airplane");
            Button disarm = AddButton(content, "DisarmBoosterButton", "Clear selection");
            TextMeshProUGUI armedLabel = AddLabel(content, "ArmedBoosterLabel", string.Empty, 24);
            Image armedIcon = AddIcon(content, "ArmedBoosterIcon");

            TextMeshProUGUI seedLabel = AddLabel(content, "SeedLabel", "Seed:", 24);
            TMP_InputField seedInput = AddInput(content, "SeedInput", "Seed");
            Button applySeed = AddButton(content, "ApplySeedButton", "Apply");
            Button restart = AddButton(content, "RestartAttemptButton", "Restart");

            Toggle freeMoves = AddToggle(content, "FreeMovesToggle", "Free moves");
            Toggle grid = AddToggle(content, "CoordinateGridToggle", "Show coordinate grid");
            Toggle hints = AddToggle(content, "DisableHintsToggle", "Disable hints");
            Button hintNow = AddButton(content, "HintNowButton", "Hint now");
            Button dump = AddButton(content, "DumpTranscriptButton", "Dump transcript to log");

            // What the ScrollRect scrolls: the rows are laid out by hand, so the height is known
            // exactly and no layout group has to run for the panel to be usable.
            content.sizeDelta = new Vector2(0f, Mathf.Abs(_cursor) + ContentPadding);

            var view = panelGo.AddComponent<CheatPanelView>();
            PrefabAuthoring.Wire(view, "_window", window);
            PrefabAuthoring.Wire(view, "_titleLabel", title);
            PrefabAuthoring.Wire(view, "_closeButton", close);
            PrefabAuthoring.Wire(view, "_levelInput", levelInput);
            PrefabAuthoring.Wire(view, "_goToLevelButton", goTo);
            PrefabAuthoring.Wire(view, "_levelStatusLabel", levelStatus);
            PrefabAuthoring.Wire(view, "_winLevelButton", win);
            PrefabAuthoring.Wire(view, "_loseLevelButton", lose);
            PrefabAuthoring.Wire(view, "_addMovesButton", addMoves);
            PrefabAuthoring.Wire(view, "_rocketBoosterButton", rocket);
            PrefabAuthoring.Wire(view, "_bombBoosterButton", bomb);
            PrefabAuthoring.Wire(view, "_rainbowBoosterButton", rainbow);
            PrefabAuthoring.Wire(view, "_airplaneBoosterButton", airplane);
            PrefabAuthoring.Wire(view, "_disarmBoosterButton", disarm);
            PrefabAuthoring.Wire(view, "_armedBoosterLabel", armedLabel);
            PrefabAuthoring.Wire(view, "_armedBoosterIcon", armedIcon);
            PrefabAuthoring.Wire(view, "_seedLabel", seedLabel);
            PrefabAuthoring.Wire(view, "_seedInput", seedInput);
            PrefabAuthoring.Wire(view, "_applySeedButton", applySeed);
            PrefabAuthoring.Wire(view, "_restartAttemptButton", restart);
            PrefabAuthoring.Wire(view, "_freeMovesToggle", freeMoves);
            PrefabAuthoring.Wire(view, "_coordinateGridToggle", grid);
            PrefabAuthoring.Wire(view, "_disableHintsToggle", hints);
            PrefabAuthoring.Wire(view, "_hintNowButton", hintNow);
            PrefabAuthoring.Wire(view, "_dumpTranscriptButton", dump);

            window.SetActive(false);
            return view;
        }

        private static TextMeshProUGUI CreateHeaderTitle(RectTransform window)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(window, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(WindowPadding + 4f, -HeaderHeight + 10f);
            rect.offsetMax = new Vector2(-136f, -10f);

            TextMeshProUGUI text = PrefabAuthoring.CreateText("Text", rect, "Cheats", 30);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Left;
            text.color = LabelColor;
            return text;
        }

        private static Button CreateHeaderClose(RectTransform window)
        {
            var go = new GameObject("CloseButton", typeof(RectTransform));
            go.transform.SetParent(window, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-WindowPadding - 4f, -10f);
            rect.sizeDelta = new Vector2(112f, 44f);

            Button button = PrefabAuthoring.CreateButton("Button", rect, "Close", rect.sizeDelta);
            Stretch((RectTransform)button.transform);
            SetFontSize(button, ButtonFontSize);
            return button;
        }

        /// <summary>
        /// Viewport plus content plus a slim scrollbar, wired by hand. The rows below are parented
        /// to the content, so adding one costs nothing: the height is recomputed from the cursor.
        /// </summary>
        private static RectTransform CreateScrollView(RectTransform window)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(window, false);

            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(WindowPadding, WindowPadding);
            scrollRect.offsetMax = new Vector2(-WindowPadding, -HeaderHeight);

            var viewportGo = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);

            var viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-ScrollbarWidth - 4f, 0f);

            // Transparent but raycastable, so a drag that starts on empty space still scrolls.
            Image viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);

            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            Scrollbar scrollbar = CreateScrollbar(scrollRect);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;

            // Clamped, not elastic: the panel is a tool, and a list that bounces back is harder to
            // land a press on than one that simply stops.
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private static Scrollbar CreateScrollbar(RectTransform parent)
        {
            var barGo = new GameObject(
                "Scrollbar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Scrollbar));
            barGo.transform.SetParent(parent, false);

            var barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.offsetMin = new Vector2(-ScrollbarWidth, 0f);
            barRect.offsetMax = Vector2.zero;
            barGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            var slidingGo = new GameObject("SlidingArea", typeof(RectTransform));
            slidingGo.transform.SetParent(barGo.transform, false);
            Stretch((RectTransform)slidingGo.transform);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGo.transform.SetParent(slidingGo.transform, false);
            var handle = (RectTransform)handleGo.transform;
            Stretch(handle);

            var handleImage = handleGo.GetComponent<Image>();
            handleImage.color = new Color(1f, 1f, 1f, 0.35f);

            var scrollbar = barGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            return scrollbar;
        }

        private static RectTransform NextRow(RectTransform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, _cursor);
            rect.sizeDelta = new Vector2(-8f, height);

            _cursor -= height + RowGap;
            return rect;
        }

        private static TextMeshProUGUI AddLabel(RectTransform parent, string name, string content, int size)
        {
            RectTransform row = NextRow(parent, name, size + 12f);
            TextMeshProUGUI text = PrefabAuthoring.CreateText("Text", row, content, size);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Left;
            text.color = LabelColor;
            return text;
        }

        private static Image AddIcon(RectTransform parent, string name)
        {
            RectTransform row = NextRow(parent, name, 48f);
            Image icon = PrefabAuthoring.CreateImageNode("Icon", row);
            var rect = (RectTransform)icon.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(44f, 44f);
            icon.preserveAspect = true;
            icon.enabled = false;
            return icon;
        }

        private static Button AddButton(RectTransform parent, string name, string label)
        {
            RectTransform row = NextRow(parent, name, RowHeight);
            Button button = PrefabAuthoring.CreateButton("Button", row, label, new Vector2(0f, RowHeight));
            Stretch((RectTransform)button.transform);
            SetFontSize(button, ButtonFontSize);
            return button;
        }

        private static TMP_InputField AddInput(RectTransform parent, string name, string placeholder)
        {
            RectTransform row = NextRow(parent, name, RowHeight);

            var fieldGo = new GameObject("Field", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fieldGo.transform.SetParent(row, false);
            Stretch((RectTransform)fieldGo.transform);
            fieldGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            // TMP_InputField reads its viewport rect without a null check, so the masked area is
            // built here rather than left to whoever opens the prefab.
            var viewportGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(fieldGo.transform, false);
            var viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport);
            viewport.offsetMin = new Vector2(10f, 0f);
            viewport.offsetMax = new Vector2(-10f, 0f);

            TextMeshProUGUI text = PrefabAuthoring.CreateText("Text", viewport, string.Empty, 28);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Left;
            text.color = LabelColor;
            text.richText = false;

            TextMeshProUGUI hint = PrefabAuthoring.CreateText("Placeholder", viewport, placeholder, 26);
            Stretch((RectTransform)hint.transform);
            hint.alignment = TextAlignmentOptions.Left;
            hint.color = new Color(1f, 1f, 1f, 0.4f);

            var input = fieldGo.AddComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            return input;
        }

        private static Toggle AddToggle(RectTransform parent, string name, string label)
        {
            RectTransform row = NextRow(parent, name, RowHeight);

            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            boxGo.transform.SetParent(row, false);
            var boxRect = (RectTransform)boxGo.transform;
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.sizeDelta = new Vector2(36f, 36f);
            boxGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);

            Image check = PrefabAuthoring.CreateImageNode("Check", boxRect);
            Stretch((RectTransform)check.transform);
            check.color = new Color(0.5f, 0.9f, 0.6f, 1f);

            TextMeshProUGUI text = PrefabAuthoring.CreateText("Label", row, label, 26);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(46f, 0f);
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Left;
            text.color = LabelColor;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxGo.GetComponent<Image>();
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>
        /// The shared button helper sizes its caption for the HUD, which is twice as wide as this
        /// panel; the longest caption here would otherwise run past the window edge.
        /// </summary>
        private static void SetFontSize(Component target, float size)
        {
            TMP_Text label = target.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = size;
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
