using Match3.Bootstrap;
using Match3.Content;
using Match3.Hud;
using Match3.Levels.Authoring;
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

        [MenuItem("Match3/Authoring/Generate Game Scene")]
        public static void GenerateGameScene()
        {
            SceneAuthoring.EnsureFolder("Assets/Game/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Canvas canvas = SceneAuthoring.CreateUiCanvas("GameCanvas", sortOrder: 0);

            RectTransform background = SceneAuthoring.CreateStretchedChild(canvas.transform, "Background");
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.14f, 0.19f, 1f);
            backgroundImage.raycastTarget = false;

            RectTransform boardArea = CreateBoardArea(canvas.transform);
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

            Text value = PrefabAuthoring.CreateText("Value", rect, "0", 96);
            var valueRect = (RectTransform)value.transform;
            valueRect.anchorMin = new Vector2(0f, 0.35f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            Text caption = PrefabAuthoring.CreateText("Caption", rect, "ходов", 32);
            var captionRect = (RectTransform)caption.transform;
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(1f, 0.35f);
            captionRect.offsetMin = Vector2.zero;
            captionRect.offsetMax = Vector2.zero;
            caption.color = new Color(0.75f, 0.78f, 0.85f, 1f);

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

            Image icon = PrefabAuthoring.CreateImageNode("Icon", rect);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(90f, 90f);
            icon.preserveAspect = true;

            Text counter = PrefabAuthoring.CreateText("Counter", rect, "0/0", 34);
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
            tick.sprite = PrefabAuthoring.LoadSprite("T_Fx_Glow_2D");
            tick.color = new Color(0.45f, 0.9f, 0.5f, 1f);
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

            RectTransform panel = CreatePopupPanel(root, out Text titleLabel, out Button primary,
                out Text primaryLabel, title, button);

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

            RectTransform panel = CreatePopupPanel(root, out Text titleLabel, out Button primary,
                out Text primaryLabel, HudStringsSafe.EndTitle, HudStringsSafe.PlayAgain);

            Text last = PrefabAuthoring.CreateText("LastLevel", panel, string.Empty, 40);
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
            out Text titleLabel,
            out Button primary,
            out Text primaryLabel,
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
            panelImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                ArtAuthoring.HudArtFolder + "/T_Ui_Panel_2D.png");
            panelImage.color = new Color(0.16f, 0.19f, 0.26f, 1f);

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

            primaryLabel = primary.GetComponentInChildren<Text>();
            return panel;
        }

        private static void WirePopup(
            PopupView view,
            CanvasGroup group,
            RectTransform panel,
            Text title,
            Button primary,
            Text primaryLabel)
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
