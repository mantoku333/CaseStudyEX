using System;
using System.Collections.Generic;
using Metroidvania.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DecorationPage : MonoBehaviour
{
    private const string DefaultPurchasePriceFormat = "{0} Pt";
    private const string DefaultPurchaseBalanceFormat = "所持ポイント: {0} / {1}";
    private const string DefaultInsufficientPointsMessage = "優雅ポイントが足りません。";
    private const string PurchaseConfirmResourceRoot = "Figma/Design2/PurchaseConfirm/";
    private static readonly Color PurchaseBalanceNormalColor = Color.white;
    private static readonly Color PurchaseBalanceInsufficientColor = new Color(1f, 0.37f, 0.37f, 1f);

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
    private TextMeshProUGUI lockedTooltipText;
    private RectTransform lockedTooltipRect;
    private bool lockedTooltipVisible;

    public bool IsPurchaseModalOpen => purchaseModalOpen;
    public bool IsShopEnabled => DecorationShopFeature.Enabled;

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

        if (IsShopEnabled)
        {
            EnsureElegantPointBalanceText();
            EnsurePurchaseModal();
            ConfigurePurchaseButtonNavigation();
            purchaseYesButton.onClick.AddListener(OnPurchaseYesClicked);
            purchaseNoButton.onClick.AddListener(OnPurchaseNoClicked);
            UIButtonSfxPlayer.Register(purchaseYesButton);
            UIButtonSfxPlayer.Register(purchaseNoButton);
            SetPurchaseModalVisible(false);
            RefreshElegantPointBalance();
        }
        else
        {
            HideShopUi();
        }

        ClearRightPanel();
    }

    private void OnEnable()
    {
        ElegantPointWallet.BalanceChanged -= OnElegantPointBalanceChanged;
        if (IsShopEnabled)
        {
            ElegantPointWallet.BalanceChanged += OnElegantPointBalanceChanged;
            RefreshElegantPointBalance();
        }
        else
        {
            HideShopUi();
        }
    }

    private void OnDisable()
    {
        ElegantPointWallet.BalanceChanged -= OnElegantPointBalanceChanged;
        ClosePurchaseModal(restoreSlotFocus: false);
    }

    private void LateUpdate()
    {
        if (lockedTooltipVisible)
            SetLockedTooltipPosition(GetPointerScreenPosition());
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
        if (!IsShopEnabled || !purchaseModalOpen)
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
            slot.Initialize(data, isOwned, isEquipped, IsShopEnabled, OnSlotSelected, OnLockedSlotHoverChanged);
            slots.Add(slot);

            if (data != null && (data == preferredSelection || (preferredSelection == null && isEquipped)))
            {
                selectedItem = data;
                selectedSlot = slot;
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
            if (IsShopEnabled && slot.ItemData.elegantPointCost > 0)
                OpenPurchaseModal(slot);
            return;
        }

        selectedItem = slot.ItemData;
        selectedSlot = slot;
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
        if (!IsShopEnabled || slot == null || slot.ItemData == null)
            return;

        pendingPurchaseItem = slot.ItemData;
        purchaseReturnSlot = slot;
        purchaseModalOpen = true;
        SetLockedTooltipVisible(false);
        SetUnderlyingInteractionEnabled(false);
        SetPurchaseModalVisible(true);

        if (purchaseItemImage != null)
            purchaseItemImage.gameObject.SetActive(false);

        if (purchaseItemNameText != null)
        {
            purchaseItemNameText.gameObject.SetActive(false);
            purchaseItemNameText.text = pendingPurchaseItem.itemName;
        }
        if (purchasePriceText != null)
        {
            purchasePriceText.gameObject.SetActive(true);
            purchasePriceText.text = pendingPurchaseItem.elegantPointCost.ToString();
        }
        if (purchaseBalanceText != null)
            purchaseBalanceText.gameObject.SetActive(true);

        RefreshPurchaseAffordability();
        SelectButton(purchaseNoButton);
    }

    private void OnPurchaseYesClicked()
    {
        if (!IsShopEnabled || !purchaseModalOpen || pendingPurchaseItem == null)
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
        {
            purchaseStatusText.gameObject.SetActive(true);
            purchaseStatusText.text = GetPurchaseFailureMessage(result);
        }
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
            purchaseBalanceText.text = balance.ToString();

        if (pendingPurchaseItem == null)
        {
            if (purchaseYesButton != null)
                purchaseYesButton.interactable = false;
            return;
        }

        bool affordable = balance >= pendingPurchaseItem.elegantPointCost;
        if (purchaseYesButton != null)
            purchaseYesButton.interactable = affordable;
        if (purchaseBalanceText != null)
            purchaseBalanceText.color = affordable
                ? PurchaseBalanceNormalColor
                : PurchaseBalanceInsufficientColor;

        if (purchaseStatusText != null && !preserveFailureMessage)
        {
            purchaseStatusText.gameObject.SetActive(false);
            purchaseStatusText.text = affordable ? string.Empty : insufficientPointsMessage;
        }
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
        if (!IsShopEnabled)
            return;

        RefreshElegantPointBalance();
        if (purchaseModalOpen)
            RefreshPurchaseAffordability();
    }

    private void OnLockedSlotHoverChanged(DecorationItemSlot slot, bool visible, Vector2 screenPosition)
    {
        if (purchaseModalOpen || slot == null || slot.IsOwned)
            visible = false;

        if (!visible)
        {
            SetLockedTooltipVisible(false);
            return;
        }

        EnsureLockedTooltip();
        lockedTooltipText.text = "ロックされている";
        SetLockedTooltipPosition(screenPosition.sqrMagnitude > 0f ? screenPosition : GetPointerScreenPosition());
        SetLockedTooltipVisible(true);
    }

    private void EnsureLockedTooltip()
    {
        if (lockedTooltipText != null)
            return;

        GameObject tooltipObject = CreateUiObject("LockedTooltip (Runtime)", ResolvePurchaseModalHost());
        lockedTooltipRect = (RectTransform)tooltipObject.transform;
        lockedTooltipRect.anchorMin = Vector2.zero;
        lockedTooltipRect.anchorMax = Vector2.zero;
        lockedTooltipRect.pivot = new Vector2(0f, 1f);
        lockedTooltipRect.sizeDelta = new Vector2(220f, 44f);

        lockedTooltipText = tooltipObject.AddComponent<TextMeshProUGUI>();
        ApplyShopFont(lockedTooltipText);
        lockedTooltipText.fontSize = 24f;
        lockedTooltipText.alignment = TextAlignmentOptions.Left;
        lockedTooltipText.color = Color.white;
        lockedTooltipText.raycastTarget = false;
        tooltipObject.SetActive(false);
    }

    private void SetLockedTooltipPosition(Vector2 screenPosition)
    {
        if (lockedTooltipRect == null)
            return;

        Vector2 targetScreenPosition = screenPosition + new Vector2(28f, -8f);
        RectTransform parentRect = lockedTooltipRect.parent as RectTransform;
        Canvas canvas = lockedTooltipRect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            lockedTooltipRect.position = targetScreenPosition;
        }
        else if (parentRect != null &&
            RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, targetScreenPosition, eventCamera, out Vector3 worldPosition))
        {
            lockedTooltipRect.position = worldPosition;
        }
        else
        {
            lockedTooltipRect.position = targetScreenPosition;
        }

        lockedTooltipRect.SetAsLastSibling();
    }

    private void SetLockedTooltipVisible(bool visible)
    {
        lockedTooltipVisible = visible;
        if (lockedTooltipText != null)
            lockedTooltipText.gameObject.SetActive(visible);
    }

    private static Vector2 GetPointerScreenPosition()
    {
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();

        return Vector2.zero;
    }

    private void RefreshElegantPointBalance()
    {
        if (!IsShopEnabled)
        {
            if (elegantPointBalanceText != null)
                elegantPointBalanceText.gameObject.SetActive(false);
            return;
        }

        if (elegantPointBalanceText != null)
        {
            elegantPointBalanceText.gameObject.SetActive(true);
            elegantPointBalanceText.text = string.Format(
                elegantPointBalanceFormat,
                ElegantPointWallet.Balance,
                ElegantPointWallet.MaxBalance);
        }
    }

    private void EnsureElegantPointBalanceText()
    {
        if (!IsShopEnabled)
            return;

        if (elegantPointBalanceText != null)
        {
            ConfigureElegantPointBalanceText();
            return;
        }

        elegantPointBalanceText = CreateRuntimeText(
            "ElegantPointBalanceText (Runtime)",
            transform,
            28f,
            TextAlignmentOptions.TopRight,
            new Color(0.95f, 0.75f, 1f, 1f));
        ConfigureElegantPointBalanceText();
    }

    private void ConfigureElegantPointBalanceText()
    {
        if (elegantPointBalanceText == null)
            return;

        ApplyShopFont(elegantPointBalanceText);
        elegantPointBalanceText.alignment = TextAlignmentOptions.TopRight;
        elegantPointBalanceText.fontSize = 28f;
        elegantPointBalanceText.color = new Color(0.95f, 0.75f, 1f, 1f);

        RectTransform rect = elegantPointBalanceText.rectTransform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = new Vector2(-35f, -30f);
        rect.sizeDelta = new Vector2(340f, 50f);
    }

    private void EnsurePurchaseModal()
    {
        if (!IsShopEnabled)
            return;

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
        overlay.color = new Color(0f, 0f, 0f, 0.84f);
        overlay.raycastTarget = true;

        GameObject panel = CreateUiObject("FigmaPanel", purchaseModal.transform);
        RectTransform panelRect = (RectTransform)panel.transform;
        Stretch(panelRect);
        DisableRaycastImage(panel);

        CreateFigmaImage(panel.transform, "Back", "back", 0f, 398f, 2659f, 251f);
        CreateFigmaImage(panel.transform, "Title", "title_purchase", 786f, 463f, 347f, 49f);
        CreateFigmaImage(panel.transform, "BalanceFrame", "balance_frame", 810f, 520f, 295f, 45f);
        CreateFigmaImage(panel.transform, "Star", "star", 914f, 526f, 32f, 31f);
        CreateFigmaImage(panel.transform, "Multiply", "multiply", 954f, 533f, 16f, 16f);

        TextMeshProUGUI balanceLabel = CreateRuntimeText("BalanceLabel", panel.transform, 20f, TextAlignmentOptions.Right, Color.white);
        balanceLabel.text = "所持数:";
        balanceLabel.fontStyle = FontStyles.Bold;
        SetFigmaRect(balanceLabel.rectTransform, 814f, 527f, 88f, 28f);

        GameObject imageObject = CreateUiObject("ItemImage", panel.transform);
        RectTransform imageRect = (RectTransform)imageObject.transform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = new Vector2(-205f, 45f);
        imageRect.sizeDelta = new Vector2(180f, 180f);
        purchaseItemImage = imageObject.AddComponent<Image>();
        purchaseItemImage.raycastTarget = false;
        purchaseItemImage.gameObject.SetActive(false);

        purchaseItemNameText = CreateRuntimeText("ItemName", panel.transform, 22f, TextAlignmentOptions.Center, Color.white);
        purchaseItemNameText.fontStyle = FontStyles.Bold;
        SetFigmaRect(purchaseItemNameText.rectTransform, 710f, 430f, 500f, 34f);
        purchaseItemNameText.gameObject.SetActive(false);

        purchasePriceText = CreateRuntimeText("Price", panel.transform, 22f, TextAlignmentOptions.Center, Color.white);
        purchasePriceText.fontStyle = FontStyles.Bold;
        purchasePriceText.enableAutoSizing = true;
        purchasePriceText.fontSizeMin = 18f;
        purchasePriceText.fontSizeMax = 22f;
        SetFigmaRect(purchasePriceText.rectTransform, 978f, 526f, 50f, 31f);

        purchaseBalanceText = CreateRuntimeText("Balance", panel.transform, 22f, TextAlignmentOptions.Center, PurchaseBalanceNormalColor);
        purchaseBalanceText.fontStyle = FontStyles.Bold;
        purchaseBalanceText.enableAutoSizing = true;
        purchaseBalanceText.fontSizeMin = 18f;
        purchaseBalanceText.fontSizeMax = 22f;
        SetFigmaRect(purchaseBalanceText.rectTransform, 1048f, 526f, 50f, 31f);

        purchaseStatusText = CreateRuntimeText("Status", panel.transform, 20f, TextAlignmentOptions.Center, new Color(0.72f, 0.1f, 0.18f, 1f));
        SetFigmaRect(purchaseStatusText.rectTransform, 710f, 568f, 500f, 30f);
        purchaseStatusText.gameObject.SetActive(false);

        purchaseYesButton = CreateFigmaSpriteButton("YesButton", panel.transform, "yes_normal", "yes_selected", 594f, 594f, 346f, 80f);
        purchaseNoButton = CreateFigmaSpriteButton("NoButton", panel.transform, "no_normal", "no_selected", 981f, 594f, 346f, 80f);
        purchaseModal.SetActive(false);
    }

    private void DisableRaycastImage(GameObject target)
    {
        Image image = target.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;
    }

    private Image CreateFigmaImage(Transform parent, string objectName, string resourceName, float x, float y, float width, float height)
    {
        GameObject imageObject = CreateUiObject(objectName, parent);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = LoadPurchaseConfirmSprite(resourceName);
        image.preserveAspect = false;
        image.raycastTarget = false;
        SetFigmaRect((RectTransform)imageObject.transform, x, y, width, height);
        return image;
    }

    private Button CreateFigmaSpriteButton(
        string objectName,
        Transform parent,
        string normalResourceName,
        string selectedResourceName,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        RectTransform rect = (RectTransform)buttonObject.transform;
        SetFigmaRect(rect, x, y, width, height);

        Image image = buttonObject.AddComponent<Image>();
        Sprite normal = LoadPurchaseConfirmSprite(normalResourceName);
        Sprite selected = LoadPurchaseConfirmSprite(selectedResourceName);
        image.sprite = normal;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
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

    private static Sprite LoadPurchaseConfirmSprite(string resourceName)
    {
        Texture2D texture = Resources.Load<Texture2D>(PurchaseConfirmResourceRoot + resourceName);
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
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
        rect.sizeDelta = new Vector2(180f, 46f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 0.55f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 0.7f);
        button.colors = colors;

        TextMeshProUGUI labelText = CreateRuntimeText("Label", buttonObject.transform, 28f, TextAlignmentOptions.Center, new Color(0.02f, 0.02f, 0.02f, 1f));
        labelText.fontStyle = FontStyles.Bold;
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

    private static void SetFigmaRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x + width * 0.5f - 960f, 540f - y - height * 0.5f);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void SetPurchaseModalVisible(bool visible)
    {
        if (purchaseModal == null)
            return;

        purchaseModal.SetActive(visible);
        if (visible)
            purchaseModal.transform.SetAsLastSibling();
    }

    private void HideShopUi()
    {
        purchaseModalOpen = false;
        pendingPurchaseItem = null;
        purchaseReturnSlot = null;

        if (elegantPointBalanceText != null)
            elegantPointBalanceText.gameObject.SetActive(false);
        if (purchaseModal != null)
            purchaseModal.SetActive(false);
        SetLockedTooltipVisible(false);
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
            case EquipmentAbilityType.RecoilCooldownMultiplier:
                return "反動移動 CT 短縮";
            case EquipmentAbilityType.RecoilCooldownDisabled:
                return "反動移動 CT なし";
            case EquipmentAbilityType.JumpHeightBonus:
                return "ジャンプ高さ UP";
            case EquipmentAbilityType.MaxHealthMultiplier:
                return "HP 上限 UP";
            case EquipmentAbilityType.ElegantPointGainMultiplier:
                return "優雅ポイント獲得量 UP";
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
