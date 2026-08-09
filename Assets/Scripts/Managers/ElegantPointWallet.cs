using System;
using UnityEngine;

public static class ElegantPointWallet
{
    public const int MaxBalance = 500;
    public const string SaveSectionKey = "elegant_points_v1";

    private const int SavePayloadVersion = 1;
    private static readonly ElegantPointWalletSaveModule module = new ElegantPointWalletSaveModule();
    private static int balance;

    public static int Balance => balance;

    public static event Action<int> BalanceChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        balance = 0;
        BalanceChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SaveManager.RegisterModule(module);
    }

    public static int Add(int amount)
    {
        if (amount <= 0 || balance >= MaxBalance)
        {
            return 0;
        }

        int amountAdded = Mathf.Min(amount, MaxBalance - balance);
        SetBalance(balance + amountAdded);
        return amountAdded;
    }

    public static bool TrySpend(int amount)
    {
        if (amount < 0 || amount > balance)
        {
            return false;
        }

        if (amount > 0)
        {
            SetBalance(balance - amount);
        }

        return true;
    }

    public static void Clear()
    {
        SetBalance(0);
    }

    public static void RestoreFromSaveData(SaveGameData saveData)
    {
        if (saveData == null)
        {
            SetBalance(0);
            return;
        }

        string json = saveData.GetCustomSectionJson(SaveSectionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            SetBalance(0);
            return;
        }

        try
        {
            var payload = JsonUtility.FromJson<ElegantPointWalletPayload>(json);
            SetBalance(payload != null ? payload.balance : 0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[ElegantPointWallet] Failed to parse saved balance. {exception}");
            SetBalance(0);
        }
    }

    private static void SetBalance(int value)
    {
        int normalized = Mathf.Clamp(value, 0, MaxBalance);
        if (balance == normalized)
        {
            return;
        }

        balance = normalized;
        BalanceChanged?.Invoke(balance);
    }

    private static ElegantPointWalletPayload CreatePayload()
    {
        return new ElegantPointWalletPayload
        {
            version = SavePayloadVersion,
            balance = balance
        };
    }

    [Serializable]
    private sealed class ElegantPointWalletPayload
    {
        public int version;
        public int balance;
    }

    private sealed class ElegantPointWalletSaveModule : ISaveDataModule
    {
        public int Priority => 230;

        public void Capture(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(CreatePayload());
            saveData.SetCustomSectionJson(SaveSectionKey, json);
        }

        public void Restore(SaveGameData saveData)
        {
            RestoreFromSaveData(saveData);
        }
    }
}
