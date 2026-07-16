#if !DISABLE_SRDEBUGGER && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using SRDebugger;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Opens/closes SRDebugger with gamepad menu/start button.
/// Auto-created at runtime, no scene setup required.
/// </summary>
public class SRDebuggerGamepadMenuShortcut : MonoBehaviour
{
    [SerializeField] private bool requireEntryCode = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Object.FindFirstObjectByType<SRDebuggerGamepadMenuShortcut>() != null)
        {
            return;
        }

        var go = new GameObject("[SRDebuggerGamepadMenuShortcut]");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<SRDebuggerGamepadMenuShortcut>();
    }

    private void Update()
    {
        if (!WasMenuPressedThisFrame())
        {
            return;
        }

        TogglePanel();
    }

    private static bool WasMenuPressedThisFrame()
    {
        if (Gamepad.all.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            var gamepad = Gamepad.all[i];
            if (gamepad == null)
            {
                continue;
            }

            if (gamepad.startButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    private void TogglePanel()
    {
        if (!SRDebug.IsInitialized)
        {
            SRDebug.Init();
        }

        var debugService = SRDebug.Instance;
        if (debugService == null)
        {
            return;
        }

        if (debugService.IsDebugPanelVisible)
        {
            debugService.HideDebugPanel();
        }
        else
        {
            debugService.ShowDebugPanel(requireEntryCode);
        }
    }
}
#endif
