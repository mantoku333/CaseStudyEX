using UnityEngine;

namespace Player
{
    public class PlayerAbilityController : MonoBehaviour
    {
        //--------------能力解放判定関連------------------
        [Header("能力解放判定関連")]
        [SerializeField] private bool canDodge = false;
        [SerializeField] private bool canGlide = false;
        [SerializeField] private bool canGunRecoil = false;

        private void Awake()
        {
            LoadAbilitieItemsFlags();
        }

        //--------Set関数-------
        public void SetCanDodge(bool isEnabled)
        {
            canDodge = isEnabled;
        }
        
        public void SetCanGlide(bool isEnabled)
        {
            canGlide = isEnabled;
        }
        
        public void SetCanGunRecoil(bool isEnabled)
        {
            canGunRecoil = isEnabled;
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

    }
}
