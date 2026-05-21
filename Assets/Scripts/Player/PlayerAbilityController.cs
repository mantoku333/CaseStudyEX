using UnityEngine;

using System;

namespace Player
{
    /// <summary>
    /// プレイヤーの能力解放の管理を行うクラス。
    /// 各解放場所からアクセスできるように制作する(中江)
    /// </summary>
    public class PlayerAbilityController : MonoBehaviour, ISaveDataModule
    {
        private const string SectionKey = "player_abilities_v1";

        //--------------能力解放判定関連------------------
        [Header("能力解放判定関連")]
        [SerializeField] private bool canDodge = false;
        [SerializeField] private bool canGlide = false;
        [SerializeField] private bool canGunRecoil = false;
        [SerializeField] private bool canParry = false;

        public int Priority => 220;

        private void Awake()
        {
            LoadAbilitieItemsFlags();
        }

        private void OnEnable()
        {
            SaveManager.RegisterModule(this);
        }

        private void OnDisable()
        {
            SaveManager.UnregisterModule(this);
        }

        //--------Set関数-------
        //回避
        public void SetCanDodge(bool isEnabled)
        {
            canDodge = isEnabled;
            GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, isEnabled);
        }

        //滑空
        public void SetCanGlide(bool isEnabled)
        {
            canGlide = isEnabled;
            GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, isEnabled);
        }

        //銃反動
        public void SetCanGunRecoil(bool isEnabled)
        {
            canGunRecoil = isEnabled;
            GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, isEnabled);
        }

        //パリィ
        public void SetCanParry(bool isEnabled)
        {
             canParry = isEnabled;
        }

        //--------Get関数-------
        //回避
        public bool GetCanDodge()
        {
            return canDodge;
        }

        //滑空
        public bool GetCanGlide()
        {
            return canGlide;
        }

        //反動
        public bool GetCanGunRecoil()
        {
            return canGunRecoil;
        }

        //パリィ
        public bool GetCanParry()
        {
            return canParry;
        }

        /// <summary>
        /// 回避能力を解放する関数。解放フラグを立て、
        /// セーブデータに保存する
        /// </summary>
        public void UnlockDodge()
        {
            SetCanDodge(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, true);
            Debug.Log("回避能力を解放しました。");
        }

        /// <summary>
        /// 滑空能力を解放する関数。解放フラグを立て、
        /// セーブデータに保存する
        /// </summary>
        public void UnlockGlide()
        {
            SetCanGlide(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, true);
            Debug.Log("滑空能力を解放しました。");
        }

        /// <summary>
        /// 銃反動能力を解放する関数。解放フラグを立て、
        /// セーブデータに保存する
        /// </summary>
        public void UnlockGunRecoil()
        {
            SetCanGunRecoil(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, true);
            Debug.Log("銃反動能力を解放しました。");
        }
        /// <summary>
        /// パリィ能力を解放する関数。解放フラグを立て、
        /// セーブデータに保存する
        /// </summary>
        public void UnlockParry()
        {
            SetCanParry(true);
            GameProgressFlags.Set(GameProgressKeys.AbilityParryUnlocked, true);
            Debug.Log("パリィ能力を解放しました。");
        }

        public void UnlockAbility(PlayerAbilityType abilityType)
        {
            if (abilityType == PlayerAbilityType.Dodge)
            {
                UnlockDodge();
                return;
            }

            if (abilityType == PlayerAbilityType.Glide)
            {
                UnlockGlide();
                return;
            }

            if (abilityType == PlayerAbilityType.GunRecoil)
            {
                UnlockGunRecoil();
                return;
            }

            if(abilityType == PlayerAbilityType.Parry)
            {
                UnlockParry();
                return;
            }
        }

        //セーブデータからアイテム取得状況を復元するための関数
        public void LoadAbilitieItemsFlags()
        {
            canDodge = GameProgressFlags.Get(GameProgressKeys.AbilityDodgeUnlocked);
            canGlide = GameProgressFlags.Get(GameProgressKeys.AbilityGlideUnlocked);
            canGunRecoil = GameProgressFlags.Get(GameProgressKeys.AbilityGunRecoilUnlocked);
            canParry = GameProgressFlags.Get(GameProgressKeys.AbilityParryUnlocked);
        }
        public void Capture(SaveGameData saveData)
        {
            GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, canDodge);
            GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, canGlide);
            GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, canGunRecoil);

            if (saveData == null)
            {
                return;
            }

            var payload = new PlayerAbilityPayload
            {
                canDodge = canDodge,
                canGlide = canGlide,
                canGunRecoil = canGunRecoil
            };

            saveData.SetCustomSectionJson(SectionKey, JsonUtility.ToJson(payload));
        }

        public void Restore(SaveGameData saveData)
        {
            if (saveData != null)
            {
                string json = saveData.GetCustomSectionJson(SectionKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        PlayerAbilityPayload payload = JsonUtility.FromJson<PlayerAbilityPayload>(json);
                        canDodge = payload.canDodge;
                        canGlide = payload.canGlide;
                        canGunRecoil = payload.canGunRecoil;
                        GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, canDodge);
                        GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, canGlide);
                        GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, canGunRecoil);
                        return;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[PlayerAbilityController] Failed to parse saved abilities. {exception}");
                    }
                }
            }

            LoadAbilitieItemsFlags();
        }

        [Serializable]
        private struct PlayerAbilityPayload
        {
            public bool canDodge;
            public bool canGlide;
            public bool canGunRecoil;
        }
    }
}
