#if MATCH3_CHEATS
using Match3.Cheats;
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

        private const float RowHeight = 62f;
        private const float RowGap = 6f;
        private const float PanelWidth = 460f;

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

            // Fully transparent but raycastable: it only exists to catch the placement tap, and
            // the presenter enables it just while a booster is armed.
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            var catcher = rect.gameObject.AddComponent<CheatBoardTapCatcher>();
            PrefabAuthoring.Wire(catcher, "_raycastArea", image);
            rect.gameObject.SetActive(false);
            return catcher;
        }

        private static CheatGridOverlayView CreateGridOverlay(Transform parent)
        {
            RectTransform rect = LevelContextAuthoring.CreateStretched("GridOverlay", parent);
            RectTransform labelRoot = LevelContextAuthoring.CreateStretched("LabelRoot", rect);

            Text template = PrefabAuthoring.CreateText("LabelTemplate", labelRoot, "(0, 0)", 22);
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
            rect.gameObject.SetActive(false);
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
            windowRect.sizeDelta = new Vector2(PanelWidth, 1400f);

            var windowImage = window.GetComponent<Image>();
            windowImage.color = new Color(0.10f, 0.12f, 0.17f, 0.96f);

            _cursor = -20f;

            Text title = AddLabel(windowRect, "Title", "Читы", 40);
            Button close = AddButton(windowRect, "CloseButton", "Закрыть");

            InputField levelInput = AddInput(windowRect, "LevelInput", "Номер уровня");
            Button goTo = AddButton(windowRect, "GoToLevelButton", "Перейти");
            Text levelStatus = AddLabel(windowRect, "LevelStatus", string.Empty, 26);

            Button win = AddButton(windowRect, "WinLevelButton", "Выиграть уровень");
            Button lose = AddButton(windowRect, "LoseLevelButton", "Проиграть уровень");
            Button addMoves = AddButton(windowRect, "AddMovesButton", "+5 ходов");

            Button rocket = AddButton(windowRect, "RocketBoosterButton", "Ракета");
            Button bomb = AddButton(windowRect, "BombBoosterButton", "Бомба");
            Button rainbow = AddButton(windowRect, "RainbowBoosterButton", "Радужный шар");
            Button airplane = AddButton(windowRect, "AirplaneBoosterButton", "Самолётик");
            Button disarm = AddButton(windowRect, "DisarmBoosterButton", "Убрать выбор");
            Text armedLabel = AddLabel(windowRect, "ArmedBoosterLabel", string.Empty, 26);
            Image armedIcon = AddIcon(windowRect, "ArmedBoosterIcon");

            Text seedLabel = AddLabel(windowRect, "SeedLabel", "Seed:", 26);
            InputField seedInput = AddInput(windowRect, "SeedInput", "Seed");
            Button applySeed = AddButton(windowRect, "ApplySeedButton", "Применить");
            Button restart = AddButton(windowRect, "RestartAttemptButton", "Заново");

            Toggle freeMoves = AddToggle(windowRect, "FreeMovesToggle", "Ходы бесплатно");
            Toggle grid = AddToggle(windowRect, "CoordinateGridToggle", "Показать сетку координат");
            Toggle hints = AddToggle(windowRect, "DisableHintsToggle", "Отключить подсказки");
            Button hintNow = AddButton(windowRect, "HintNowButton", "Подсказка сейчас");
            Button dump = AddButton(windowRect, "DumpTranscriptButton", "Дамп транскрипта в лог");

            windowRect.sizeDelta = new Vector2(PanelWidth, Mathf.Abs(_cursor) + 30f);

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

        private static RectTransform NextRow(RectTransform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(16f, 0f);
            rect.offsetMax = new Vector2(-16f, 0f);
            rect.anchoredPosition = new Vector2(0f, _cursor);
            rect.sizeDelta = new Vector2(-32f, height);

            _cursor -= height + RowGap;
            return rect;
        }

        private static Text AddLabel(RectTransform parent, string name, string content, int size)
        {
            RectTransform row = NextRow(parent, name, size + 14f);
            Text text = PrefabAuthoring.CreateText("Text", row, content, size);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private static Image AddIcon(RectTransform parent, string name)
        {
            RectTransform row = NextRow(parent, name, 56f);
            Image icon = PrefabAuthoring.CreateImageNode("Icon", row);
            var rect = (RectTransform)icon.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(52f, 52f);
            icon.preserveAspect = true;
            icon.enabled = false;
            return icon;
        }

        private static Button AddButton(RectTransform parent, string name, string label)
        {
            RectTransform row = NextRow(parent, name, RowHeight);
            Button button = PrefabAuthoring.CreateButton("Button", row, label, new Vector2(0f, RowHeight));
            Stretch((RectTransform)button.transform);
            return button;
        }

        private static InputField AddInput(RectTransform parent, string name, string placeholder)
        {
            RectTransform row = NextRow(parent, name, RowHeight);

            var fieldGo = new GameObject("Field", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fieldGo.transform.SetParent(row, false);
            Stretch((RectTransform)fieldGo.transform);
            fieldGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

            Text text = PrefabAuthoring.CreateText("Text", (RectTransform)fieldGo.transform, string.Empty, 30);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            Text hint = PrefabAuthoring.CreateText("Placeholder", (RectTransform)fieldGo.transform, placeholder, 28);
            Stretch((RectTransform)hint.transform);
            hint.alignment = TextAnchor.MiddleLeft;
            hint.color = new Color(1f, 1f, 1f, 0.4f);

            var input = fieldGo.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = InputField.ContentType.IntegerNumber;
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
            boxRect.sizeDelta = new Vector2(40f, 40f);
            boxGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);

            Image check = PrefabAuthoring.CreateImageNode("Check", boxRect);
            Stretch((RectTransform)check.transform);
            check.color = new Color(0.5f, 0.9f, 0.6f, 1f);

            Text text = PrefabAuthoring.CreateText("Label", row, label, 28);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(52f, 0f);
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleLeft;

            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxGo.GetComponent<Image>();
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
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
