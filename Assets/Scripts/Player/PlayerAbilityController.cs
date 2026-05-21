using UnityEngine;

using System;

namespace Player
{
    public class PlayerAbilityController : MonoBehaviour, ISaveDataModule
    {
        private const string SectionKey = "player_abilities_v1";

        //--------------能力解放判定関連------------------
        [Header("能力解放判定関連")]
        [SerializeField] private bool canDodge = false;
        [SerializeField] private bool canGlide = false;
        [SerializeField] private bool canGunRecoil = false;

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
        public void SetCanDodge(bool isEnabled)
        {
            canDodge = isEnabled;
            GameProgressFlags.Set(GameProgressKeys.AbilityDodgeUnlocked, isEnabled);
        }
        
        public void SetCanGlide(bool isEnabled)
        {
            canGlide = isEnabled;
            GameProgressFlags.Set(GameProgressKeys.AbilityGlideUnlocked, isEnabled);
        }
        
        public void SetCanGunRecoil(bool isEnabled)
        {
            canGunRecoil = isEnabled;
            GameProgressFlags.Set(GameProgressKeys.AbilityGunRecoilUnlocked, isEnabled);
        }

        //--------Get関数-------
        public bool GetCanDodge()
        {
            return canDodge;
        }

        public bool GetCanGlide()
        {
            return canGlide;
        }

        public bool GetCanGunRecoil()
        {
            return canGunRecoil;
        }

        //セーブデータからアイテム取得状況を復元するための関数
        public void LoadAbilitieItemsFlags()
        {
            canDodge = GameProgressFlags.Get(GameProgressKeys.AbilityDodgeUnlocked);
            canGlide = GameProgressFlags.Get(GameProgressKeys.AbilityGlideUnlocked);
            canGunRecoil = GameProgressFlags.Get(GameProgressKeys.AbilityGunRecoilUnlocked);
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
