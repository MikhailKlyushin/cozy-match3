using Match3.Bootstrap;
using Match3.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Match3.EditorTools
{
    /// <summary>
    /// Builds the LevelContext prefab: the object whose destruction ends one attempt (A08).
    /// Its GameObjectContext installs <see cref="LevelInstaller"/>, so the whole attempt lives
    /// and dies with this object.
    /// </summary>
    internal static class LevelContextAuthoring
    {
        private const string PrefabFolder = "Assets/Game/Content/Gameplay/Prefabs";
        private const string ContextPath = PrefabFolder + "/VAR_LevelContext.prefab";

        [MenuItem("Match3/Authoring/Generate Level Context Prefab")]
        public static void GenerateLevelContext()
        {
            SceneAuthoring.EnsureFolder(PrefabFolder);

            var chipPrefab = AssetDatabase.LoadAssetAtPath<ChipView>(PrefabFolder + "/VAR_ChipView.prefab");
            var elementPrefab = AssetDatabase.LoadAssetAtPath<ElementView>(PrefabFolder + "/VAR_ElementView.prefab");
            var cellPrefab = AssetDatabase.LoadAssetAtPath<Image>(PrefabFolder + "/VAR_BoardCell.prefab");

            if (chipPrefab == null || elementPrefab == null || cellPrefab == null)
            {
                Debug.LogError("[Match3] View prefabs are missing - run Generate View Prefabs first");
                return;
            }

            var root = new GameObject("VAR_LevelContext", typeof(RectTransform));
            Stretch((RectTransform)root.transform);

            RectTransform boardRoot = CreateStretched("BoardRoot", root.transform);
            RectTransform mask = CreateStretched("BoardMask", boardRoot);
            mask.gameObject.AddComponent<RectMask2D>();

            RectTransform cellLayer = CreateStretched("CellLayer", mask);
            RectTransform elementLayer = CreateStretched("ElementLayer", mask);
            RectTransform chipLayer = CreateStretched("ChipLayer", mask);

            // FX sit OUTSIDE the mask: an airplane flies over the frame, and a chip spawns one
            // cell above the top row, where the mask would clip it (§11.3).
            RectTransform fxLayer = CreateStretched("FxLayer", boardRoot);

            var boardView = boardRoot.gameObject.AddComponent<BoardView>();
            PrefabAuthoring.Wire(boardView, "_boardRoot", boardRoot);
            PrefabAuthoring.Wire(boardView, "_cellLayer", cellLayer);
            PrefabAuthoring.Wire(boardView, "_elementLayer", elementLayer);
            PrefabAuthoring.Wire(boardView, "_chipLayer", chipLayer);
            PrefabAuthoring.Wire(boardView, "_fxLayer", fxLayer);
            PrefabAuthoring.Wire(boardView, "_chipPrefab", chipPrefab);
            PrefabAuthoring.Wire(boardView, "_elementPrefab", elementPrefab);
            PrefabAuthoring.Wire(boardView, "_cellPrefab", cellPrefab);
            PrefabAuthoring.WireInt(boardView, "_chipPrewarm", 96);
            PrefabAuthoring.WireInt(boardView, "_elementPrewarm", 32);

            var input = boardRoot.gameObject.AddComponent<BoardInputPresenter>();

            HintView hintView = CreateHintView(root.transform);

            var context = root.AddComponent<GameObjectContext>();
            var installer = root.AddComponent<LevelInstaller>();
            PrefabAuthoring.Wire(installer, "_boardView", boardView);
            PrefabAuthoring.Wire(installer, "_input", input);
            PrefabAuthoring.Wire(installer, "_hintView", hintView);

            WireInstallers(context, installer);

            PrefabUtility.SaveAsPrefabAsset(root, ContextPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] Level context prefab generated: " + ContextPath);
        }

        /// <summary>
        /// Zenject keeps a context's MonoInstaller list in _monoInstallers; older versions called
        /// it _installers, which is still listed as a FormerlySerializedAs alias.
        /// </summary>
        internal static void WireInstallers(Context context, MonoInstaller installer)
        {
            var so = new SerializedObject(context);
            SerializedProperty installers = so.FindProperty("_monoInstallers")
                                            ?? so.FindProperty("_installers");
            if (installers == null)
            {
                Debug.LogError("[Match3] Zenject Context exposes no installer list in this version");
                return;
            }

            installers.arraySize = 1;
            installers.GetArrayElementAtIndex(0).objectReferenceValue = installer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static HintView CreateHintView(Transform parent)
        {
            RectTransform root = CreateStretched("HintRoot", parent);
            root.gameObject.AddComponent<CanvasGroup>();

            Image first = PrefabAuthoring.CreateImageNode("FirstMarker", root);
            Image second = PrefabAuthoring.CreateImageNode("SecondMarker", root);
            Image arrow = PrefabAuthoring.CreateImageNode("Arrow", root);

            Sprite marker = PrefabAuthoring.LoadSprite("T_Board_Cell_2D");
            Sprite glow = PrefabAuthoring.LoadSprite("T_Fx_Glow_2D");
            first.sprite = marker;
            second.sprite = marker;
            arrow.sprite = glow;

            var hint = root.gameObject.AddComponent<HintView>();
            PrefabAuthoring.Wire(hint, "_rect", root);
            PrefabAuthoring.Wire(hint, "_group", root.GetComponent<CanvasGroup>());
            PrefabAuthoring.Wire(hint, "_firstMarker", first);
            PrefabAuthoring.Wire(hint, "_secondMarker", second);
            PrefabAuthoring.Wire(hint, "_arrow", arrow);
            return hint;
        }

        internal static RectTransform CreateStretched(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
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
