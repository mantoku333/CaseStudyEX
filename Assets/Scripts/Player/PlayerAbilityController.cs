using UnityEngine;

namespace Player
{

    /// <summary>
    /// プレイヤーの能力解放の管理を行うクラス。
    /// 各解放場所からアクセスできるように制作する(中江)
    /// </summary>
    public class PlayerAbilityController : MonoBehaviour
    {
        //--------------能力解放判定関連------------------
        [Header("能力解放判定関連")]
        [SerializeField] private bool canDodge = false;
        [SerializeField] private bool canGlide = false;
        [SerializeField] private bool canGunRecoil = false;
        [SerializeField] private bool canParry = false;

        private void Awake()
        {
            LoadAbilitieItemsFlags();
        }

        //--------Set関数-------
        //回避
        public void SetCanDodge(bool isEnabled)
        {
            canDodge = isEnabled;
        }

        //滑空
        public void SetCanGlide(bool isEnabled)
        {
            canGlide = isEnabled;
        }

        //銃反動
        public void SetCanGunRecoil(bool isEnabled)
        {
            canGunRecoil = isEnabled;
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
    }
}
