using UnityEngine;

namespace Player
{
    public class PlayerAbilityController : MonoBehaviour
    {
        //--------------能力関連------------------
        [SerializeField] private bool canDodge = false;

        //--------Set関数-------
        public void SetCanDodge(bool isEnabled)
        {
            canDodge = isEnabled;
        }

        //--------Get関数-------
        public bool GetCanDodge()
        {
            return canDodge;
        }
    }
}
