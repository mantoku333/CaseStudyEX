using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// Sprite based player visual controller.
    /// Animator mode only.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpriteAnimator : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private global::PlayerController statsController;

        [Header("Facing")]
        [SerializeField] private bool syncFacingFromController = true;
        [SerializeField] private bool autoCollectFlipRenderers = true;
        [SerializeField] private SpriteRenderer[] flipRenderers;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private int animatorLayer = 0;
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string runStateName = "run";
        [SerializeField] private string jumpStateName = "jump";
        [SerializeField] private string landStateName = "land";
        [SerializeField] private string dodgeStateName = "dodge";

        private enum VisualState
        {
            Idle,
            Run,
            Jump,
            Land,
            Dodge
        }

        private static readonly SpriteRenderer[] EmptyRenderers = new SpriteRenderer[0];

        private VisualState _currentState = (VisualState)(-1);
        private bool _warnedNoController;
        private bool _warnedNoAnimator;
        private SpriteRenderer[] _resolvedFlipRenderers = EmptyRenderers;
        private bool _landingLocked;
        private bool _hasPreviousGrounded;
        private bool _previousGrounded;
        private string _activeLandStateName;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            _currentState = (VisualState)(-1);
            _landingLocked = false;
            _hasPreviousGrounded = false;
            _previousGrounded = false;
            _activeLandStateName = null;
        }

        private void Update()
        {
            if (!TryReadControllerState(out var isGrounded, out var isMoving, out var isGliding, out var isDodging, out var isFacingRight))
            {
                if (!_warnedNoController)
                {
                    Debug.LogWarning("PlayerSpriteAnimator: no compatible player controller found.", this);
                    _warnedNoController = true;
                }
                return;
            }
            _warnedNoController = false;

            if (!IsAnimatorReady())
            {
                if (!_warnedNoAnimator)
                {
                    Debug.LogWarning("PlayerSpriteAnimator: Animator is missing or invalid.", this);
                    _warnedNoAnimator = true;
                }
                return;
            }
            _warnedNoAnimator = false;

            if (syncFacingFromController)
            {
                ApplyFacing(isFacingRight);
            }

            UpdateLandingLock(isGrounded);
            if (_landingLocked && _currentState == VisualState.Land && IsLandAnimationFinished())
            {
                _landingLocked = false;
            }

            var nextState = ResolveState(isGrounded, isMoving, isGliding, isDodging, _landingLocked);
            if (_currentState != nextState)
            {
                SwitchState(nextState);
            }
        }

        private void ResolveReferences()
        {
            if (statsController == null)
            {
                statsController = GetComponentInParent<global::PlayerController>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = GetComponentInParent<Animator>();
                }
            }

            if (flipRenderers != null && flipRenderers.Length > 0)
            {
                _resolvedFlipRenderers = flipRenderers;
                return;
            }

            if (!autoCollectFlipRenderers)
            {
                _resolvedFlipRenderers = EmptyRenderers;
                return;
            }

            var childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (childRenderers != null && childRenderers.Length > 0)
            {
                _resolvedFlipRenderers = childRenderers;
                return;
            }

            var parentRenderers = GetComponentsInParent<SpriteRenderer>(true);
            _resolvedFlipRenderers = parentRenderers != null && parentRenderers.Length > 0
                ? parentRenderers
                : EmptyRenderers;
        }

        private bool TryReadControllerState(out bool isGrounded, out bool isMoving, out bool isGliding, out bool isDodging, out bool isFacingRight)
        {
            if (statsController != null)
            {
                isGrounded = statsController.IsGrounded;
                isMoving = statsController.IsMoving;
                isGliding = statsController.IsGliding;
                isDodging = statsController.IsDodging;
                isFacingRight = statsController.IsFacingRight;
                return true;
            }

            isGrounded = false;
            isMoving = false;
            isGliding = false;
            isDodging = false;
            isFacingRight = true;
            return false;
        }

        private static VisualState ResolveState(bool isGrounded, bool isMoving, bool isGliding, bool isDodging, bool hasLandingLock)
        {
            if (isDodging)
            {
                return VisualState.Dodge;
            }

            if (hasLandingLock)
            {
                return VisualState.Land;
            }

            if (isGliding)
            {
                return VisualState.Jump;
            }

            if (!isGrounded)
            {
                return VisualState.Jump;
            }

            return isMoving ? VisualState.Run : VisualState.Idle;
        }

        private void UpdateLandingLock(bool isGrounded)
        {
            if (!_hasPreviousGrounded)
            {
                _previousGrounded = isGrounded;
                _hasPreviousGrounded = true;
                return;
            }

            if (!_previousGrounded && isGrounded)
            {
                _landingLocked = true;
            }
            else if (!isGrounded)
            {
                _landingLocked = false;
            }

            _previousGrounded = isGrounded;
        }

        private void ApplyFacing(bool isFacingRight)
        {
            if (_resolvedFlipRenderers == null || _resolvedFlipRenderers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _resolvedFlipRenderers.Length; i++)
            {
                var renderer = _resolvedFlipRenderers[i];
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
            _currentState = nextState;

            var stateName = ResolveAnimatorStateName(nextState);
            if (nextState == VisualState.Land)
            {
                _activeLandStateName = stateName;
            }
            else
            {
                _activeLandStateName = null;
            }

            if (!string.IsNullOrEmpty(stateName))
            {
                animator.Play(stateName, animatorLayer, 0f);
            }
        }

        private bool IsLandAnimationFinished()
        {
            if (animator == null)
            {
                return true;
            }

            if (string.IsNullOrEmpty(_activeLandStateName))
            {
                return true;
            }

            var stateInfo = animator.GetCurrentAnimatorStateInfo(animatorLayer);
            if (!stateInfo.IsName(_activeLandStateName))
            {
                return true;
            }

            return stateInfo.normalizedTime >= 1f && !animator.IsInTransition(animatorLayer);
        }

        /// <summary>
        /// land.anim の AnimationEvent 受け口。
        /// SpriteView 側でイベントを受け、必要な後処理を Controller に中継する。
        /// </summary>
        public void OnLandAnimationEnd()
        {
            _landingLocked = false;
            _activeLandStateName = null;

            if (statsController != null)
            {
                statsController.OnLandAnimationEnd();
            }
        }

        private string GetAnimatorStateName(VisualState state)
        {
            switch (state)
            {
                case VisualState.Run:
                    return runStateName;
                case VisualState.Jump:
                    return jumpStateName;
                case VisualState.Land:
                    return landStateName;
                case VisualState.Dodge:
                    return dodgeStateName;
                default:
                    return idleStateName;
            }
        }

        private string ResolveAnimatorStateName(VisualState state)
        {
            var primary = GetAnimatorStateName(state);
            if (AnimatorHasState(primary))
            {
                return primary;
            }

            switch (state)
            {
                case VisualState.Run:
                    if (AnimatorHasState("Walk")) return "Walk";
                    if (AnimatorHasState("run")) return "run";
                    break;
                case VisualState.Jump:
                    if (AnimatorHasState("Glide")) return "Glide";
                    if (AnimatorHasState("jump")) return "jump";
                    break;
                case VisualState.Land:
                    if (AnimatorHasState("Land")) return "Land";
                    if (AnimatorHasState("land")) return "land";
                    break;
                case VisualState.Dodge:
                    if (AnimatorHasState("Dodge")) return "Dodge";
                    if (AnimatorHasState("dodge")) return "dodge";
                    break;
                default:
                    if (AnimatorHasState("Idle")) return "Idle";
                    if (AnimatorHasState("idle")) return "idle";
                    break;
            }

            return primary;
        }

        private bool AnimatorHasState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            return animator.HasState(animatorLayer, Animator.StringToHash(stateName));
        }
    }
}
