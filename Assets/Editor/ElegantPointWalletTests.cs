using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ElegantPointWalletTests
{
    private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        ElegantPointWallet.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        ElegantPointWallet.Clear();
    }

    [Test]
    public void Add_ClampsAtMaximumAndReturnsAppliedAmount()
    {
        Assert.That(ElegantPointWallet.Add(490), Is.EqualTo(490));
        Assert.That(ElegantPointWallet.Add(25), Is.EqualTo(10));
        Assert.That(ElegantPointWallet.Add(int.MaxValue), Is.Zero);
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(ElegantPointWallet.MaxBalance));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Add_NonPositiveAmountIsIgnored(int amount)
    {
        ElegantPointWallet.Add(25);

        Assert.That(ElegantPointWallet.Add(amount), Is.Zero);
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(25));
    }

    [Test]
    public void TrySpend_IsAtomicWhenBalanceIsInsufficient()
    {
        ElegantPointWallet.Add(100);

        Assert.That(ElegantPointWallet.TrySpend(70), Is.True);
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(30));
        Assert.That(ElegantPointWallet.TrySpend(31), Is.False);
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(30));
        Assert.That(ElegantPointWallet.TrySpend(-1), Is.False);
        Assert.That(ElegantPointWallet.TrySpend(0), Is.True);
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(30));
    }

    [Test]
    public void BalanceChanged_FiresOnlyWhenBalanceActuallyChanges()
    {
        int invocationCount = 0;
        int lastBalance = -1;
        Action<int> listener = newBalance =>
        {
            invocationCount++;
            lastBalance = newBalance;
        };

        ElegantPointWallet.BalanceChanged += listener;
        try
        {
            ElegantPointWallet.Add(10);
            ElegantPointWallet.Add(0);
            ElegantPointWallet.TrySpend(3);
            ElegantPointWallet.TrySpend(8);
            ElegantPointWallet.Clear();
            ElegantPointWallet.Clear();
        }
        finally
        {
            ElegantPointWallet.BalanceChanged -= listener;
        }

        Assert.That(invocationCount, Is.EqualTo(3));
        Assert.That(lastBalance, Is.Zero);
    }

    [Test]
    public void SaveModule_CapturesVersionedBalanceSection()
    {
        ElegantPointWallet.Add(275);
        var saveData = new SaveGameData();

        GetSaveModule().Capture(saveData);

        string json = saveData.GetCustomSectionJson(ElegantPointWallet.SaveSectionKey);
        WalletPayload payload = JsonUtility.FromJson<WalletPayload>(json);
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload.version, Is.EqualTo(1));
        Assert.That(payload.balance, Is.EqualTo(275));
    }

    [Test]
    public void RestoreFromSaveData_RestoresAndClampsBalance()
    {
        var saveData = CreateSaveData(balance: 900);

        ElegantPointWallet.RestoreFromSaveData(saveData);

        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(ElegantPointWallet.MaxBalance));
    }

    [Test]
    public void RestoreFromOldSaveWithoutSection_DefaultsToZero()
    {
        ElegantPointWallet.Add(125);
        var oldSaveData = new SaveGameData { version = 2 };

        ElegantPointWallet.RestoreFromSaveData(oldSaveData);

        Assert.That(ElegantPointWallet.Balance, Is.Zero);
    }

    [Test]
    public void SaveManagerPreSceneRestore_AppliesWalletBeforeSceneModules()
    {
        var saveData = CreateSaveData(balance: 123);
        MethodInfo restoreMethod = typeof(SaveManager).GetMethod("RestorePreSceneState", StaticNonPublic);

        Assert.That(restoreMethod, Is.Not.Null);
        restoreMethod.Invoke(null, new object[] { saveData });

        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(123));
    }

    [Test]
    public void Clear_ResetsTheWallet()
    {
        ElegantPointWallet.Add(100);

        ElegantPointWallet.Clear();

        Assert.That(ElegantPointWallet.Balance, Is.Zero);
    }

    private static ISaveDataModule GetSaveModule()
    {
        FieldInfo moduleField = typeof(ElegantPointWallet).GetField("module", StaticNonPublic);
        Assert.That(moduleField, Is.Not.Null);
        return (ISaveDataModule)moduleField.GetValue(null);
    }

    private static SaveGameData CreateSaveData(int balance)
    {
        var saveData = new SaveGameData();
        saveData.SetCustomSectionJson(
            ElegantPointWallet.SaveSectionKey,
            JsonUtility.ToJson(new WalletPayload { version = 1, balance = balance }));
        return saveData;
    }

    [Serializable]
    private sealed class WalletPayload
    {
        public int version;
        public int balance;
    }
}
