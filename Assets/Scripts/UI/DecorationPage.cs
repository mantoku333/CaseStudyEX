using System;
using System.Collections.Generic;
using Metroidvania.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DecorationPage : MonoBehaviour
{
    private const string DefaultPurchasePriceFormat = "{0} Pt";
    private const string DefaultPurchaseBalanceFormat = "所持ポイント: {0} / {1}";
    private const string DefaultInsufficientPointsMessage = "優雅ポイントが足りません。";

    [Header("Typography")]
    [SerializeField] private TMP_FontAsset shopFont;

    [Header("Catalog")]
    [SerializeField] private ItemData[] itemCatalog;
    [SerializeField] private int minimumSlotCount = 9;

    [Header("Slots")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private DecorationItemSlot slotPrefab;

    [Header("Details")]
    [SerializeField] private Image previewImage;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Transform abilityTagContainer;
    [SerializeField] private GameObject abilityTagPrefab;

    [Header("Equipment Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button removeButton;

    [Header("Elegant Point Display")]
    [SerializeField] private TextMeshProUGUI elegantPointBalanceText;
    [SerializeField] private string elegantPointBalanceFormat = "{0} / {1}";

    [Header("Purchase Modal")]
    [SerializeField] private GameObject purchaseModal;
    [SerializeField] private Image purchaseItemImage;
    [SerializeField] private TextMeshProUGUI purchaseItemNameText;
    [SerializeField] private TextMeshProUGUI purchasePriceText;
    [SerializeField] private TextMeshProUGUI purchaseBalanceText;
    [SerializeField] private TextMeshProUGUI purchaseStatusText;
    [SerializeField] private Button purchaseYesButton;
    [SerializeField] private Button purchaseNoButton;
    [SerializeField] private string purchasePriceFormat = "{0} Pt";
    [SerializeField] private string purchaseBalanceFormat = "所持ポイント: {0} / {1}";
    [SerializeField] private string insufficientPointsMessage = "優雅ポイントが足りません。";

    private readonly List<DecorationItemSlot> slots = new List<DecorationItemSlot>();
    private ItemData selectedItem;
    private DecorationItemSlot selectedSlot;
    private ItemData pendingPurchaseItem;
    private DecorationItemSlot purchaseReturnSlot;
    private GameObject itemNameBase;
    private Sprite defaultPreviewSprite;
    private bool purchaseModalOpen;

    public bool IsPurchaseModalOpen => purchaseModalOpen;

    private void Awake()
    {
        MigrateLegacyEnglishText();
        ApplyShopFont(itemNameText);
        ApplyShopFont(descriptionText);

        if (itemNameBase == null)
            itemNameBase = transform.Find("item name base")?.gameObject;

        if (previewImage != null)
        {
            defaultPreviewSprite = previewImage.sprite;
            previewImage.preserveAspect = true;
        }

        if (confirmButton != null)
        {
            OptionsCanvasButtonUtility.ConfigureSingleIllustrationButton(confirmButton);
            confirmButton.onClick.AddListener(OnConfirmClicked);
            UIButtonSfxPlayer.Register(confirmButton);
        }

        if (removeButton != null)
        {
            OptionsCanvasButtonUtility.ConfigureSingleIllustrationButton(removeButton);
            removeButton.onClick.AddListener(OnRemoveClicked);
            UIButtonSfxPlayer.Register(removeButton);
        }

        EnsureElegantPointBalanceText();
        EnsurePurchaseModal();
        ConfigurePurchaseButtonNavigation();
        purchaseYesButton.onClick.AddListener(OnPurchaseYesClicked);
        purchaseNoButton.onClick.AddListener(OnPurchaseNoClicked);
        UIButtonSfxPlayer.Register(purchaseYesButton);
        UIButtonSfxPlayer.Register(purchaseNoButton);
        SetPurchaseModalVisible(false);
        ClearRightPanel();
        RefreshElegantPointBalance();
    }

    private void OnEnable()
    {
        ElegantPointWallet.BalanceChanged -= OnElegantPointBalanceChanged;
        ElegantPointWallet.BalanceChanged += OnElegantPointBalanceChanged;
        RefreshElegantPointBalance();
    }

    private void OnDisable()
    {
        ElegantPointWallet.BalanceChanged -= OnElegantPointBalanceChanged;
        ClosePurchaseModal(restoreSlotFocus: false);
    }

    /// <summary>
    /// Rebuilds the catalog. Every configured decoration is shown in canonical
    /// shop order, whether it has been purchased or not.
    /// </summary>
    public void Refresh()
    {
        Refresh(null);
    }

    public bool TryCancelPurchaseModal()
    {
        if (!purchaseModalOpen)
            return false;

        ClosePurchaseModal(restoreSlotFocus: true);
        return true;
    }

    private void Refresh(ItemData preferredSelection)
    {
        if (purchaseModalOpen)
            ClosePurchaseModal(restoreSlotFocus: false);

        ClearSlots();
        selectedItem = null;
        selectedSlot = null;

        List<ItemData> orderedCatalog = BuildOrderedCatalog();
        int totalSlots = Mathf.Max(minimumSlotCount, orderedCatalog.Count);

        for (int i = 0; i < totalSlots; i++)
        {
            ItemData data = i < orderedCatalog.Count ? orderedCatalog[i] : null;
            bool isOwned = data != null && PlayerEquipmentState.HasItem(data);
            bool isEquipped = isOwned && PlayerEquipmentState.IsEquipped(data);

            DecorationItemSlot slot = Instantiate(slotPrefab, slotContainer);
            slot.Initialize(data, isOwned, isEquipped, OnSlotSelected);
            slots.Add(slot);

            if (data != null && (data == preferredSelection || (preferredSelection == null && isEquipped)))
            {
                selectedItem = data;
                selectedSlot = slot;
                slot.SetSelected(true);
            }
        }

        RefreshRightPanel();
        RefreshElegantPointBalance();

        if (preferredSelection != null && selectedSlot != null)
            SelectButton(selectedSlot.Button);
    }

    private List<ItemData> BuildOrderedCatalog()
    {
        var entries = new List<CatalogEntry>();
        if (itemCatalog == null)
            return new List<ItemData>();

        var seen = new HashSet<ItemData>();
        for (int i = 0; i < itemCatalog.Length; i++)
        {
            ItemData data = itemCatalog[i];
            if (data == null || data.itemType != ItemType.Equipment || !seen.Add(data))
                continue;

            entries.Add(new CatalogEntry(data, i, GetCatalogPriority(data)));
        }

        entries.Sort((left, right) =>
        {
            int priorityComparison = left.Priority.CompareTo(right.Priority);
            return priorityComparison != 0
                ? priorityComparison
                : left.SourceIndex.CompareTo(right.SourceIndex);
        });

        var ordered = new List<ItemData>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
            ordered.Add(entries[i].Data);

        return ordered;
    }

    private static int GetCatalogPriority(ItemData data)
    {
        string itemName = NormalizeCatalogName(data != null ? data.itemName : string.Empty);
        string assetName = NormalizeCatalogName(data != null ? data.name : string.Empty);

        if (itemName == "blueaura" || assetName == "blueauraequipment")
            return 0;
        if (itemName == "redaura" || assetName == "redauraequipment")
            return 1;
        if (itemName == "arcanciel" || assetName == "arcancielequipment")
            return 2;

        return 100;
    }

    private static string NormalizeCatalogName(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                Destroy(slots[i].gameObject);
        }

        slots.Clear();
    }

    private void OnSlotSelected(DecorationItemSlot slot)
    {
        if (slot == null || slot.ItemData == null || purchaseModalOpen)
            return;

        if (!slot.IsOwned)
        {
            if (slot.ItemData.elegantPointCost > 0)
                OpenPurchaseModal(slot);
            return;
        }

        if (selectedSlot != null)
            selectedSlot.SetSelected(false);

        selectedItem = slot.ItemData;
        selectedSlot = slot;
        selectedSlot.SetSelected(true);
        RefreshRightPanel();
    }

    private void OnConfirmClicked()
    {
        if (purchaseModalOpen || selectedItem == null || !PlayerEquipmentState.HasItem(selectedItem))
            return;

        if (!PlayerEquipmentState.Equip(selectedItem))
            return;

        RefreshAllSlotSelection();
        RefreshRightPanel();
    }

    private void OnRemoveClicked()
    {
        if (purchaseModalOpen)
            return;

        PlayerEquipmentState.Unequip();
        selectedItem = null;
        selectedSlot = null;
        RefreshAllSlotSelection();
        ClearRightPanel();
    }

    private void OpenPurchaseModal(DecorationItemSlot slot)
    {
        pendingPurchaseItem = slot.ItemData;
        purchaseReturnSlot = slot;
        purchaseModalOpen = true;
        SetUnderlyingInteractionEnabled(false);
        SetPurchaseModalVisible(true);

        if (purchaseItemImage != null)
        {
            Sprite displaySprite = pendingPurchaseItem.illustration != null
                ? pendingPurchaseItem.illustration
                : pendingPurchaseItem.icon;
            purchaseItemImage.sprite = displaySprite;
            purchaseItemImage.preserveAspect = true;
            purchaseItemImage.gameObject.SetActive(displaySprite != null);
        }

        if (purchaseItemNameText != null)
            purchaseItemNameText.text = pendingPurchaseItem.itemName;
        if (purchasePriceText != null)
            purchasePriceText.text = string.Format(purchasePriceFormat, pendingPurchaseItem.elegantPointCost);

        RefreshPurchaseAffordability();
        SelectButton(purchaseNoButton);
    }

    private void OnPurchaseYesClicked()
    {
        if (!purchaseModalOpen || pendingPurchaseItem == null)
            return;

        ItemData purchasedItem = pendingPurchaseItem;
        if (DecorationPurchaseService.TryPurchase(purchasedItem, out DecorationPurchaseResult result))
        {
            ClosePurchaseModal(restoreSlotFocus: false);
            // Select for inspection/equipping, but do not equip automatically.
            Refresh(purchasedItem);
            return;
        }

        if (result == DecorationPurchaseResult.AlreadyOwned)
        {
            ClosePurchaseModal(restoreSlotFocus: false);
            Refresh(purchasedItem);
            return;
        }

        if (purchaseStatusText != null)
            purchaseStatusText.text = GetPurchaseFailureMessage(result);
        RefreshPurchaseAffordability(preserveFailureMessage: true);
        SelectButton(purchaseNoButton);
    }

    private void OnPurchaseNoClicked()
    {
        ClosePurchaseModal(restoreSlotFocus: true);
    }

    private void ClosePurchaseModal(bool restoreSlotFocus)
    {
        if (!purchaseModalOpen)
        {
            SetPurchaseModalVisible(false);
            return;
        }

        Button returnButton = purchaseReturnSlot != null ? purchaseReturnSlot.Button : null;
        purchaseModalOpen = false;
        pendingPurchaseItem = null;
        purchaseReturnSlot = null;
        SetPurchaseModalVisible(false);
        SetUnderlyingInteractionEnabled(true);
        RefreshRightPanel();

        if (restoreSlotFocus)
            SelectButton(returnButton);
    }

    private void RefreshPurchaseAffordability(bool preserveFailureMessage = false)
    {
        int balance = ElegantPointWallet.Balance;
        if (purchaseBalanceText != null)
            purchaseBalanceText.text = string.Format(purchaseBalanceFormat, balance, ElegantPointWallet.MaxBalance);

        if (pendingPurchaseItem == null)
        {
            if (purchaseYesButton != null)
                purchaseYesButton.interactable = false;
            return;
        }

        bool affordable = balance >= pendingPurchaseItem.elegantPointCost;
        if (purchaseYesButton != null)
            purchaseYesButton.interactable = affordable;

        if (purchaseStatusText != null && !preserveFailureMessage)
            purchaseStatusText.text = affordable ? string.Empty : insufficientPointsMessage;
    }

    private void SetUnderlyingInteractionEnabled(bool enabled)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].SetInteractionEnabled(enabled);
        }

        if (confirmButton != null)
            confirmButton.interactable = enabled && selectedItem != null && PlayerEquipmentState.HasItem(selectedItem);
        if (removeButton != null)
            removeButton.interactable = enabled && !string.IsNullOrWhiteSpace(PlayerEquipmentState.EquippedItemId);
    }

    private void RefreshAllSlotSelection()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                continue;

            ItemData data = slots[i].ItemData;
            slots[i].SetSelected(data != null && PlayerEquipmentState.IsEquipped(data));
        }
    }

    private void RefreshRightPanel()
    {
        if (selectedItem == null || !PlayerEquipmentState.HasItem(selectedItem))
        {
            ClearRightPanel();
            return;
        }

        if (previewImage != null)
        {
            previewImage.sprite = selectedItem.illustration;
            previewImage.gameObject.SetActive(selectedItem.illustration != null);
        }

        if (itemNameBase != null)
            itemNameBase.SetActive(true);

        if (itemNameText != null)
        {
            itemNameText.gameObject.SetActive(true);
            itemNameText.text = selectedItem.itemName;
        }

        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(true);
            descriptionText.text = selectedItem.description;
        }

        if (confirmButton != null)
            confirmButton.interactable = !purchaseModalOpen;
        if (removeButton != null)
            removeButton.interactable = !purchaseModalOpen && !string.IsNullOrWhiteSpace(PlayerEquipmentState.EquippedItemId);

        RefreshAbilityTags();
    }

    private void ClearRightPanel()
    {
        if (previewImage != null)
        {
            previewImage.sprite = defaultPreviewSprite;
            previewImage.gameObject.SetActive(true);
        }

        if (itemNameBase != null)
            itemNameBase.SetActive(false);
        if (itemNameText != null)
            itemNameText.gameObject.SetActive(false);
        if (descriptionText != null)
            descriptionText.gameObject.SetActive(false);
        if (confirmButton != null)
            confirmButton.interactable = false;
        if (removeButton != null)
            removeButton.interactable = !purchaseModalOpen && !string.IsNullOrWhiteSpace(PlayerEquipmentState.EquippedItemId);

        ClearAbilityTags();
    }

    private void RefreshAbilityTags()
    {
        ClearAbilityTags();
        if (abilityTagContainer == null || abilityTagPrefab == null || selectedItem == null)
            return;

        EquipmentAbilityData[] abilities = selectedItem.equipmentAbility;
        if (abilities == null)
            return;

        for (int i = 0; i < abilities.Length; i++)
        {
            string label = GetAbilityLabel(abilities[i].abilityType);
            if (string.IsNullOrEmpty(label))
                continue;

            GameObject tag = Instantiate(abilityTagPrefab, abilityTagContainer);
            TextMeshProUGUI text = tag.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                ApplyShopFont(text);
                text.text = label;
            }
        }
    }

    private void ClearAbilityTags()
    {
        if (abilityTagContainer == null)
            return;

        for (int i = abilityTagContainer.childCount - 1; i >= 0; i--)
            Destroy(abilityTagContainer.GetChild(i).gameObject);
    }

    private void OnElegantPointBalanceChanged(int _)
    {
        RefreshElegantPointBalance();
        if (purchaseModalOpen)
            RefreshPurchaseAffordability();
    }

    private void RefreshElegantPointBalance()
    {
        if (elegantPointBalanceText != null)
        {
            elegantPointBalanceText.text = string.Format(
                elegantPointBalanceFormat,
                ElegantPointWallet.Balance,
                ElegantPointWallet.MaxBalance);
        }
    }

    private void EnsureElegantPointBalanceText()
    {
        if (elegantPointBalanceText != null)
            return;

        elegantPointBalanceText = CreateRuntimeText(
            "ElegantPointBalanceText (Runtime)",
            transform,
            28f,
            TextAlignmentOptions.TopRight,
            new Color(0.95f, 0.75f, 1f, 1f));

        RectTransform rect = elegantPointBalanceText.rectTransform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-35f, -30f);
        rect.sizeDelta = new Vector2(340f, 50f);
    }

    private void EnsurePurchaseModal()
    {
        bool hasFunctionalModal =
            purchaseModal != null &&
            purchaseItemNameText != null &&
            purchasePriceText != null &&
            purchaseBalanceText != null &&
            purchaseStatusText != null &&
            purchaseYesButton != null &&
            purchaseNoButton != null;

        if (!hasFunctionalModal)
        {
            if (purchaseModal != null)
                purchaseModal.SetActive(false);

            CreateRuntimePurchaseModal();
        }

        MovePurchaseModalToCanvasOverlay();
    }

    private void CreateRuntimePurchaseModal()
    {
        purchaseModal = CreateUiObject("PurchaseModal (Runtime)", ResolvePurchaseModalHost());
        RectTransform modalRect = (RectTransform)purchaseModal.transform;
        Stretch(modalRect);
        Image overlay = purchaseModal.AddComponent<Image>();
        overlay.color = new Color(0.03f, 0.01f, 0.06f, 0.82f);
        overlay.raycastTarget = true;

        GameObject panel = CreateUiObject("Panel", purchaseModal.transform);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(680f, 460f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.13f, 0.07f, 0.19f, 0.98f);
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.25f, 0.9f, 0.95f);
        outline.effectDistance = new Vector2(3f, -3f);

        GameObject imageObject = CreateUiObject("ItemImage", panel.transform);
        RectTransform imageRect = (RectTransform)imageObject.transform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(-205f, 45f);
        imageRect.sizeDelta = new Vector2(180f, 180f);
        purchaseItemImage = imageObject.AddComponent<Image>();
        purchaseItemImage.raycastTarget = false;

        purchaseItemNameText = CreateRuntimeText("ItemName", panel.transform, 36f, TextAlignmentOptions.Center, Color.white);
        ConfigureCenteredRect(purchaseItemNameText.rectTransform, new Vector2(105f, 145f), new Vector2(360f, 60f));

        purchasePriceText = CreateRuntimeText("Price", panel.transform, 28f, TextAlignmentOptions.Center, Color.white);
        ConfigureCenteredRect(purchasePriceText.rectTransform, new Vector2(105f, 75f), new Vector2(360f, 48f));

        purchaseBalanceText = CreateRuntimeText("Balance", panel.transform, 25f, TextAlignmentOptions.Center, new Color(0.95f, 0.75f, 1f, 1f));
        ConfigureCenteredRect(purchaseBalanceText.rectTransform, new Vector2(105f, 20f), new Vector2(360f, 44f));

        purchaseStatusText = CreateRuntimeText("Status", panel.transform, 23f, TextAlignmentOptions.Center, new Color(1f, 0.55f, 0.65f, 1f));
        ConfigureCenteredRect(purchaseStatusText.rectTransform, new Vector2(0f, -65f), new Vector2(600f, 55f));

        purchaseYesButton = CreateRuntimeButton("YesButton", panel.transform, "はい", new Vector2(-100f, -160f));
        purchaseNoButton = CreateRuntimeButton("NoButton", panel.transform, "いいえ", new Vector2(100f, -160f));
        purchaseModal.SetActive(false);
    }

    private void MovePurchaseModalToCanvasOverlay()
    {
        if (purchaseModal == null)
            return;

        Transform host = ResolvePurchaseModalHost();
        if (purchaseModal.transform.parent != host)
            purchaseModal.transform.SetParent(host, false);

        if (purchaseModal.transform is RectTransform modalRect)
            Stretch(modalRect);
    }

    private Transform ResolvePurchaseModalHost()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    private TextMeshProUGUI CreateRuntimeText(
        string objectName,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        ApplyShopFont(text);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private void ConfigurePurchaseButtonNavigation()
    {
        Navigation yesNavigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = purchaseNoButton,
            selectOnRight = purchaseNoButton,
            selectOnUp = purchaseYesButton,
            selectOnDown = purchaseYesButton
        };
        purchaseYesButton.navigation = yesNavigation;

        Navigation noNavigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = purchaseYesButton,
            selectOnRight = purchaseYesButton,
            selectOnUp = purchaseNoButton,
            selectOnDown = purchaseNoButton
        };
        purchaseNoButton.navigation = noNavigation;
    }

    private Button CreateRuntimeButton(string objectName, Transform parent, string label, Vector2 position)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(160f, 60f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.48f, 0.17f, 0.62f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.72f, 0.3f, 0.9f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.35f, 0.1f, 0.48f, 1f);
        button.colors = colors;

        TextMeshProUGUI labelText = CreateRuntimeText("Label", buttonObject.transform, 28f, TextAlignmentOptions.Center, Color.white);
        Stretch(labelText.rectTransform);
        labelText.text = label;
        return button;
    }

    private void ApplyShopFont(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_FontAsset resolvedFont = shopFont != null
            ? shopFont
            : itemNameText != null ? itemNameText.font : null;
        if (resolvedFont != null)
            text.font = resolvedFont;
    }

    private void MigrateLegacyEnglishText()
    {
        if (IsLegacyEnglishText(purchasePriceFormat, "Price") ||
            IsLegacyEnglishText(purchasePriceFormat, "EP"))
        {
            purchasePriceFormat = DefaultPurchasePriceFormat;
        }

        if (IsLegacyEnglishText(purchaseBalanceFormat, "Balance") ||
            IsLegacyEnglishText(purchaseBalanceFormat, "EP"))
        {
            purchaseBalanceFormat = DefaultPurchaseBalanceFormat;
        }

        if (IsLegacyEnglishText(insufficientPointsMessage, "Insufficient"))
            insufficientPointsMessage = DefaultInsufficientPointsMessage;
    }

    private static bool IsLegacyEnglishText(string value, string token)
    {
        return string.IsNullOrWhiteSpace(value) ||
            value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private GameObject CreateUiObject(string objectName, Transform parent)
    {
        var uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = gameObject.layer;
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void ConfigureCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetPurchaseModalVisible(bool visible)
    {
        if (purchaseModal == null)
            return;

        purchaseModal.SetActive(visible);
        if (visible)
            purchaseModal.transform.SetAsLastSibling();
    }

    private static void SelectButton(Button button)
    {
        if (button == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private static string GetPurchaseFailureMessage(DecorationPurchaseResult result)
    {
        switch (result)
        {
            case DecorationPurchaseResult.InsufficientPoints:
                return "優雅ポイントが足りません";
            case DecorationPurchaseResult.AlreadyOwned:
                return "既に所持しています";
            case DecorationPurchaseResult.NotPurchasable:
                return "このデコレーションは購入できません";
            default:
                return "購入失敗";
        }
    }

    private static string GetAbilityLabel(EquipmentAbilityType type)
    {
        switch (type)
        {
            case EquipmentAbilityType.GunRecoilForceBonus:
                return "反動移動の距離 UP";
            case EquipmentAbilityType.AttackPowerMultiplier:
                return "攻撃力 UP";
            case EquipmentAbilityType.HealPercentOnEnemyKill:
                return "撃破時 HP 回復";
            default:
                return string.Empty;
        }
    }

    private readonly struct CatalogEntry
    {
        public readonly ItemData Data;
        public readonly int SourceIndex;
        public readonly int Priority;

        public CatalogEntry(ItemData data, int sourceIndex, int priority)
        {
            Data = data;
            SourceIndex = sourceIndex;
            Priority = priority;
        }
    }
}
