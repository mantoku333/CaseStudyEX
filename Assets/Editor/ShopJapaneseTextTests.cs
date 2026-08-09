using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class ShopJapaneseTextTests
{
    private const string OptionsCanvasPath = "Assets/Prefabs/UI/OptionsCanvas.prefab";
    private const string SlotPrefabPath = "Assets/Prefabs/UI/DecorationItemSlot.prefab";
    private const string RequiredJapaneseCharacters = "価格所持ポイント優雅が足りませんはいいえ。";

    [Test]
    public void DecorationPagePrefab_UsesJapaneseTextAndJapaneseFont()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionsCanvasPath);
        Assert.That(prefab, Is.Not.Null, OptionsCanvasPath);

        DecorationPage page = prefab.GetComponentInChildren<DecorationPage>(true);
        Assert.That(page, Is.Not.Null);

        var serializedPage = new SerializedObject(page);
        Assert.That(
            serializedPage.FindProperty("purchasePriceFormat").stringValue,
            Is.EqualTo("{0} Pt"));
        Assert.That(
            serializedPage.FindProperty("purchaseBalanceFormat").stringValue,
            Is.EqualTo("所持ポイント: {0} / {1}"));
        Assert.That(
            serializedPage.FindProperty("insufficientPointsMessage").stringValue,
            Is.EqualTo("優雅ポイントが足りません。"));

        AssertSupportsJapanese(serializedPage.FindProperty("shopFont").objectReferenceValue as TMP_FontAsset);
    }

    [Test]
    public void DecorationItemSlotPrefab_UsesJapaneseFontAndLocalizedPointSuffix()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        Assert.That(prefab, Is.Not.Null, SlotPrefabPath);

        DecorationItemSlot slot = prefab.GetComponent<DecorationItemSlot>();
        Assert.That(slot, Is.Not.Null);

        var serializedSlot = new SerializedObject(slot);
        Assert.That(serializedSlot.FindProperty("priceFormat").stringValue, Is.EqualTo("{0} Pt"));
        AssertSupportsJapanese(serializedSlot.FindProperty("shopFont").objectReferenceValue as TMP_FontAsset);
    }

    private static void AssertSupportsJapanese(TMP_FontAsset font)
    {
        Assert.That(font, Is.Not.Null);
        foreach (char character in RequiredJapaneseCharacters)
        {
            Assert.That(
                font.HasCharacter(character),
                Is.True,
                $"Font '{font.name}' is missing Japanese character '{character}' (U+{(int)character:X4}).");
        }
    }
}
