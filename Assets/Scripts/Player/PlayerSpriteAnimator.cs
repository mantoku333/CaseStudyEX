using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Player
{
    /// <summary>
    /// Sprite based player visual controller.
    /// Animator mode only.
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom("Metroidvania.Player")]
    public class PlayerSpriteAnimator : MonoBehaviour
    {
        [Header("Controller")]
        [FormerlySerializedAs("statsController")]
        [SerializeField] private MonoBehaviour stateProviderSource;

        [Header("Facing")]
        [SerializeField] private bool syncFacingFromController = true;
        [SerializeField] private bool autoCollectFlipRenderers = true;
        [SerializeField] private SpriteRenderer[] flipRenderers;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [SerializeField] private int animatorLayer = 0;
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string closedIdleStateName = "idle_close";
        [SerializeField] private string runStateName = "run";
        [SerializeField] private string closedRunStateName = "walk_close";
        [SerializeField] private string jumpStateName = "jump";
        [SerializeField] private string closedJumpStateName = "jump_close";
        [SerializeField] private string glideStateName = "glide";
        [SerializeField] private string landStateName = "land";
        [SerializeField] private string closedLandStateName = "land_close";
        [SerializeField] private string dodgeStateName = "dodge";
        [SerializeField] private string parryStateName = "parry";
        [SerializeField] private string changeStateName = "change";
        [SerializeField] private string attackStateName = "attack";
        [SerializeField] private string recoilBoostSkyStateName = "recoilboost_sky";

        [Header("Cutscene Movement")]
        [SerializeField] private bool useTransformDeltaAsMovement = true;
        [SerializeField, Min(0f)] private float transformMovementThreshold = 0.001f;

        [Header("Debug")]
        [SerializeField] private bool logCurrentSpriteEveryFrame = true;

        private enum VisualState
        {
            Idle,
            Run,
            Jump,
            Glide,
            Land,
            Dodge,
            Parry,
            Change,
            Attack,
            RecoilBoostSky
        }

        private static readonly SpriteRenderer[] EmptyRenderers = new SpriteRenderer[0];

        private VisualState _currentState = (VisualState)(-1);
        private bool _warnedNoStateProvider;
        private bool _warnedNoAnimator;
        private IPlayerViewStateProvider _stateProvider;
        private SpriteRenderer[] _resolvedFlipRenderers = EmptyRenderers;
        private bool _landingLocked;
        private bool _hasPreviousGrounded;
        private bool _previousGrounded;
        private bool _currentUmbrellaOpen;
        private string _activeLandStateName;
        private string _currentAnimatorStateName;
        private bool _hasPreviousWorldPosition;
        private Vector3 _previousWorldPosition;

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
            _currentUmbrellaOpen = false;
            _activeLandStateName = null;
            _currentAnimatorStateName = null;
            _hasPreviousWorldPosition = false;
            _previousWorldPosition = transform.position;
        }

        private void Update()
        {
            if (!TryReadProviderState(out var isGrounded, out var isMoving, out var isGliding, out var isUmbrellaOpen, out var isDodging, out var isFacingRight, out var isParrying, out var isChanging, out var isAttacking, out var isRecoilBoosting))
            {
                if (!_warnedNoStateProvider)
                {
                    Debug.LogWarning("PlayerSpriteAnimator: no compatible state provider found.", this);
                    _warnedNoStateProvider = true;
                }
                return;
            }
            _warnedNoStateProvider = false;

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

            bool cutsceneMovementActive = TryResolveCutsceneMovement(out bool cutsceneFacingRight);
            if (cutsceneMovementActive && !isDodging && !isGliding && !isRecoilBoosting)
            {
                isMoving = true;
                isGrounded = true;
                isFacingRight = cutsceneFacingRight;
            }

            if (syncFacingFromController)
            {
                ApplyFacing(isFacingRight);
            }

            UpdateLandingLock(isGrounded);
            if (_landingLocked && _currentState == VisualState.Land && IsLandAnimationFinished())
            {
                _landingLocked = false;
            }

            var nextState = ResolveState(isGrounded, isMoving, isGliding, isDodging, isParrying, isChanging, isAttacking, isRecoilBoosting, _landingLocked);
            if (_currentState != nextState || _currentUmbrellaOpen != isUmbrellaOpen)
            {
                SwitchState(nextState, isUmbrellaOpen);
            }
        }

        private void LateUpdate()
        {
            if (!logCurrentSpriteEveryFrame)
            {
                return;
            }

            //Debug.Log(BuildCurrentSpriteLog(), this);
        }

        private void ResolveReferences()
        {
            if (stateProviderSource == null)
            {
                var providerInParent = GetComponentInParent<IPlayerViewStateProvider>();
                if (providerInParent is MonoBehaviour monoProvider)
                {
                    stateProviderSource = monoProvider;
                }
            }

            _stateProvider = stateProviderSource as IPlayerViewStateProvider;
            if (_stateProvider == null && stateProviderSource != null)
            {
                Debug.LogWarning("PlayerSpriteAnimator: assigned state provider does not implement IPlayerViewStateProvider.", this);
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

        private bool TryReadProviderState(out bool isGrounded, out bool isMoving, out bool isGliding, out bool isUmbrellaOpen, out bool isDodging, out bool isFacingRight, out bool isParrying, out bool isChanging, out bool isAttacking, out bool isRecoilBoosting)
        {
            if (_stateProvider != null)
            {
                isGrounded = _stateProvider.IsGrounded;
                isMoving = _stateProvider.IsMoving;
                isGliding = _stateProvider.IsGliding;
                isUmbrellaOpen = _stateProvider.IsUmbrellaOpen;
                isDodging = _stateProvider.IsDodging;
                isFacingRight = _stateProvider.IsFacingRight;
                isParrying = _stateProvider.IsParrying;
                isChanging = _stateProvider.IsUmbrellaChanging;
                isAttacking = _stateProvider.IsAttacking;
                isRecoilBoosting = _stateProvider.IsRecoilBoosting;
                return true;
            }

            isGrounded = false;
            isMoving = false;
            isGliding = false;
            isUmbrellaOpen = false;
            isDodging = false;
            isFacingRight = true;
            isParrying = false;
            isChanging = false;
            isAttacking = false;
            isRecoilBoosting = false;
            return false;
        }

        private static VisualState ResolveState(bool isGrounded, bool isMoving, bool isGliding, bool isDodging, bool isParrying, bool isChanging, bool isAttacking, bool isRecoilBoosting, bool hasLandingLock)
        {
            if (isParrying)
            {
                return VisualState.Parry;
            }

            if (isChanging)
            {
                return VisualState.Change;
            }

            if (isAttacking)
            {
                return VisualState.Attack;
            }

            if (isDodging)
            {
                return VisualState.Dodge;
            }

            if (isRecoilBoosting)
            {
                return VisualState.RecoilBoostSky;
            }

            if (hasLandingLock)
            {
                return VisualState.Land;
            }

            if (isGliding)
            {
                return VisualState.Glide;
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

        private void SwitchState(VisualState nextState, bool isUmbrellaOpen)
        {
            _currentState = nextState;
            _currentUmbrellaOpen = isUmbrellaOpen;

            var stateName = ResolveAnimatorStateName(nextState, isUmbrellaOpen);
            _currentAnimatorStateName = stateName;
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
        /// Receives AnimationEvent from land.anim.
        /// Kept for compatibility with clip event wiring.
        /// </summary>
        public void OnLandAnimationEnd()
        {
            _landingLocked = false;
            _activeLandStateName = null;
        }

        private string GetAnimatorStateName(VisualState state, bool isUmbrellaOpen)
        {
            switch (state)
            {
                case VisualState.Run:
                    if (!isUmbrellaOpen && !string.IsNullOrEmpty(closedRunStateName))
                    {
                        return closedRunStateName;
                    }
                    return runStateName;
                case VisualState.Jump:
                    if (!isUmbrellaOpen && !string.IsNullOrEmpty(closedJumpStateName))
                    {
                        return closedJumpStateName;
                    }
                    return jumpStateName;
                case VisualState.Glide:
                    return glideStateName;
                case VisualState.Land:
                    if (!isUmbrellaOpen && !string.IsNullOrEmpty(closedLandStateName))
                    {
                        return closedLandStateName;
                    }
                    return landStateName;
                case VisualState.Dodge:
                    return dodgeStateName;
                case VisualState.Parry:
                    return parryStateName;
                case VisualState.Change:
                    return changeStateName;
                case VisualState.Attack:
                    return attackStateName;
                case VisualState.RecoilBoostSky:
                    return recoilBoostSkyStateName;
                default:
                    if (!isUmbrellaOpen && !string.IsNullOrEmpty(closedIdleStateName))
                    {
                        return closedIdleStateName;
                    }
                    return idleStateName;
            }
        }

        private string ResolveAnimatorStateName(VisualState state, bool isUmbrellaOpen)
        {
            var primary = GetAnimatorStateName(state, isUmbrellaOpen);
            if (state != VisualState.Parry &&
                state != VisualState.Change &&
                state != VisualState.Attack &&
                state != VisualState.RecoilBoostSky &&
                AnimatorHasState(primary))
            {
                return primary;
            }

            switch (state)
            {
                case VisualState.Run:
                    if (!isUmbrellaOpen)
                    {
                        if (AnimatorHasState("walk_close")) return "walk_close";
                        if (AnimatorHasState("run_close")) return "run_close";
                        if (AnimatorHasState("WalkClose")) return "WalkClose";
                        if (AnimatorHasState("RunClose")) return "RunClose";
                        if (AnimatorHasState("WalkClosed")) return "WalkClosed";
                        if (AnimatorHasState("RunClosed")) return "RunClosed";
                    }
                    if (AnimatorHasState("Walk")) return "Walk";
                    if (AnimatorHasState("walk")) return "walk";
                    if (AnimatorHasState("run")) return "run";
                    break;
                case VisualState.Jump:
                    if (!isUmbrellaOpen)
                    {
                        if (AnimatorHasState("jump_close")) return "jump_close";
                        if (AnimatorHasState("JumpClose")) return "JumpClose";
                        if (AnimatorHasState("JumpClosed")) return "JumpClosed";
                    }
                    if (AnimatorHasState("Jump")) return "Jump";
                    if (AnimatorHasState("jump")) return "jump";
                    break;
                case VisualState.Glide:
                    if (AnimatorHasState("Glide")) return "Glide";
                    if (AnimatorHasState("glide")) return "glide";
                    break;
                case VisualState.Land:
                    if (!isUmbrellaOpen)
                    {
                        if (AnimatorHasState("land_close")) return "land_close";
                        if (AnimatorHasState("LandClose")) return "LandClose";
                        if (AnimatorHasState("LandClosed")) return "LandClosed";
                    }
                    if (AnimatorHasState("Land")) return "Land";
                    if (AnimatorHasState("land")) return "land";
                    break;
                case VisualState.Dodge:
                    if (AnimatorHasState("Dodge")) return "Dodge";
                    if (AnimatorHasState("dodge")) return "dodge";
                    break;
                case VisualState.Parry:
                    if (IsDefaultStateName(primary, "parry") && AnimatorHasState("Parry")) return "Parry";
                    if (AnimatorHasState(primary)) return primary;
                    if (AnimatorHasState("Parry")) return "Parry";
                    if (AnimatorHasState("parry")) return "parry";
                    break;
                case VisualState.Change:
                    if (IsDefaultStateName(primary, "change") && AnimatorHasState("Change")) return "Change";
                    if (AnimatorHasState(primary)) return primary;
                    if (AnimatorHasState("Change")) return "Change";
                    if (AnimatorHasState("change")) return "change";
                    break;
                case VisualState.Attack:
                    if (IsDefaultStateName(primary, "attack") && AnimatorHasState("Attack")) return "Attack";
                    if (AnimatorHasState(primary)) return primary;
                    if (AnimatorHasState("Attack")) return "Attack";
                    if (AnimatorHasState("attack")) return "attack";
                    break;
                case VisualState.RecoilBoostSky:
                    if (AnimatorHasState(primary)) return primary;
                    if (AnimatorHasState("RecoilBoost_sky")) return "RecoilBoost_sky";
                    if (AnimatorHasState("RecoilBoostSky")) return "RecoilBoostSky";
                    if (AnimatorHasState("recoilBoost_sky")) return "recoilBoost_sky";
                    if (AnimatorHasState("recoilboost_sky")) return "recoilboost_sky";
                    if (AnimatorHasState("recoil_boost_sky")) return "recoil_boost_sky";
                    break;
                default:
                    if (!isUmbrellaOpen)
                    {
                        if (AnimatorHasState("idle_close")) return "idle_close";
                        if (AnimatorHasState("IdleClose")) return "IdleClose";
                        if (AnimatorHasState("IdleClosed")) return "IdleClosed";
                    }
                    if (AnimatorHasState("Idle")) return "Idle";
                    if (AnimatorHasState("idle")) return "idle";
                    break;
            }

            return primary;
        }

        private string BuildCurrentSpriteLog()
        {
            if (_resolvedFlipRenderers == null || _resolvedFlipRenderers.Length == 0)
            {
                return $"[PlayerSprite] frame={Time.frameCount} visualState={_currentState} animatorState={_currentAnimatorStateName ?? "(none)"} sprite=(no SpriteRenderer)";
            }

            var message = $"[PlayerSprite] frame={Time.frameCount} visualState={_currentState} animatorState={_currentAnimatorStateName ?? "(none)"} sprite=";
            var appendedAny = false;

            for (int i = 0; i < _resolvedFlipRenderers.Length; i++)
            {
                var renderer = _resolvedFlipRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (appendedAny)
                {
                    message += ", ";
                }

                var spriteName = renderer.sprite != null ? renderer.sprite.name : "(null)";
                message += $"{renderer.name}:{spriteName}";
                appendedAny = true;
            }

            return appendedAny
                ? message
                : $"[PlayerSprite] frame={Time.frameCount} visualState={_currentState} animatorState={_currentAnimatorStateName ?? "(none)"} sprite=(no active SpriteRenderer)";
        }

        private bool TryResolveCutsceneMovement(out bool isFacingRight)
        {
            if (_stateProvider is global::PlayerController playerController &&
                playerController.IsExternalMovementActive)
            {
                isFacingRight = playerController.IsFacingRight;
                TrackPreviousWorldPosition();
                return true;
            }

            return TryResolveTransformMovement(out isFacingRight);
        }

        private bool TryResolveTransformMovement(out bool isFacingRight)
        {
            Vector3 currentPosition = transform.position;
            float deltaX = currentPosition.x - _previousWorldPosition.x;
            isFacingRight = deltaX >= 0f;

            if (!_hasPreviousWorldPosition)
            {
                _hasPreviousWorldPosition = true;
                _previousWorldPosition = currentPosition;
                return false;
            }

            _previousWorldPosition = currentPosition;

            return ShouldUseTransformDeltaAsMovement() &&
                   Mathf.Abs(deltaX) > transformMovementThreshold;
        }

        private void TrackPreviousWorldPosition()
        {
            _hasPreviousWorldPosition = true;
            _previousWorldPosition = transform.position;
        }

        private bool ShouldUseTransformDeltaAsMovement()
        {
            if (!useTransformDeltaAsMovement)
            {
                return false;
            }

            if (_stateProvider is global::PlayerController playerController)
            {
                return playerController.IsExternalControlLocked ||
                       playerController.IsExternalMovementActive;
            }

            return false;
        }

        private static bool IsDefaultStateName(string stateName, string defaultStateName)
        {
            return string.Equals(stateName, defaultStateName, System.StringComparison.OrdinalIgnoreCase);
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
