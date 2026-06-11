using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// Sprite-sheet based visual controller for enemies.
    /// Plays idle, move, engage, and attack states through an Animator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpriteAnimator : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private EnemyController enemyController;
        [SerializeField] private EnemyTackleAttack tackleAttack;
        [SerializeField] private StageBossAttack stageBossAttack;
        [SerializeField] private EnemyRangedAttack rangedAttack;

        [Header("Facing")]
        [SerializeField] private bool syncFacingFromController = true;
        [SerializeField] private bool autoCollectFlipRenderers = true;
        [SerializeField] private SpriteRenderer[] flipRenderers;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private int animatorLayer = 0;
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string moveStateName = "move";
        [SerializeField] private string engageStateName = "engage";
        [SerializeField] private string attackStateName = "attack";
        [SerializeField, Min(0f)] private float moveSpeedThreshold = 0.05f;

        private enum VisualState
        {
            Idle,
            Move,
            Engage,
            Attack
        }

        private static readonly SpriteRenderer[] EmptyRenderers = new SpriteRenderer[0];

        private VisualState currentState = (VisualState)(-1);
        private SpriteRenderer[] resolvedFlipRenderers = EmptyRenderers;
        private Rigidbody2D resolvedRigidbody2D;
        private bool warnedNoAnimator;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            currentState = (VisualState)(-1);
        }

        private void Update()
        {
            if (syncFacingFromController && enemyController != null)
            {
                ApplyFacing(enemyController.FacingDirection >= 0);
            }

            if (!IsAnimatorReady())
            {
                if (!warnedNoAnimator)
                {
                    Debug.LogWarning("EnemySpriteAnimator: Animator is missing or has no controller.", this);
                    warnedNoAnimator = true;
                }

                return;
            }
            warnedNoAnimator = false;

            VisualState nextState = ResolveState();
            if (currentState != nextState)
            {
                SwitchState(nextState);
            }
        }

        private void ResolveReferences()
        {
            if (enemyController == null)
            {
                enemyController = GetComponentInParent<EnemyController>();
            }

            if (tackleAttack == null)
            {
                tackleAttack = GetComponentInParent<EnemyTackleAttack>();
            }

            if (stageBossAttack == null)
            {
                stageBossAttack = GetComponentInParent<StageBossAttack>();
            }

            if (rangedAttack == null)
            {
                rangedAttack = GetComponentInParent<EnemyRangedAttack>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            resolvedRigidbody2D = GetComponentInParent<Rigidbody2D>();

            if (flipRenderers != null && flipRenderers.Length > 0)
            {
                resolvedFlipRenderers = flipRenderers;
                return;
            }

            if (!autoCollectFlipRenderers)
            {
                resolvedFlipRenderers = EmptyRenderers;
                return;
            }

            SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            resolvedFlipRenderers = childRenderers != null && childRenderers.Length > 0
                ? childRenderers
                : EmptyRenderers;
        }

        private VisualState ResolveState()
        {
            if (tackleAttack != null && tackleAttack.IsCharging)
            {
                return VisualState.Attack;
            }

            if (tackleAttack != null && (tackleAttack.IsWindingUp || tackleAttack.IsCoolingDown))
            {
                return VisualState.Engage;
            }

            if (stageBossAttack != null && stageBossAttack.IsCharging)
            {
                return VisualState.Attack;
            }

            if (stageBossAttack != null && (stageBossAttack.IsWindingUp || stageBossAttack.IsCoolingDown))
            {
                return VisualState.Engage;
            }

            if (rangedAttack != null && rangedAttack.IsFiring)
            {
                return VisualState.Attack;
            }

            if (rangedAttack != null && rangedAttack.IsWindingUp)
            {
                return VisualState.Engage;
            }

            return IsMoving() ? VisualState.Move : VisualState.Idle;
        }

        private bool IsMoving()
        {
            if (resolvedRigidbody2D == null)
            {
                return false;
            }

            return Mathf.Abs(resolvedRigidbody2D.linearVelocity.x) > moveSpeedThreshold;
        }

        private void ApplyFacing(bool isFacingRight)
        {
            for (int i = 0; i < resolvedFlipRenderers.Length; i++)
            {
                SpriteRenderer renderer = resolvedFlipRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.flipX = !isFacingRight;
            }
        }

        private bool IsAnimatorReady()
        {
            return animator != null &&
                   animatorLayer >= 0 &&
                   animator.runtimeAnimatorController != null;
        }

        private void SwitchState(VisualState nextState)
        {
            currentState = nextState;

            string stateName = GetAnimatorStateName(nextState);
            if (string.IsNullOrEmpty(stateName))
            {
                return;
            }

            animator.Play(stateName, animatorLayer, 0f);
        }

        private string GetAnimatorStateName(VisualState state)
        {
            switch (state)
            {
                case VisualState.Attack:
                    return attackStateName;
                case VisualState.Engage:
                    return engageStateName;
                case VisualState.Move:
                    return moveStateName;
                default:
                    return idleStateName;
            }
        }
    }
}
