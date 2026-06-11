using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class LastBossSpriteAnimator : MonoBehaviour
    {
        [Header("Renderer")]
        [SerializeField] private SpriteRenderer mainRenderer;
        [SerializeField] private bool flipXWhenFacingRight = false;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private int animatorLayer = 0;
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string moveStateName = "move";
        [SerializeField] private string normalAttackStateName = "attack_normal";
        [SerializeField] private string horizontalStartStateName = "attack_horizontal_start";
        [SerializeField] private string horizontalEndStateName = "attack_horizontal_end";
        [SerializeField] private string verticalStartStateName = "attack_vertical_start";
        [SerializeField] private string verticalEndStateName = "attack_vertical_end";
        [SerializeField] private string downStartStateName = "down_start";
        [SerializeField] private string downHoldStateName = "down_hold";
        [SerializeField] private string downEndStateName = "down_end";

        [Header("Timing")]
        [SerializeField, Min(0f)] private float downStartDuration = 3.75f;
        [SerializeField, Min(0f)] private float downEndDuration = 3.0833333f;

        private enum VisualState
        {
            None,
            Idle,
            Move,
            NormalAttack,
            HorizontalStart,
            HorizontalEnd,
            VerticalStart,
            VerticalEnd,
            DownStart,
            DownHold,
            DownEnd,
            Dead
        }

        private VisualState currentState = VisualState.None;
        private Color defaultColor = Color.white;
        private bool warnedNoAnimator;

        public SpriteRenderer MainRenderer
        {
            get
            {
                ResolveReferences();
                return mainRenderer;
            }
        }

        public Color DefaultColor => defaultColor;
        public float DownStartDuration => downStartDuration;
        public float DownEndDuration => downEndDuration;

        private void Awake()
        {
            ResolveReferences();
            if (mainRenderer != null)
            {
                defaultColor = mainRenderer.color;
            }
        }

        private void OnEnable()
        {
            currentState = VisualState.None;
            PlayIdle();
        }

        public void SetFacing(int facingDirection)
        {
            if (MainRenderer == null)
            {
                return;
            }

            mainRenderer.flipX = facingDirection >= 0 ? flipXWhenFacingRight : !flipXWhenFacingRight;
        }

        public void SetColor(Color color)
        {
            if (MainRenderer != null)
            {
                mainRenderer.color = color;
            }
        }

        public void PlayIdle()
        {
            PlayState(VisualState.Idle, idleStateName);
        }

        public void PlayMove()
        {
            PlayState(VisualState.Move, moveStateName);
        }

        public void PlayNormalAttack()
        {
            PlayState(VisualState.NormalAttack, normalAttackStateName);
        }

        public void PlayHorizontalStart()
        {
            PlayState(VisualState.HorizontalStart, horizontalStartStateName);
        }

        public void PlayHorizontalEnd()
        {
            PlayState(VisualState.HorizontalEnd, horizontalEndStateName);
        }

        public void PlayVerticalStart()
        {
            PlayState(VisualState.VerticalStart, verticalStartStateName);
        }

        public void PlayVerticalEnd()
        {
            PlayState(VisualState.VerticalEnd, verticalEndStateName);
        }

        public void PlayDownStart()
        {
            PlayState(VisualState.DownStart, downStartStateName);
        }

        public void PlayDownHold()
        {
            PlayState(VisualState.DownHold, downHoldStateName);
        }

        public void PlayDownEnd()
        {
            PlayState(VisualState.DownEnd, downEndStateName);
        }

        public void PlayDead()
        {
            currentState = VisualState.Dead;
        }

        private void PlayState(VisualState nextState, string stateName)
        {
            if (currentState == nextState)
            {
                return;
            }

            currentState = nextState;
            if (string.IsNullOrEmpty(stateName) || !IsAnimatorReady())
            {
                return;
            }

            animator.Play(stateName, animatorLayer, 0f);
        }

        private bool IsAnimatorReady()
        {
            bool isReady = animator != null &&
                           animatorLayer >= 0 &&
                           animator.runtimeAnimatorController != null;
            if (!isReady && !warnedNoAnimator)
            {
                Debug.LogWarning("LastBossSpriteAnimator: Animator is missing or has no controller.", this);
                warnedNoAnimator = true;
            }

            if (isReady)
            {
                warnedNoAnimator = false;
            }

            return isReady;
        }

        private void ResolveReferences()
        {
            if (mainRenderer == null)
            {
                mainRenderer = GetComponent<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }
    }
}
