using System;
using System.ComponentModel;
using Player;
using UnityEngine;

public partial class SROptions
{
    private const string AbilitiesCategory = "Abilities";

    [Category(AbilitiesCategory)]
    [DisplayName("回避取得")]
    [Sort(-40)]
    public void UnlockDodgeAbility()
    {
        UnlockAbility(
            PlayerAbilityType.Dodge,
            GameProgressKeys.AbilityDodgeUnlocked,
            controller => controller.UnlockDodge());
    }

    [Category(AbilitiesCategory)]
    [DisplayName("滑空取得")]
    [Sort(-39)]
    public void UnlockGlideAbility()
    {
        UnlockAbility(
            PlayerAbilityType.Glide,
            GameProgressKeys.AbilityGlideUnlocked,
            controller => controller.UnlockGlide());
    }

    [Category(AbilitiesCategory)]
    [DisplayName("銃反動取得")]
    [Sort(-38)]
    public void UnlockGunRecoilAbility()
    {
        UnlockAbility(
            PlayerAbilityType.GunRecoil,
            GameProgressKeys.AbilityGunRecoilUnlocked,
            controller => controller.UnlockGunRecoil());
    }

    [Category(AbilitiesCategory)]
    [DisplayName("パリィ取得")]
    [Sort(-37)]
    public void UnlockParryAbility()
    {
        UnlockAbility(
            PlayerAbilityType.Parry,
            GameProgressKeys.AbilityParryUnlocked,
            controller => controller.UnlockParry());
    }

    [Category(AbilitiesCategory)]
    [DisplayName("全アビリティ取得")]
    [Sort(-36)]
    public void UnlockAllAbilities()
    {
        UnlockDodgeAbility();
        UnlockGlideAbility();
        UnlockGunRecoilAbility();
        UnlockParryAbility();
    }

    [Category(AbilitiesCategory)]
    [DisplayName("回避取得済み")]
    [Sort(-30)]
    public bool IsDodgeAbilityUnlocked => GameProgressFlags.Get(GameProgressKeys.AbilityDodgeUnlocked);

    [Category(AbilitiesCategory)]
    [DisplayName("滑空取得済み")]
    [Sort(-29)]
    public bool IsGlideAbilityUnlocked => GameProgressFlags.Get(GameProgressKeys.AbilityGlideUnlocked);

    [Category(AbilitiesCategory)]
    [DisplayName("銃反動取得済み")]
    [Sort(-28)]
    public bool IsGunRecoilAbilityUnlocked => GameProgressFlags.Get(GameProgressKeys.AbilityGunRecoilUnlocked);

    [Category(AbilitiesCategory)]
    [DisplayName("パリィ取得済み")]
    [Sort(-27)]
    public bool IsParryAbilityUnlocked => GameProgressFlags.Get(GameProgressKeys.AbilityParryUnlocked);

    private static void UnlockAbility(
        PlayerAbilityType abilityType,
        string flagKey,
        Action<PlayerAbilityController> unlockAction)
    {
        PlayerAbilityController abilityController = ResolvePlayerAbilityController();
        if (abilityController != null)
        {
            unlockAction?.Invoke(abilityController);
            return;
        }

        GameProgressFlags.Set(flagKey, true);
        Debug.LogWarning(
            $"[SROptions] PlayerAbilityController not found. Set progress flag only. ability={abilityType}, flag='{flagKey}'");
    }

    private static PlayerAbilityController ResolvePlayerAbilityController()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            PlayerAbilityController abilityController = playerObject.GetComponent<PlayerAbilityController>();
            if (abilityController != null)
            {
                return abilityController;
            }

            abilityController = playerObject.GetComponentInChildren<PlayerAbilityController>(true);
            if (abilityController != null)
            {
                return abilityController;
            }
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerAbilityController>(FindObjectsInactive.Include);
    }
}
