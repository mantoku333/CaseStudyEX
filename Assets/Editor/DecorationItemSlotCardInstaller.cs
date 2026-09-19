using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DecorationItemSlotCardInstaller
{
    private const string PrefabPath = "Assets/Prefabs/UI/DecorationItemSlot.prefab";
    private const string SpriteRoot = "Assets/Art/Texture/Deco/newDecoCard/";
    private const string ZenOldMinchoBoldPath = "Assets/Figma/Fonts/ZenOldMincho/TMP/ZenOldMincho-Bold SDF.asset";

    static DecorationItemSlotCardInstaller()
    {
        EditorApplication.delayCall += InstallIfMissing;
    }

    private static void InstallIfMissing()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
            return;

        try
        {
            if (prefabRoot.transform.Find("EquippedState") != null &&
                prefabRoot.transform.Find("LockedState") != null &&
                prefabRoot.transform.Find("UnlockedDescriptionText") != null &&
                prefabRoot.transform.Find("LockIcon") != null)
            {
                return;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        Install();
    }

    [MenuItem("Tools/Decoration Item Slot Card/Install")]
    public static void Install()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError("[DecorationItemSlotCardInstaller] DecorationItemSlot prefab not found.");
            return;
        }

        try
        {
            DecorationItemSlot slot = prefabRoot.GetComponent<DecorationItemSlot>();
            if (slot == null)
            {
                Debug.LogError("[DecorationItemSlotCardInstaller] DecorationItemSlot component not found.");
                return;
            }

            Image background = prefabRoot.transform.Find("ItemCardBg")?.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = LoadSprite("item card 2");
                background.color = Color.white;
                background.preserveAspect = true;
            }

            TMP_FontAsset zenOldMincho = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ZenOldMinchoBoldPath);

            TextMeshProUGUI description = GetOrCreateText(
                prefabRoot.transform,
                "UnlockedDescriptionText",
                zenOldMincho,
                new Vector2(0f, -60f),
                new Vector2(150f, 54f),
                18f,
                TextAlignmentOptions.Center);
            description.text = "説明文";

            GameObject equippedRoot = GetOrCreateRoot(prefabRoot.transform, "EquippedState");
            GetOrCreateImage(
                equippedRoot.transform,
                "EquippedDecorationImage",
                LoadSprite("装備中 装飾 1"),
                new Vector2(0f, -60f),
                new Vector2(150f, 42f));
            GetOrCreateImage(
                equippedRoot.transform,
                "EquippedTextImage",
                LoadSprite("装備中_text 1"),
                new Vector2(0f, -60f),
                new Vector2(120f, 32f));

            GameObject lockedRoot = GetOrCreateRoot(prefabRoot.transform, "LockedState");
            TextMeshProUGUI costText = GetOrCreateText(
                lockedRoot.transform,
                "LockedCostText",
                zenOldMincho,
                new Vector2(-12f, -60f),
                new Vector2(86f, 48f),
                34f,
                TextAlignmentOptions.Right);
            costText.text = "250";
            costText.fontStyle = FontStyles.Bold;

            GetOrCreateImage(
                lockedRoot.transform,
                "PointTextImage",
                LoadSprite("pt_text 1"),
                new Vector2(48f, -60f),
                new Vector2(42f, 22f));

            Image lockIcon = GetOrCreateImage(
                prefabRoot.transform,
                "LockIcon",
                LoadSprite("鍵　アイコン 1"),
                new Vector2(0f, 20f),
                new Vector2(51f, 81f));

            equippedRoot.SetActive(false);
            lockedRoot.SetActive(false);
            description.gameObject.SetActive(false);
            lockIcon.gameObject.SetActive(false);

            SerializedObject serializedSlot = new SerializedObject(slot);
            SetObject(serializedSlot, "unlockedDescriptionText", description);
            SetObject(serializedSlot, "equippedStateRoot", equippedRoot);
            SetObject(serializedSlot, "lockedStateRoot", lockedRoot);
            SetObject(serializedSlot, "lockIconImage", lockIcon);
            SetObject(serializedSlot, "lockedCostText", costText);
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Debug.Log("[DecorationItemSlotCardInstaller] Decoration item slot card installed.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static GameObject GetOrCreateRoot(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject root = CreateUiObject(name, parent);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(180f, 260f);
        return root;
    }

    private static Image GetOrCreateImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        Image image;
        RectTransform rect;
        if (existing != null)
        {
            image = existing.GetComponent<Image>();
            if (image == null)
                image = existing.gameObject.AddComponent<Image>();
            rect = (RectTransform)existing;
        }
        else
        {
            GameObject obj = CreateUiObject(name, parent);
            image = obj.AddComponent<Image>();
            rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static TextMeshProUGUI GetOrCreateText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text;
        RectTransform rect;
        if (existing != null)
        {
            text = existing.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = existing.gameObject.AddComponent<TextMeshProUGUI>();
            rect = text.rectTransform;
        }
        else
        {
            GameObject obj = CreateUiObject(name, parent);
            text = obj.AddComponent<TextMeshProUGUI>();
            rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = LayerMask.NameToLayer("UI");
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteRoot}{name}.png");
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }
}
