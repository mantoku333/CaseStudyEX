using System;
using System.Collections.Generic;
using Metroidvania.Data;
using Player;
using UnityEngine;


/// <summary>
/// Player本体に付けるコンポーネントです。
///・装備中ItemDataを取得
///・装備画像を生成
///・装備効果をPlayerへ反映
///・Playerの向きに合わせて装備画像を反転
///・SpriteView 基準で装備を配置
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerEquipmentController : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("セーブから装備IDを復元するため、作成した装備ItemDataをここへ登録する")]
    [SerializeField] private ItemData[] equipmentCatalog;

    [Header("Visual Roots")]
    [SerializeField] private Transform visualAnchor;
    [Tooltip("Playerの後ろ側に表示する装備画像の親。未設定なら自動作成")]
    [SerializeField] private Transform backVisualRoot;

    [Tooltip("Playerの前側に表示する装備画像の親。未設定なら自動作成")]
    [SerializeField] private Transform frontVisualRoot;

    private readonly List<EquipmentVisualInstance> activeVisuals = new List<EquipmentVisualInstance>();

    private ItemData currentEquipment;
    private GunController gunController;
    private PlayerHealth playerHealth;
    private SpriteRenderer baseRenderer;
    private IPlayerViewStateProvider viewStateProvider;

    private float attackPowerMultiplier = 1f;
    private float healPercentOnEnemyKill;
    private float gunRecoilForceBonus;
    private bool lastFacingRight = true;

    public float AttackPowerMultiplier => attackPowerMultiplier;

    private void Awake()
    {
        ResolveReferences();
        EnsureVisualRoots();
    }

    private void OnEnable()
    {
        PlayerEquipmentState.EquippedItemChanged += RefreshEquipment;
    }

    private void Start()
    {
        RefreshEquipment();
    }

    private void LateUpdate()
    {
        UpdateVisualFacing();
    }

    private void OnDisable()
    {
        PlayerEquipmentState.EquippedItemChanged -= RefreshEquipment;
        ApplyGunRecoilBonus(0f);
    }

    public void RefreshEquipment()
    {
        ResolveReferences();
        EnsureVisualRoots();

        ItemData nextEquipment = ResolveEquippedItem();
        currentEquipment = nextEquipment;

        ClearVisuals();
        ResetEffects();

        if (currentEquipment == null)
        {
            ApplyGunRecoilBonus(0f);
            return;
        }

        ApplyEffects(currentEquipment);
        ApplyGunRecoilBonus(gunRecoilForceBonus);
        BuildVisuals(currentEquipment);
        UpdateVisualFacing(true);
    }

    public void NotifyEnemyKilledByPlayerAttack()
    {
        if (healPercentOnEnemyKill <= 0f)
        {
            return;
        }

        ResolveReferences();
        if (playerHealth == null)
        {
            return;
        }

        int healAmount = Mathf.CeilToInt(playerHealth.MaxHealth * healPercentOnEnemyKill);
        playerHealth.Heal(healAmount);
    }

    private ItemData ResolveEquippedItem()
    {
        ItemData stateItem = PlayerEquipmentState.EquippedItemData;
        if (stateItem != null)
        {
            return stateItem;
        }

        string equippedItemId = PlayerEquipmentState.EquippedItemId;
        if (string.IsNullOrWhiteSpace(equippedItemId) || equipmentCatalog == null)
        {
            return null;
        }

        for (int i = 0; i < equipmentCatalog.Length; i++)
        {
            ItemData itemData = equipmentCatalog[i];
            if (itemData == null)
            {
                continue;
            }

            if (string.Equals(itemData.itemId, equippedItemId, StringComparison.Ordinal))
            {
                return itemData;
            }
        }

        return null;
    }

    private void ResetEffects()
    {
        attackPowerMultiplier = 1f;
        healPercentOnEnemyKill = 0f;
        gunRecoilForceBonus = 0f;
    }

    private void ApplyEffects(ItemData itemData)
    {
        if (itemData.equipmentAbility == null) { return; }

        for (int i = 0; i < itemData.equipmentAbility.Length; i++)
        {
            EquipmentAbilityData effect = itemData.equipmentAbility[i];
            if (effect == null)
            {
                continue;
            }

            switch (effect.abilityType)
            {
                case EquipmentAbilityType.AttackPowerMultiplier:
                    attackPowerMultiplier *= Mathf.Max(0f, effect.value);
                    break;
                case EquipmentAbilityType.HealPercentOnEnemyKill:
                    healPercentOnEnemyKill += Mathf.Max(0f, effect.value);
                    break;
                case EquipmentAbilityType.GunRecoilForceBonus:
                    gunRecoilForceBonus += Mathf.Max(0f, effect.value);
                    break;
            }
        }
    }

    private void ApplyGunRecoilBonus(float bonus)
    {
        ResolveReferences();
        if (gunController != null)
        {
            gunController.SetRecoilForceBonus(bonus);
        }
    }

    private void BuildVisuals(ItemData itemData)
    {
        if (itemData.equipmentVisualLayers == null)
        {
            return;
        }

        for (int i = 0; i < itemData.equipmentVisualLayers.Length; i++)
        {
            EquipmentVisualLayerData layerData = itemData.equipmentVisualLayers[i];
            if (layerData == null || layerData.sprite == null)
            {
                continue;
            }

            Transform parent = layerData.parent == EquipmentVisualParent.Front
                ? frontVisualRoot
                : backVisualRoot;

            if (parent == null)
            {
                continue;
            }

            string objectName = string.IsNullOrWhiteSpace(layerData.layerName)
                ? $"EquipmentVisual_{i}"
                : layerData.layerName;

            GameObject visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = layerData.localPosition;
            visualObject.transform.localRotation = Quaternion.Euler(layerData.localEulerAngles);
            visualObject.transform.localScale = layerData.localScale;

            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = layerData.sprite;
            ApplyRendererSorting(renderer, layerData);

            activeVisuals.Add(new EquipmentVisualInstance
            {
                transform = visualObject.transform,
                layerData = layerData
            });
        }
    }

    private void ApplyRendererSorting(SpriteRenderer renderer, EquipmentVisualLayerData layerData)
    {
        if (renderer == null)
        {
            return;
        }

        if (baseRenderer != null)
        {
            renderer.sortingLayerID = baseRenderer.sortingLayerID;
            renderer.sortingOrder = baseRenderer.sortingOrder + layerData.sortingOrderOffset;
            return;
        }

        renderer.sortingOrder = layerData.sortingOrderOffset;
    }

    private void ClearVisuals()
    {
        activeVisuals.Clear();
        ClearChildren(backVisualRoot);
        ClearChildren(frontVisualRoot);
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void EnsureVisualRoots()
    {
        ResolveVisualAnchor();

        if (backVisualRoot == null)
        {
            backVisualRoot = EnsureChild("EquipmentVisualBack");
        }

        if (frontVisualRoot == null)
        {
            frontVisualRoot = EnsureChild("EquipmentVisualFront");
        }
    }

    private Transform EnsureChild(string childName)
    {
        Transform parent = visualAnchor != null ? visualAnchor : transform;
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private void UpdateVisualFacing(bool force = false)
    {
        ResolveReferences();

        bool isFacingRight = viewStateProvider == null || viewStateProvider.IsFacingRight;
        if (!force && isFacingRight == lastFacingRight && activeVisuals.Count > 0)
        {
            return;
        }

        lastFacingRight = isFacingRight;
        float directionScale = isFacingRight ? 1f : -1f;
        ResetRootFacing(backVisualRoot);
        ResetRootFacing(frontVisualRoot);

        for (int i = 0; i < activeVisuals.Count; i++)
        {
            EquipmentVisualInstance visual = activeVisuals[i];
            if (visual.transform == null || visual.layerData == null)
            {
                continue;
            }

            Vector3 position = visual.layerData.localPosition;
            if (!isFacingRight)
            {
                if (visual.layerData.useLeftFacingLocalPosition)
                {
                    position = visual.layerData.leftFacingLocalPosition;
                }
                else
                {
                    position.x *= -1f;
                }
            }

            Vector3 scale = visual.layerData.localScale;
            scale.x = Mathf.Abs(scale.x) * directionScale;

            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
        }
    }

    private static void ResetRootFacing(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Vector3 localScale = root.localScale;
        localScale.x = Mathf.Abs(localScale.x);
        root.localScale = localScale;
    }

    private void ResolveReferences()
    {
        if (gunController == null)
        {
            gunController = GetComponentInChildren<GunController>(true);
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (baseRenderer == null)
        {
            ResolveVisualAnchor();
            if (visualAnchor != null)
            {
                baseRenderer = visualAnchor.GetComponent<SpriteRenderer>();
            }

            if (baseRenderer == null)
            {
                baseRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        if (viewStateProvider == null)
        {
            viewStateProvider = GetComponent<IPlayerViewStateProvider>();
        }
    }

    private void ResolveVisualAnchor()
    {
        if (visualAnchor != null)
        {
            return;
        }

        Transform spriteView = transform.Find("SpriteView");
        if (spriteView != null)
        {
            visualAnchor = spriteView;
        }
    }

    private sealed class EquipmentVisualInstance
    {
        public Transform transform;
        public EquipmentVisualLayerData layerData;
    }
}
