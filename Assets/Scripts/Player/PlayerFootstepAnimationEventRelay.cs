using UnityEngine;

namespace Player
{
    public sealed class PlayerFootstepAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private PlayerFootstepController footstepController;

        private void Awake()
        {
            if (footstepController == null)
            {
                footstepController = GetComponentInParent<PlayerFootstepController>();
            }
        }

        public void OnFootstep()
        {
            if (footstepController == null)
            {
                return;
            }

            footstepController.PlayFootstepFromAnimationEvent();
        }
    }
}
