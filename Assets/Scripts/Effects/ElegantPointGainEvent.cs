using System;
using UnityEngine;

public struct ElegantPointGainEvent
{
    public ElegantPointGainEvent(int amount, int balance, Vector3 worldPosition)
    {
        Amount = amount;
        Balance = balance;
        WorldPosition = worldPosition;
    }

    public int Amount { get; }
    public int Balance { get; }
    public Vector3 WorldPosition { get; }
}

public static class ElegantPointGainEvents
{
    public static event Action<ElegantPointGainEvent> Gained;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Gained = null;
    }

    public static void Raise(int amount, int balance, Vector3 worldPosition)
    {
        if (amount <= 0)
        {
            return;
        }

        Gained?.Invoke(new ElegantPointGainEvent(amount, balance, worldPosition));
    }
}
