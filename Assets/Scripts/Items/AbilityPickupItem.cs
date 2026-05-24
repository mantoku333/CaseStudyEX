using UnityEngine;
using Player;

public class AbilityPickupItem : MonoBehaviour, ISaveDataModule
{
    //--------------能力関連------------------
    [SerializeField] private PlayerAbilityType abilityType = PlayerAbilityType.None;

    //--------------状態関連------------------
    private bool isPickedUp = false;

    public int Priority => 251;

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
        BoxCollider2D col = GetComponent<BoxCollider2D>();

        if (col == null)
        {
            Debug.LogError("BoxCollider2Dが付いていません", this);
            return;
        }

        if (!col.isTrigger)
        {
            Debug.LogWarning("BoxCollider2DがTriggerになっていません", this);
        }

        if (IsAlreadyUnlocked())
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
        if (isPickedUp){ return; }

        PlayerAbilityController abilityController = other.GetComponent<PlayerAbilityController>();

        if (abilityController == null)
        {
            abilityController = other.GetComponentInParent<PlayerAbilityController>();
        }

        if (abilityController == null){ return; }

        if (IsAlreadyUnlocked())
        {
            CompletePickup();
            return;
        }

        UnlockAbility(abilityController);

        CompletePickup();
    }

    private bool IsAlreadyUnlocked()
    {
        if (abilityType == PlayerAbilityType.Dodge)
        {
            return GameProgressFlags.Get(GameProgressKeys.AbilityDodgeUnlocked);
        }

        if (abilityType == PlayerAbilityType.Glide)
        {
            return GameProgressFlags.Get(GameProgressKeys.AbilityGlideUnlocked);
        }

        if (abilityType == PlayerAbilityType.GunRecoil)
        {
            return GameProgressFlags.Get(GameProgressKeys.AbilityGunRecoilUnlocked);
        }

        return false;
    }

    private void UnlockAbility(PlayerAbilityController abilityController)
    {
        if (abilityType == PlayerAbilityType.Dodge)
        {
            abilityController.SetCanDodge(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, true);
            Debug.Log("回避能力を取得しました！");
            return;
        }

        if (abilityType == PlayerAbilityType.Glide)
        {
            abilityController.SetCanGlide(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, true);
            Debug.Log("滑空能力を取得しました！");
            return;
        }

        if (abilityType == PlayerAbilityType.GunRecoil)
        {
            abilityController.SetCanGunRecoil(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, true);
            Debug.Log("銃反動能力を取得しました！");
            return;
        }
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
        if (IsAlreadyUnlocked())
        {
            Destroy(gameObject);
        }
    }
}
