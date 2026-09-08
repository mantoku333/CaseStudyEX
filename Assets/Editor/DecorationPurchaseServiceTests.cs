using System.Collections.Generic;
using System.Reflection;
using Metroidvania.Data;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class DecorationPurchaseServiceTests
{
    private readonly List<ItemData> createdItems = new List<ItemData>();
    private readonly List<ItemSnapshot> originalItems = new List<ItemSnapshot>();
    private List<GameProgressFlags.GameProgressFlagSnapshot> originalFlags;
    private int originalBalance;
    private string originalEquippedId;
    private ItemData originalEquippedData;

    [SetUp]
    public void SetUp()
    {
        originalBalance = ElegantPointWallet.Balance;
        originalFlags = GameProgressFlags.GetSnapshot();
        originalEquippedId = PlayerEquipmentState.EquippedItemId;
        originalEquippedData = PlayerEquipmentState.EquippedItemData;

        IReadOnlyList<string> insertionOrder = GameItems.InsertionOrder;
        for (int i = 0; i < insertionOrder.Count; i++)
        {
            string itemId = insertionOrder[i];
            originalItems.Add(new ItemSnapshot(itemId, GameItems.GetCount(itemId)));
        }

        ElegantPointWallet.Clear();
        GameItems.ClearAll();
        GameProgressFlags.ClearAll();
        PlayerEquipmentState.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
        ElegantPointWallet.Clear();
        GameItems.ClearAll();
        GameProgressFlags.ClearAll();
        PlayerEquipmentState.ClearAll();

        ElegantPointWallet.Add(originalBalance);
        for (int i = 0; i < originalItems.Count; i++)
            GameItems.SetCount(originalItems[i].ItemId, originalItems[i].Count);
        for (int i = 0; i < originalFlags.Count; i++)
            GameProgressFlags.Set(originalFlags[i].Key, originalFlags[i].Value);

        if (originalEquippedData != null)
            PlayerEquipmentState.Equip(originalEquippedData);
        else if (!string.IsNullOrEmpty(originalEquippedId))
            PlayerEquipmentState.EquipById(originalEquippedId);

        for (int i = 0; i < createdItems.Count; i++)
            Object.DestroyImmediate(createdItems[i]);

        createdItems.Clear();
        originalItems.Clear();
    }

    [Test]
    public void TryPurchase_SpendsPointsAndGrantsInventoryAndProgressFlag()
    {
        ItemData item = CreateDecoration("test_decoration", 50);
        ElegantPointWallet.Add(120);

        bool purchased = DecorationPurchaseService.TryPurchase(item, out DecorationPurchaseResult result);

        Assert.That(purchased, Is.True);
        Assert.That(result, Is.EqualTo(DecorationPurchaseResult.Success));
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(70));
        Assert.That(GameItems.GetCount(item.itemId), Is.EqualTo(1));
        Assert.That(GameProgressFlags.Get(item.itemId), Is.True);
        Assert.That(PlayerEquipmentState.EquippedItemId, Is.Empty, "Purchasing must not auto-equip.");
    }

    [Test]
    public void TryPurchase_InsufficientPointsIsAtomic()
    {
        ItemData item = CreateDecoration("expensive_decoration", 70);
        ElegantPointWallet.Add(69);

        bool purchased = DecorationPurchaseService.TryPurchase(item, out DecorationPurchaseResult result);

        Assert.That(purchased, Is.False);
        Assert.That(result, Is.EqualTo(DecorationPurchaseResult.InsufficientPoints));
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(69));
        Assert.That(GameItems.GetCount(item.itemId), Is.Zero);
        Assert.That(GameProgressFlags.Get(item.itemId), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TryPurchase_AlreadyOwnedDoesNotSpend(bool representedByProgressFlag)
    {
        ItemData item = CreateDecoration("owned_decoration", 50);
        ElegantPointWallet.Add(100);
        if (representedByProgressFlag)
            GameProgressFlags.Set(item.itemId, true);
        else
            GameItems.SetCount(item.itemId, 1);

        bool purchased = DecorationPurchaseService.TryPurchase(item, out DecorationPurchaseResult result);

        Assert.That(purchased, Is.False);
        Assert.That(result, Is.EqualTo(DecorationPurchaseResult.AlreadyOwned));
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(100));
    }

    [Test]
    public void TryPurchase_RejectsNonEquipmentAndZeroCostItems()
    {
        ItemData nonEquipment = CreateDecoration("not_equipment", 10);
        nonEquipment.itemType = ItemType.KeyItem;
        ItemData noPrice = CreateDecoration("no_price", 0);
        ElegantPointWallet.Add(100);

        Assert.That(
            DecorationPurchaseService.TryPurchase(nonEquipment, out DecorationPurchaseResult nonEquipmentResult),
            Is.False);
        Assert.That(nonEquipmentResult, Is.EqualTo(DecorationPurchaseResult.NotEquipment));
        Assert.That(
            DecorationPurchaseService.TryPurchase(noPrice, out DecorationPurchaseResult noPriceResult),
            Is.False);
        Assert.That(noPriceResult, Is.EqualTo(DecorationPurchaseResult.NotPurchasable));
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(100));
    }

    [TestCase("Assets/Data/Items/Equipment/BlueAura_Equipment.asset", 50)]
    [TestCase("Assets/Data/Items/Equipment/RedAura_Equipment.asset", 70)]
    [TestCase("Assets/Data/Items/Equipment/Arcanciel_Equipment.asset", 250)]
    public void DecorationAssets_HaveExpectedElegantPointCost(string assetPath, int expectedCost)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);

        Assert.That(item, Is.Not.Null);
        Assert.That(item.elegantPointCost, Is.EqualTo(expectedCost));
    }

    [Test]
    public void BlueAndRedAssets_PreserveTheirExistingSerializedIds()
    {
        ItemData blue = AssetDatabase.LoadAssetAtPath<ItemData>(
            "Assets/Data/Items/Equipment/BlueAura_Equipment.asset");
        ItemData red = AssetDatabase.LoadAssetAtPath<ItemData>(
            "Assets/Data/Items/Equipment/RedAura_Equipment.asset");

        Assert.That(blue.itemId, Is.EqualTo(GameProgressKeys.EquipmentRedAuraUnlocked));
        Assert.That(red.itemId, Is.EqualTo(GameProgressKeys.EquipmentBlueAuraUnlocked));
    }

    [Test]
    public void OptionsCanvas_DecorationCatalogUsesBlueRedArcancielOrder()
    {
        GameObject optionsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/OptionsCanvas.prefab");
        Assert.That(optionsPrefab, Is.Not.Null);
        DecorationPage page = optionsPrefab.GetComponentInChildren<DecorationPage>(true);
        Assert.That(page, Is.Not.Null);
        var serializedPage = new SerializedObject(page);
        SerializedProperty catalog = serializedPage.FindProperty("itemCatalog");

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.arraySize, Is.GreaterThanOrEqualTo(3));
        Assert.That(GetCatalogItem(catalog, 0).name, Is.EqualTo("BlueAura_Equipment"));
        Assert.That(GetCatalogItem(catalog, 1).name, Is.EqualTo("RedAura_Equipment"));
        Assert.That(GetCatalogItem(catalog, 2).name, Is.EqualTo("Arcanciel_Equipment"));
    }

    [Test]
    public void PlayerPrefab_EquipmentCatalogContainsEveryPurchasableDecoration()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/Player.prefab");
        Assert.That(playerPrefab, Is.Not.Null);
        PlayerEquipmentController controller = playerPrefab.GetComponent<PlayerEquipmentController>();
        Assert.That(controller, Is.Not.Null);
        var serializedController = new SerializedObject(controller);
        SerializedProperty catalog = serializedController.FindProperty("equipmentCatalog");
        ItemData blue = AssetDatabase.LoadAssetAtPath<ItemData>(
            "Assets/Data/Items/Equipment/BlueAura_Equipment.asset");
        ItemData red = AssetDatabase.LoadAssetAtPath<ItemData>(
            "Assets/Data/Items/Equipment/RedAura_Equipment.asset");
        ItemData arcanciel = AssetDatabase.LoadAssetAtPath<ItemData>(
            "Assets/Data/Items/Equipment/Arcanciel_Equipment.asset");

        Assert.That(catalog, Is.Not.Null);
        Assert.That(CatalogContains(catalog, blue), Is.True, "Blue Aura is missing from Player equipmentCatalog.");
        Assert.That(CatalogContains(catalog, red), Is.True, "Red Aura is missing from Player equipmentCatalog.");
        Assert.That(CatalogContains(catalog, arcanciel), Is.True, "Arcanciel is missing from Player equipmentCatalog.");
    }

    [Test]
    public void DecorationSlot_ShopDisabledHidesPriceAndLocksUnownedItem()
    {
        GameObject slotObject = InstantiateSlotPrefab();
        try
        {
            DecorationItemSlot slot = slotObject.GetComponent<DecorationItemSlot>();
            ItemData item = CreateDecoration("locked_decoration", 50);

            slot.Initialize(item, owned: false, isEquipped: false, enableShop: false, onSelected: null);

            Assert.That(slot.Button.interactable, Is.False);
            var serializedSlot = new SerializedObject(slot);
            Assert.That(serializedSlot.FindProperty("priceText").objectReferenceValue, Is.Null);
            Assert.That(slotObject.transform.Find("PriceText (Runtime)"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(slotObject);
        }
    }

    [Test]
    public void DecorationSlot_ShopDisabledKeepsOwnedItemInteractive()
    {
        GameObject slotObject = InstantiateSlotPrefab();
        try
        {
            DecorationItemSlot slot = slotObject.GetComponent<DecorationItemSlot>();
            ItemData item = CreateDecoration("owned_drop", 50);
            DecorationItemSlot selectedSlot = null;

            slot.Initialize(
                item,
                owned: true,
                isEquipped: false,
                enableShop: false,
                onSelected: selected => selectedSlot = selected);

            Assert.That(slot.Button.interactable, Is.True);
            slot.Button.onClick.Invoke();
            Assert.That(selectedSlot, Is.SameAs(slot));
        }
        finally
        {
            Object.DestroyImmediate(slotObject);
        }
    }

    [Test]
    public void DecorationSlot_ShopEnabledLocksUnownedItemWithoutInlinePrice()
    {
        GameObject slotObject = InstantiateSlotPrefab();
        try
        {
            DecorationItemSlot slot = slotObject.GetComponent<DecorationItemSlot>();
            ItemData item = CreateDecoration("future_shop_item", 70);

            slot.Initialize(item, owned: false, isEquipped: false, enableShop: true, onSelected: null);

            Assert.That(slot.Button.interactable, Is.True);
            var serializedSlot = new SerializedObject(slot);
            var priceText = serializedSlot.FindProperty("priceText").objectReferenceValue as TextMeshProUGUI;
            Assert.That(priceText, Is.Not.Null);
            Assert.That(priceText.gameObject.activeSelf, Is.False);
            Assert.That(priceText.text, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(slotObject);
        }
    }

    [Test]
    public void DecorationPage_ShopEnabledShowsBalanceAndCreatesPurchaseFallbacks()
    {
        var root = new GameObject("DecorationPage Test", typeof(RectTransform));
        root.SetActive(false);
        try
        {
            DecorationPage page = root.AddComponent<DecorationPage>();
            var balanceObject = new GameObject("Serialized Balance", typeof(RectTransform));
            balanceObject.transform.SetParent(root.transform, false);
            TextMeshProUGUI balanceText = balanceObject.AddComponent<TextMeshProUGUI>();
            var modalObject = new GameObject("Serialized Modal", typeof(RectTransform));
            modalObject.transform.SetParent(root.transform, false);

            SetPrivateField(page, "elegantPointBalanceText", balanceText);
            SetPrivateField(page, "purchaseModal", modalObject);

            InvokePrivateMethod(page, "Awake");

            Assert.That(page.IsShopEnabled, Is.True);
            Assert.That(page.IsPurchaseModalOpen, Is.False);
            Assert.That(balanceObject.activeSelf, Is.True);
            Assert.That(modalObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("ElegantPointBalanceText (Runtime)"), Is.Null);
            Assert.That(root.transform.Find("PurchaseModal (Runtime)"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private ItemData CreateDecoration(string itemId, int cost)
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = itemId;
        item.itemName = itemId;
        item.itemType = ItemType.Equipment;
        item.elegantPointCost = cost;
        createdItems.Add(item);
        return item;
    }

    private static GameObject InstantiateSlotPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/DecorationItemSlot.prefab");
        Assert.That(prefab, Is.Not.Null);
        return Object.Instantiate(prefab);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private static ItemData GetCatalogItem(SerializedProperty catalog, int index)
    {
        return catalog.GetArrayElementAtIndex(index).objectReferenceValue as ItemData;
    }

    private static bool CatalogContains(SerializedProperty catalog, ItemData expected)
    {
        for (int i = 0; i < catalog.arraySize; i++)
        {
            if (catalog.GetArrayElementAtIndex(i).objectReferenceValue == expected)
                return true;
        }

        return false;
    }

    private readonly struct ItemSnapshot
    {
        public readonly string ItemId;
        public readonly int Count;

        public ItemSnapshot(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
