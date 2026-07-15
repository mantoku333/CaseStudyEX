using UnityEngine;

/// <summary>
/// Runtime-only state for clean PV capture. The state survives scene changes and
/// resets whenever Unity starts a new play session.
/// </summary>
public static class PvModeState
{
    public static bool IsActive { get; private set; }

    public static void SetActive(bool active)
    {
        IsActive = active;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        IsActive = false;
    }
}
