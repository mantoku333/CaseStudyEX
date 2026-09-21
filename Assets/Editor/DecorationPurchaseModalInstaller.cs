using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DecorationPurchaseModalInstaller
{
    private const string PrefabPath = "Assets/Prefabs/UI/OptionsCanvas.prefab";
    private const string DecorationPath = "MenuRoot/OptionPanel/Decoration";
    private const string ResourceRoot = "Assets/Resources/Figma/Design2/PurchaseConfirm/";

    static DecorationPurchaseModalInstaller()
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
            DecorationPage page = prefabRoot.GetComponentInChildren<DecorationPage>(true);
            if (page == null)
                return;

            SerializedObject serializedPage = new SerializedObject(page);
            SerializedProperty purchaseModal = serializedPage.FindProperty("purchaseModal");
            if (purchaseModal != null && purchaseModal.objectReferenceValue != null)
                return;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        Install();
    }

    [MenuItem("Tools/Decoration Purchase Modal/Install")]
    public static void Install()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError("[DecorationPurchaseModalInstaller] OptionsCanvas prefab not found.");
            return;
        }

        try
        {
            DecorationPage page = prefabRoot.GetComponentInChildren<DecorationPage>(true);
            if (page == null)
            {
                Debug.LogError("[DecorationPurchaseModalInstaller] DecorationPage not found.");
                return;
            }

            Transform decoration = prefabRoot.transform.Find(DecorationPath);
            if (decoration == null)
            {
                Debug.LogError("[DecorationPurchaseModalInstaller] Decoration root not found.");
                return;
            }

            Transform existing = decoration.Find("PurchaseConfirmModal");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            TextMeshProUGUI fallbackText = page.GetComponentInChildren<TextMeshProUGUI>(true);
            TMP_FontAsset font = fallbackText != null ? fallbackText.font : null;

            GameObject modal = CreateUiObject("PurchaseConfirmModal", decoration);
            RectTransform modalRect = (RectTransform)modal.transform;
            SetCenteredRect(modalRect, Vector2.zero, new Vector2(1920f, 1080f));
            Image overlay = modal.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.84f);
            overlay.raycastTarget = true;

            GameObject panel = CreateUiObject("PurchaseModalContent", modal.transform);
            Stretch((RectTransform)panel.transform);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(1f, 1f, 1f, 0f);
            panelImage.raycastTarget = false;

            CreateFigmaImage(panel.transform, "PurchaseModalBackground", "back", -220f, 398f, 2659f, 251f);
            CreateFigmaImage(panel.transform, "PurchaseTitleImage", "title_purchase", 786f, 463f, 347f, 49f);
            CreateFigmaImage(panel.transform, "OwnedPointFrame", "balance_frame", 810f, 520f, 295f, 45f);
            CreateFigmaImage(panel.transform, "ElegantPointIcon", "star", 914f, 526f, 32f, 31f);
            CreateFigmaImage(panel.transform, "PriceSeparatorIcon", "multiply", 954f, 533f, 16f, 16f);

            Image itemImage = CreateImage("PurchaseItemImage", panel.transform, null, new Vector2(-205f, 45f), new Vector2(180f, 180f));
            itemImage.gameObject.SetActive(false);

            TextMeshProUGUI itemName = CreateText("PurchaseItemNameText", panel.transform, font, 22f, TextAlignmentOptions.Center, Color.white);
            itemName.fontStyle = FontStyles.Bold;
            SetFigmaRect(itemName.rectTransform, 710f, 430f, 500f, 34f);
            itemName.gameObject.SetActive(false);

            TextMeshProUGUI price = CreateText("PurchasePriceText", panel.transform, font, 22f, TextAlignmentOptions.Center, Color.white);
            price.fontStyle = FontStyles.Bold;
            price.enableAutoSizing = true;
            price.fontSizeMin = 18f;
            price.fontSizeMax = 22f;
            SetFigmaRect(price.rectTransform, 978f, 526f, 50f, 31f);

            TextMeshProUGUI balance = CreateText("OwnedPointText", panel.transform, font, 22f, TextAlignmentOptions.Center, Color.white);
            balance.fontStyle = FontStyles.Bold;
            balance.enableAutoSizing = true;
            balance.fontSizeMin = 18f;
            balance.fontSizeMax = 22f;
            SetFigmaRect(balance.rectTransform, 1028f, 526f, 70f, 31f);

            TextMeshProUGUI status = CreateText("PurchaseStatusText", panel.transform, font, 20f, TextAlignmentOptions.Center, new Color(0.72f, 0.1f, 0.18f, 1f));
            SetFigmaRect(status.rectTransform, 710f, 568f, 500f, 30f);
            status.gameObject.SetActive(false);

            Button yesButton = CreateSpriteButton("PurchaseYesButton", panel.transform, "yes_normal", "yes_selected", 594f, 594f, 346f, 80f);
            Button noButton = CreateSpriteButton("PurchaseNoButton", panel.transform, "no_normal", "no_selected", 981f, 594f, 346f, 80f);

            modal.SetActive(false);

            SerializedObject serializedPage = new SerializedObject(page);
            SetObject(serializedPage, "purchaseModal", modal);
            SetObject(serializedPage, "purchaseItemImage", itemImage);
            SetObject(serializedPage, "purchaseItemNameText", itemName);
            SetObject(serializedPage, "purchasePriceText", price);
            SetObject(serializedPage, "purchaseBalanceText", balance);
            SetObject(serializedPage, "purchaseStatusText", status);
            SetObject(serializedPage, "purchaseYesButton", yesButton);
            SetObject(serializedPage, "purchaseNoButton", noButton);
            serializedPage.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Debug.Log("[DecorationPurchaseModalInstaller] Purchase modal installed.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = LayerMask.NameToLayer("UI");
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static Image CreateFigmaImage(Transform parent, string name, string spriteName, float x, float y, float width, float height)
    {
        Sprite sprite = LoadSprite(spriteName);
        Image image = CreateImage(name, parent, sprite, Vector2.zero, new Vector2(width, height));
        image.preserveAspect = false;
        SetFigmaRect(image.rectTransform, x, y, width, height);
        return image;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = obj.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static Button CreateSpriteButton(string name, Transform parent, string normalName, string selectedName, float x, float y, float width, float height)
    {
        Image image = CreateFigmaImage(parent, name, normalName, x, y, width, height);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        Sprite normal = LoadSprite(normalName);
        Sprite selected = LoadSprite(selectedName);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = selected,
            selectedSprite = selected,
            pressedSprite = selected,
            disabledSprite = normal
        };
        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject obj = CreateUiObject(name, parent);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourceRoot}{name}.png");
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetFigmaRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x + width * 0.5f - 960f, 540f - y - height * 0.5f);
        rect.sizeDelta = new Vector2(width, height);
    }
}
