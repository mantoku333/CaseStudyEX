using UnityEngine;

[CreateAssetMenu(fileName = "ItemEffectSettings", menuName = "Metroidvania/Items/Item Effect Settings")]
public class ItemEffectSettings : ScriptableObject
{
    [Header("Frames")]
    public Texture2D loopSpriteSheet;
    [Min(1)] public int loopColumns = 5;
    [Min(1)] public int loopRows = 6;
    [Min(1)] public int loopFrameCount = 30;

    public Texture2D pickupSpriteSheet;
    [Min(1)] public int pickupColumns = 5;
    [Min(1)] public int pickupRows = 3;
    [Min(1)] public int pickupFrameCount = 15;

    [Header("Legacy Fallback")]
    public Sprite[] loopFrames;
    public Sprite[] pickupFrames;

    [Header("Timing")]
    [Min(0.01f)] public float loopFrameSeconds = 0.05f;
    [Min(0.01f)] public float pickupFrameSeconds = 0.04f;

    [Header("Import")]
    [Min(1f)] public float pixelsPerUnit = 100f;
    [Range(0f, 16f)] public float frameInsetPixels = 2f;

    [Header("Visual")]
    public Vector3 visualScale = new Vector3(1.5f, 1.5f, 1f);
    public Vector3 visualOffset = new Vector3(0f, 0.2f, 0f);
    public Vector3 pickupVisualOffset = new Vector3(0f, 0.08f, 0f);
    public int sortingOrder = 0;
}
