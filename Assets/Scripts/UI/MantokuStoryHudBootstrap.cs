using UnityEngine;
using UnityEngine.UI;

public static class MantokuStoryHudBootstrap
{
    private const string HudCanvasName = "PlayerHUDCanvas";

    // Mantoku uses a HUD that is placed in the scene hierarchy ahead of time.
    // This method only finds that canvas and wires the visibility gate used by
    // the prologue flow. It does not create HUD visuals at runtime.
    public static Canvas EnsureHudCanvasExists()
    {
        GameObject existing = GameObject.Find(HudCanvasName);
        if (existing == null || !existing.TryGetComponent(out Canvas existingCanvas))
        {
            Debug.LogWarning(
                "MantokuStoryHudBootstrap could not find PlayerHUDCanvas in the scene. " +
                "Place the HUD in the hierarchy before play.");
            return null;
        }

        EnsureVisibilityGate(existing);
        return existingCanvas;
    }

    private static void EnsureVisibilityGate(GameObject canvasObject)
    {
        if (!canvasObject.TryGetComponent(out FlagCanvasGroupVisibility _))
        {
            canvasObject.AddComponent<FlagCanvasGroupVisibility>();
        }

        if (!canvasObject.TryGetComponent(out GraphicRaycaster _))
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }
    }
}
