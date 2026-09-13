using Match3.Bootstrap;
using Match3.Content;
using Match3.Hud;
using Match3.Levels.Authoring;
using TMPro;
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
    /// Builds Game.unity: the canvas, the board area, the HUD, the three popups and the scene
    /// context. Generated rather than hand-built so the layout is reviewable in a diff and
    /// reproducible on a clean checkout.
    /// </summary>
    internal static class GameSceneAuthoring
    {
        private const string ScenePath = "Assets/Game/Scenes/Game.unity";
        private const string ConfigFolder = "Assets/Game/Content/Gameplay/Configs";
        private const string LevelsFolder = "Assets/Game/Content/Levels";
        private const string PrefabFolder = "Assets/Game/Content/Gameplay/Prefabs";

        private const float HudTopHeight = 260f;
        private const float BoardMargin = 30f;
        private const int GoalRowCount = 3;

        /// <summary>`T_Ui_Background_2D` is drawn square; the fitter needs to know that.</summary>
        private const float BackgroundAspect = 1f;

        /// <summary>Canvas units the plate sticks out past the outermost cells.</summary>
        private const float BoardPlatePadding = 24f;

        private const float MascotWidth = 420f;
        private const float MascotHeight = 320f;

        [MenuItem("Match3/Authoring/Generate Game Scene")]
        public static void GenerateGameScene()
        {
            SceneAuthoring.EnsureFolder("Assets/Game/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Canvas canvas = SceneAuthoring.CreateUiCanvas("GameCanvas", sortOrder: 0);

            CreateBackground(canvas.transform);

            RectTransform boardArea = CreateBoardArea(canvas.transform);
            CreateMascot(boardArea);
            CreateBoardPlate(boardArea);
            RectTransform hudTop = CreateHudZone(canvas.transform);

            MovesCounterView movesCounter = CreateMovesCounter(hudTop);
            GoalsPanelView goalsPanel = CreateGoalsPanel(hudTop, "GoalsPanel", new Vector2(0.5f, 1f));
            HudActionsView hudActions = CreateHudActions(hudTop);

            RectTransform popupRoot = SceneAuthoring.CreateStretchedChild(canvas.transform, "PopupRoot");
            var popups = new PopupView[]
            {
                CreateOutcomePopup<WinPopupView>(popupRoot, "WinPopup", HudStringsSafe.WinTitle, HudStringsSafe.Next),
                CreateOutcomePopup<LosePopupView>(popupRoot, "LosePopup", HudStringsSafe.LoseTitle, HudStringsSafe.Retry),
                CreateEndOfContentPopup(popupRoot),
            };

            CreateSceneContext(boardArea, movesCounter, goalsPanel, hudActions, popups);
            SceneAuthoring.CreateEventSystemObject();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Match3] Game scene generated: " + ScenePath);
        }

        private static RectTransform CreateBoardArea(Transform parent)
        {
            RectTransform area = LevelContextAuthoring.CreateStretched("BoardArea", parent);
            area.offsetMin = new Vector2(BoardMargin, BoardMargin);
            area.offsetMax = new Vector2(-BoardMargin, -HudTopHeight);
            return area;
        }

        /// <summary>
        /// The room the art draws (`art-direction.md` §2.2). The drawing is square and the frame is
        /// 9:16, so it is enveloped rather than stretched: stretching would pull the floor boards to
        /// almost twice their height and the perspective with them.
        /// </summary>
        private static void CreateBackground(Transform parent)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.sprite = LoadHudSprite("T_Ui_Background_2D");
            image.color = Color.white;
            image.raycastTarget = false;

            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = BackgroundAspect;
        }

        /// <summary>
        /// The plate under the grid. A fitter keeps it square and as large as the area allows,
        /// which is exactly the bounding square of a square board (`BoardLayout.CellSize` is
        /// min(area / width, area / height)), so it follows the board through every resize without
        /// a component of its own.
        /// </summary>
        private static void CreateBoardPlate(Transform parent)
        {
            var go = new GameObject("BoardPlate", typeof(RectTransform), typeof(AspectRatioFitter));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            // The padding lives on a stretched child: the fitter drives the parent's size exactly,
            // and a plate flush with the outermost cells reads as a cropped board.
            RectTransform plate = LevelContextAuthoring.CreateStretched("Plate", rect);
            plate.offsetMin = new Vector2(-BoardPlatePadding, -BoardPlatePadding);
            plate.offsetMax = new Vector2(BoardPlatePadding, BoardPlatePadding);

            var image = plate.gameObject.AddComponent<Image>();
            image.sprite = ArtPackAuthoring.LoadUiSprite("button_square_flat");
            image.type = Image.Type.Sliced;
            image.color = Match3Palette.BoardPanel;
            image.raycastTarget = false;
        }

        /// <summary>
        /// The sleeping cat of §11.1. It sits behind the plate, so even in an aspect where the
        /// board square grows past it, it is occluded rather than covering the grid.
        /// </summary>
        private static void CreateMascot(Transform parent)
        {
            Image mascot = PrefabAuthoring.CreateImageNode("Mascot", parent);
            mascot.sprite = LoadHudSprite("T_Ui_Cat_2D");
            mascot.color = Color.white;
            mascot.preserveAspect = true;
            mascot.raycastTarget = false;

            var rect = (RectTransform)mascot.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(MascotWidth, MascotHeight);
        }

        /// <summary>
        /// A nine-sliced backing plate stretched behind its siblings. First child, so whatever the
        /// caller adds next draws on top of it.
        /// </summary>
        private static Image CreatePlate(string name, Transform parent, Color color)
        {
            RectTransform rect = LevelContextAuthoring.CreateStretched(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ArtPackAuthoring.LoadUiSprite("button_square_flat");
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadHudSprite(string assetName)
        {
            string path = ArtAuthoring.HudArtFolder + "/" + assetName + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[Match3] Missing HUD sprite " + path);
            }

            return sprite;
        }

        private static RectTransform CreateHudZone(Transform parent)
        {
            var go = new GameObject("HudTop", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -HudTopHeight);
            rect.offsetMax = new Vector2(0f, 0f);
            return rect;
        }

        private static MovesCounterView CreateMovesCounter(Transform parent)
        {
            var go = new GameObject("MovesCounter", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(30f, -20f);
            rect.sizeDelta = new Vector2(200f, 200f);

            CreatePlate("Plate", rect, Match3Palette.PanelFill);

            TextMeshProUGUI value = PrefabAuthoring.CreateText("Value", rect, "0", 96);
            var valueRect = (RectTransform)value.transform;
            valueRect.anchorMin = new Vector2(0f, 0.35f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            TextMeshProUGUI caption = PrefabAuthoring.CreateText("Caption", rect, "ходов", 32);
            var captionRect = (RectTransform)caption.transform;
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(1f, 0.35f);
            captionRect.offsetMin = Vector2.zero;
            captionRect.offsetMax = Vector2.zero;
            caption.color = Match3Palette.TextSecondary;

            var view = go.AddComponent<MovesCounterView>();
            PrefabAuthoring.Wire(view, "_valueLabel", value);
            PrefabAuthoring.Wire(view, "_captionLabel", caption);
            PrefabAuthoring.Wire(view, "_pulseTarget", rect);
            return view;
        }

        private static GoalsPanelView CreateGoalsPanel(Transform parent, string name, Vector2 anchor)
        {
            var go = new GameObject("GoalsPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.name = name;
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(0f, -30f);
            rect.sizeDelta = new Vector2(520f, 150f);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 24f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rows = new Object[GoalRowCount];
            for (int i = 0; i < GoalRowCount; i++)
            {
                rows[i] = CreateGoalRow(rect, i);
            }

            var view = go.AddComponent<GoalsPanelView>();
            PrefabAuthoring.WireArray(view, "_rows", rows);
            return view;
        }

        private static GoalRowView CreateGoalRow(Transform parent, int index)
        {
            var go = new GameObject("GoalRow" + index.ToString(), typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(150f, 140f);

            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 150f;
            element.preferredHeight = 140f;

            CreatePlate("Slot", rect, Match3Palette.PanelFill);

            Image icon = PrefabAuthoring.CreateImageNode("Icon", rect);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(90f, 90f);
            icon.preserveAspect = true;

            TextMeshProUGUI counter = PrefabAuthoring.CreateText("Counter", rect, "0/0", 34);
            var counterRect = (RectTransform)counter.transform;
            counterRect.anchorMin = new Vector2(0f, 0f);
            counterRect.anchorMax = new Vector2(1f, 0f);
            counterRect.pivot = new Vector2(0.5f, 0f);
            counterRect.anchoredPosition = Vector2.zero;
            counterRect.sizeDelta = new Vector2(0f, 42f);

            Image tick = PrefabAuthoring.CreateImageNode("ClosedTick", rect);
            var tickRect = (RectTransform)tick.transform;
            tickRect.anchorMin = new Vector2(1f, 1f);
            tickRect.anchorMax = new Vector2(1f, 1f);
            tickRect.pivot = new Vector2(1f, 1f);
            tickRect.sizeDelta = new Vector2(40f, 40f);
            // A checkmark glyph, not a green blob: the glow sprite carried no shape, so a closed
            // goal read as a smear of colour.
            tick.sprite = ArtPackAuthoring.LoadUiSprite("icon_checkmark");
            tick.preserveAspect = true;
            tick.color = Match3Palette.Success;
            tick.enabled = false;

            var view = go.AddComponent<GoalRowView>();
            PrefabAuthoring.Wire(view, "_icon", icon);
            PrefabAuthoring.Wire(view, "_counterLabel", counter);
            PrefabAuthoring.Wire(view, "_closedTick", tick);
            PrefabAuthoring.Wire(view, "_pulseTarget", rect);
            return view;
        }

        private static HudActionsView CreateHudActions(Transform parent)
        {
            var go = new GameObject("HudActions", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-30f, -20f);
            rect.sizeDelta = new Vector2(220f, 200f);

            Button restart = PrefabAuthoring.CreateButton("RestartButton", rect, "Заново", new Vector2(200f, 80f));
            var restartRect = (RectTransform)restart.transform;
            restartRect.anchorMin = new Vector2(1f, 1f);
            restartRect.anchorMax = new Vector2(1f, 1f);
            restartRect.pivot = new Vector2(1f, 1f);
            restartRect.anchoredPosition = Vector2.zero;

            Button cheats = PrefabAuthoring.CreateButton("CheatsButton", rect, "Читы", new Vector2(200f, 80f));
            var cheatsRect = (RectTransform)cheats.transform;
            cheatsRect.anchorMin = new Vector2(1f, 1f);
            cheatsRect.anchorMax = new Vector2(1f, 1f);
            cheatsRect.pivot = new Vector2(1f, 1f);
            cheatsRect.anchoredPosition = new Vector2(0f, -96f);

            var view = go.AddComponent<HudActionsView>();
            PrefabAuthoring.Wire(view, "_restartButton", restart);
            PrefabAuthoring.Wire(view, "_cheatsButton", cheats);
            return view;
        }

        private static T CreateOutcomePopup<T>(Transform parent, string name, string title, string button)
            where T : PopupView
        {
            RectTransform root = LevelContextAuthoring.CreateStretched(name, parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform panel = CreatePopupPanel(root, out TextMeshProUGUI titleLabel, out Button primary,
                out TextMeshProUGUI primaryLabel, title, button);

            GoalsPanelView goals = CreateGoalsPanel(panel, "PopupGoals", new Vector2(0.5f, 0.5f));

            var view = root.gameObject.AddComponent<T>();
            WirePopup(view, group, panel, titleLabel, primary, primaryLabel);
            PrefabAuthoring.Wire(view, "_goalsPanel", goals);
            return view;
        }

        private static EndOfContentPopupView CreateEndOfContentPopup(Transform parent)
        {
            RectTransform root = LevelContextAuthoring.CreateStretched("EndOfContentPopup", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform panel = CreatePopupPanel(root, out TextMeshProUGUI titleLabel, out Button primary,
                out TextMeshProUGUI primaryLabel, HudStringsSafe.EndTitle, HudStringsSafe.PlayAgain);

            TextMeshProUGUI last = PrefabAuthoring.CreateText("LastLevel", panel, string.Empty, 40);
            var lastRect = (RectTransform)last.transform;
            lastRect.anchorMin = new Vector2(0f, 0.5f);
            lastRect.anchorMax = new Vector2(1f, 0.5f);
            lastRect.pivot = new Vector2(0.5f, 0.5f);
            lastRect.anchoredPosition = Vector2.zero;
            lastRect.sizeDelta = new Vector2(0f, 60f);

            var view = root.gameObject.AddComponent<EndOfContentPopupView>();
            WirePopup(view, group, panel, titleLabel, primary, primaryLabel);
            PrefabAuthoring.Wire(view, "_lastLevelLabel", last);
            return view;
        }

        private static RectTransform CreatePopupPanel(
            RectTransform root,
            out TextMeshProUGUI titleLabel,
            out Button primary,
            out TextMeshProUGUI primaryLabel,
            string title,
            string button)
        {
            RectTransform dim = LevelContextAuthoring.CreateStretched("Dim", root);
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.6f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(root, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 700f);

            var panelImage = panelGo.GetComponent<Image>();
            panelImage.sprite = LoadHudSprite("T_Ui_Panel_2D");
            // Sliced, and the sprite now carries a border: the corners used to stretch with the
            // panel and the rounding turned into an oval.
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Match3Palette.PanelFill;

            titleLabel = PrefabAuthoring.CreateText("Title", panel, title, 56);
            var titleRect = (RectTransform)titleLabel.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(0f, 80f);

            primary = PrefabAuthoring.CreateButton("PrimaryButton", panel, button, new Vector2(360f, 96f));
            var primaryRect = (RectTransform)primary.transform;
            primaryRect.anchorMin = new Vector2(0.5f, 0f);
            primaryRect.anchorMax = new Vector2(0.5f, 0f);
            primaryRect.pivot = new Vector2(0.5f, 0f);
            primaryRect.anchoredPosition = new Vector2(0f, 50f);

            primaryLabel = primary.GetComponentInChildren<TextMeshProUGUI>();
            return panel;
        }

        private static void WirePopup(
            PopupView view,
            CanvasGroup group,
            RectTransform panel,
            TMP_Text title,
            Button primary,
            TMP_Text primaryLabel)
        {
            PrefabAuthoring.Wire(view, "_canvasGroup", group);
            PrefabAuthoring.Wire(view, "_panel", panel);
            PrefabAuthoring.Wire(view, "_titleLabel", title);
            PrefabAuthoring.Wire(view, "_primaryButton", primary);
            PrefabAuthoring.Wire(view, "_primaryButtonLabel", primaryLabel);
        }

        private static void CreateSceneContext(
            RectTransform boardArea,
            MovesCounterView movesCounter,
            GoalsPanelView goalsPanel,
            HudActionsView hudActions,
            PopupView[] popups)
        {
            var go = new GameObject("SceneContext", typeof(SceneContext));
            var installer = go.AddComponent<GameInstaller>();

            PrefabAuthoring.Wire(installer, "_levelCatalog",
                AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelsFolder + "/LevelCatalog.asset"));
            PrefabAuthoring.Wire(installer, "_timings",
                AssetDatabase.LoadAssetAtPath<TimingProfile>(ConfigFolder + "/TimingProfile.asset"));
            PrefabAuthoring.Wire(installer, "_chipProfile",
                AssetDatabase.LoadAssetAtPath<ChipVisualProfile>(ConfigFolder + "/ChipVisualProfile.asset"));
            PrefabAuthoring.Wire(installer, "_elementProfile",
                AssetDatabase.LoadAssetAtPath<ElementVisualProfile>(ConfigFolder + "/ElementVisualProfile.asset"));
            PrefabAuthoring.Wire(installer, "_levelContextPrefab",
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/VAR_LevelContext.prefab"));
            PrefabAuthoring.Wire(installer, "_levelContextParent", boardArea);
            PrefabAuthoring.Wire(installer, "_movesCounter", movesCounter);
            PrefabAuthoring.Wire(installer, "_goalsPanel", goalsPanel);
            PrefabAuthoring.Wire(installer, "_hudActions", hudActions);
            PrefabAuthoring.WireArray(installer, "_popups", popups);

#if MATCH3_CHEATS
            // §15: the field only exists with the define, so a release build cannot reference it.
            PrefabAuthoring.Wire(
                installer,
                "_cheatsPrefab",
                AssetDatabase.LoadAssetAtPath<Match3.Cheats.CheatsRootView>(
                    "Assets/Game/Content/Cheats/Prefabs/VAR_CheatsRoot.prefab"));
#endif

            LevelContextAuthoring.WireInstallers(go.GetComponent<SceneContext>(), installer);
        }

        /// <summary>
        /// Mirrors the popup captions of §11.1. Kept here rather than reaching into Match3.Hud's
        /// internal strings, so the generator does not constrain how the HUD stores them.
        /// </summary>
        private static class HudStringsSafe
        {
            internal const string WinTitle = "Уровень пройден";
            internal const string Next = "Далее";
            internal const string LoseTitle = "Ходы закончились";
            internal const string Retry = "Заново";
            internal const string EndTitle = "Все уровни пройдены";
            internal const string PlayAgain = "Играть заново с 1-го уровня";
        }
    }
}
