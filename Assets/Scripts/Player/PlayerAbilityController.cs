using UnityEngine;

namespace Player
{
    public class PlayerAbilityController : MonoBehaviour
    {
        //--------------能力関連------------------
        [SerializeField] private bool m_CanDodge = false;

        //--------Set関数-------
        public void SetCanDodge(bool canDodge)
        {
            m_CanDodge = canDodge;
        }

        //--------Get関数-------
        public bool GetCanDodge()
        {
            return m_CanDodge;
        }
    }
}
