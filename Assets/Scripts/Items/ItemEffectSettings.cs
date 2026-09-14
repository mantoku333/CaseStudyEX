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
    public Vector3 pickupVisualScale = new Vector3(1.5f, 1.5f, 1f);
    public bool playPickupEffectAtPlayer;
    public Vector3 playerPickupVisualOffset = new Vector3(1f, 1.2f, 0f);

    /// <summary>
    /// ワールドのスプライトより十分手前。0のままだと背景や地形と描画順が並び、
    /// Z位置の前後でエフェクトが背景の裏に回ることがある。
    /// </summary>
    public const int FrontmostSortingOrder = 1000;

    [Tooltip("アイテムのエフェクトは背景や地形に隠れてはいけないため、既定でワールドより手前に置きます。")]
    public int sortingOrder = FrontmostSortingOrder;

    [Header("Heal Pickup")]
    public bool playHealEffectOnPlayer = true;
    public Texture2D healBackSpriteSheet;
    public Texture2D healFrontSpriteSheet;
    [Min(1)] public int healColumns = 5;
    [Min(1)] public int healRows = 6;
    [Min(1)] public int healFrameCount = 30;
    [Min(0.01f)] public float healFrameSeconds = 0.033f;
    [Min(1f)] public float healPixelsPerUnit = 100f;
    public Vector3 healVisualScale = Vector3.one;
    public Vector3 healVisualOffset = new Vector3(0f, 0.2f, 0f);
    public int healBackSortingOrderOffset = -1;
    public int healFrontSortingOrderOffset = 1;
}
