using Metroidvania.Data;
using Player;
using UnityEngine;

public class ItemPickup : MonoBehaviour, ISaveDataModule
{
    //--------------アイテムデータ関連------------------
    [SerializeField] private ItemData itemData;

    //--------------状態関連------------------
    private bool isPickedUp = false;

    public int Priority => 250;

    private void OnEnable()
    {
        SaveManager.RegisterModule(this);
    }

    private void OnDisable()
    {
        SaveManager.UnregisterModule(this);
    }

    private void Start()
    {
        BoxCollider2D collider2D = GetComponent<BoxCollider2D>();

        if (collider2D == null)
        {
            Debug.LogError("BoxCollider2Dが付いていません", this);
            return;
        }

        if (!collider2D.isTrigger)
        {
            Debug.LogWarning("BoxCollider2DがTriggerになっていません", this);
        }

        if (IsAlreadyPickedUp())
        {
            Destroy(gameObject);
        }
    }

    private void Reset()
    {
        BoxCollider2D collider2D = GetComponent<BoxCollider2D>();

        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")){ return; }

        Debug.Log($"GunAbilityItemに触れた: {other.name}", this);

        if (isPickedUp) { return; }

        if (itemData == null)
        {
            Debug.LogWarning("ItemDataが設定されていません", this);
            return;
        }

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        }

        PlayerAbilityController abilityController = other.GetComponent<PlayerAbilityController>();

        if (abilityController == null)
        {
            abilityController = other.GetComponentInParent<PlayerAbilityController>();
        }

        bool isApplied = ApplyItem(playerHealth, abilityController);

        if (!isApplied) { return; }

        if (IsSaveTargetItem())
        {
            GameProgressFlags.Set(itemData.itemId, true);
        }

        Debug.Log($"{itemData.itemName} を取得しました！");

        CompletePickup();
    }

    private bool ApplyItem(PlayerHealth playerHealth, PlayerAbilityController abilityController)
    {
        bool isApplied = false;

        if (itemData.healAmount > 0)
        {
            if (playerHealth == null) { return false; }

            playerHealth.Heal(itemData.healAmount);
            isApplied = true;
        }

        if (itemData.maxHealthBonus > 0)
        {
            if (playerHealth == null) { return false; }

            playerHealth.AddMaxHealth(itemData.maxHealthBonus, true);
            isApplied = true;
        }

        if (itemData.abilityType != PlayerAbilityType.None)
        {
            if (abilityController == null) { return false; }

            UnlockAbility(abilityController);
            isApplied = true;
        }

        if (!isApplied && IsInventoryItem())
        {
            GameItems.AddCount(itemData.itemId, 1);
            isApplied = true;
        }

        return isApplied;
    }

    private void UnlockAbility(PlayerAbilityController abilityController)
    {
        if (itemData.abilityType == PlayerAbilityType.Dodge)
        {
            abilityController.SetCanDodge(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, true);
            return;
        }

        if (itemData.abilityType == PlayerAbilityType.Glide)
        {
            abilityController.SetCanGlide(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, true);
            return;
        }

        if (itemData.abilityType == PlayerAbilityType.GunRecoil)
        {
            abilityController.SetCanGunRecoil(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, true);
            return;
        }

        if (itemData.abilityType == PlayerAbilityType.Parry)
        {
            abilityController.SetCanParry(true);
            return;
        }
    }

    private bool IsAlreadyPickedUp()
    {
        if (itemData == null)
        {
            return false;
        }

        if (!IsSaveTargetItem())
        {
            return false;
        }

        return GameProgressFlags.Get(itemData.itemId);
    }

    private bool IsSaveTargetItem()
    {
        if (itemData == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(itemData.itemId))
        {
            return false;
        }

        if (itemData.abilityType != PlayerAbilityType.None)
        {
            return true;
        }

        if (itemData.maxHealthBonus > 0)
        {
            return true;
        }

        if (IsInventoryItem())
        {
            return true;
        }

        return false;
    }

    private bool IsInventoryItem()
    {
        if (itemData == null || string.IsNullOrWhiteSpace(itemData.itemId))
        {
            return false;
        }

        return itemData.itemType == ItemType.KeyItem ||
               itemData.itemType == ItemType.Equipment ||
               itemData.itemType == ItemType.Collectible;
    }

    private void CompletePickup()
    {
        isPickedUp = true;

        ItemEffectController effectController = GetComponent<ItemEffectController>();
        if (effectController != null && effectController.PlayPickupEffectAndDestroy())
        {
            return;
        }

        Destroy(gameObject);
    }

    public void Capture(SaveGameData saveData)
    {
    }

    public void Restore(SaveGameData saveData)
    {
        if (IsAlreadyPickedUp())
        {
            Destroy(gameObject);
        }
    }
}
