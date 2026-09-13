using Match3.Gameplay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.EditorTools
{
    /// <summary>
    /// Builds the view prefabs from the component types. Prefabs are generated rather than
    /// hand-authored so a clean checkout has a working board without binary assets nobody can
    /// review in a diff.
    /// </summary>
    internal static class PrefabAuthoring
    {
        private const string GameplayPrefabFolder = "Assets/Game/Content/Gameplay/Prefabs";
        private const string ArtFolder = ArtAuthoring.GameplayArtFolder;

        [MenuItem("Match3/Authoring/Generate View Prefabs")]
        public static void GeneratePrefabs()
        {
            SceneAuthoring.EnsureFolder(GameplayPrefabFolder);

            CreateChipPrefab();
            CreateElementPrefab();
            CreateCellTilePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Match3] View prefabs generated in " + GameplayPrefabFolder);
        }

        /// <summary>
        /// Assigns a private [SerializeField] by name. The view components declare their
        /// dependencies as serialized fields, and this is what wires them without a human in the
        /// Inspector.
        /// </summary>
        internal static void Wire(Component component, string fieldName, Object value)
        {
            var so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[Match3] " + component.GetType().Name + " has no serialized field " + fieldName);
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void WireFloat(Component component, string fieldName, float value)
        {
            var so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[Match3] " + component.GetType().Name + " has no serialized field " + fieldName);
                return;
            }

            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void WireInt(Component component, string fieldName, int value)
        {
            var so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[Match3] " + component.GetType().Name + " has no serialized field " + fieldName);
                return;
            }

            property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void WireArray(Component component, string fieldName, Object[] values)
        {
            var so = new SerializedObject(component);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError("[Match3] " + component.GetType().Name + " has no array field " + fieldName);
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Every label in the game goes through here, so the font, the wrapping and the auto-size
        /// switch are decided in one place. Auto-size stays off: the move counter and the goal
        /// rows change their string every turn, and resizing on each change is the per-turn CPU
        /// spike §14 forbids.
        /// </summary>
        internal static TextMeshProUGUI CreateText(string name, Transform parent, string content, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = FontAuthoring.LoadUiFont();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        internal static Button CreateButton(string name, Transform parent, string label, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                ArtAuthoring.HudArtFolder + "/T_Ui_Panel_2D.png");
            image.color = new Color(0.24f, 0.28f, 0.38f, 1f);

            TextMeshProUGUI text = CreateText("Label", rect, label, 34);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        /// <summary>A stretched RectTransform with an Image, the shape every view is built from.</summary>
        internal static Image CreateImageNode(string name, Transform parent, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);

            var image = go.GetComponent<Image>();
            image.raycastTarget = raycast;
            return image;
        }

        internal static Sprite LoadSprite(string assetName)
        {
            string path = ArtFolder + "/" + assetName + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("[Match3] Missing sprite " + path + " - run Generate Placeholder Art first");
            }

            return sprite;
        }

        internal static void SaveAndCleanUp(GameObject root, string assetPath)
        {
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateChipPrefab()
        {
            var root = new GameObject("VAR_ChipView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);

            var image = root.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = LoadSprite(ProceduralArt.ChipAssetNameOf(1));
            image.preserveAspect = true;

            var view = root.AddComponent<ChipView>();
            Wire(view, "_image", image);
            Wire(view, "_rect", rect);

            SaveAndCleanUp(root, GameplayPrefabFolder + "/VAR_ChipView.prefab");
        }

        private static void CreateElementPrefab()
        {
            var root = new GameObject("VAR_ElementView", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);

            Image baseImage = CreateImageNode("Base", root.transform);
            baseImage.sprite = LoadSprite("T_Element_BoxBase_S0_2D");
            baseImage.preserveAspect = true;
            Stretch((RectTransform)baseImage.transform);

            Image bow = CreateImageNode("Bow", root.transform);
            bow.sprite = LoadSprite("T_Element_BoxBow_2D");
            bow.preserveAspect = true;
            Stretch((RectTransform)bow.transform);
            bow.enabled = false;

            Image pip = CreateImageNode("Pip", root.transform);
            pip.sprite = LoadSprite("T_Element_BoxPip_2D");
            pip.preserveAspect = true;
            var pipRect = (RectTransform)pip.transform;
            pipRect.anchorMin = new Vector2(1f, 1f);
            pipRect.anchorMax = new Vector2(1f, 1f);
            pipRect.pivot = new Vector2(1f, 1f);
            pipRect.anchoredPosition = new Vector2(-4f, -4f);
            pipRect.sizeDelta = new Vector2(30f, 30f);
            pip.enabled = false;

            var view = root.AddComponent<ElementView>();
            Wire(view, "_base", baseImage);
            Wire(view, "_bow", bow);
            Wire(view, "_pip", pip);
            Wire(view, "_rect", rect);

            SaveAndCleanUp(root, GameplayPrefabFolder + "/VAR_ElementView.prefab");
        }

        private static void CreateCellTilePrefab()
        {
            var root = new GameObject("VAR_BoardCell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);

            var image = root.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = LoadSprite("T_Board_Cell_2D");

            SaveAndCleanUp(root, GameplayPrefabFolder + "/VAR_BoardCell.prefab");
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
