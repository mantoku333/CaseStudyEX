using UnityEngine;

namespace Player
{
    public sealed class PlayerFootstepSpriteEventRelay : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PlayerFootstepController footstepController;
        [SerializeField] private Sprite[] footstepSprites;

        private Sprite previousSprite;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (footstepController == null)
            {
                footstepController = GetComponentInParent<PlayerFootstepController>();
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null || footstepController == null)
            {
                return;
            }

            Sprite currentSprite = spriteRenderer.sprite;
            if (currentSprite == previousSprite)
            {
                return;
            }

            previousSprite = currentSprite;

            if (IsFootstepSprite(currentSprite))
            {
                footstepController.PlayFootstepFromAnimationEvent();
            }
        }

        private bool IsFootstepSprite(Sprite sprite)
        {
            if (sprite == null || footstepSprites == null)
            {
                return false;
            }

            for (int i = 0; i < footstepSprites.Length; i++)
            {
                if (footstepSprites[i] == sprite)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
