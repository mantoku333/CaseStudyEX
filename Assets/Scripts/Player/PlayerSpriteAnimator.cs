using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// Sprite based player visual controller.
    /// Keeps Spine workflow untouched by living in a separate component.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpriteAnimator : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private PlayerPlatformerMockController mockController;
        [SerializeField] private PlayerController statsController;

        [Header("Facing")]
        [SerializeField] private bool syncFacingFromController = true;
        [SerializeField] private bool autoCollectFlipRenderers = true;
        [SerializeField] private SpriteRenderer[] flipRenderers;

        [Header("Animator Mode")]
        [SerializeField] private bool useAnimator = true;
        [SerializeField] private Animator animator;
        [SerializeField] private int animatorLayer = 0;
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string runStateName = "run";
        [SerializeField] private string jumpStateName = "jump";
        [SerializeField] private string dodgeStateName = "dodge";

        [Header("Sprite Sequence Mode")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] idleSprites;
        [SerializeField] private Sprite[] runSprites;
        [SerializeField] private Sprite[] jumpSprites;
        [SerializeField] private Sprite[] dodgeSprites;
        [SerializeField, Min(1f)] private float idleFps = 8f;
        [SerializeField, Min(1f)] private float runFps = 12f;
        [SerializeField, Min(1f)] private float jumpFps = 8f;
        [SerializeField, Min(1f)] private float dodgeFps = 16f;
        [SerializeField] private bool loopJumpSprites = true;
        [SerializeField] private bool loopDodgeSprites = false;

        private enum VisualState
        {
            Idle,
            Run,
            Jump,
            Dodge
        }

        private static readonly SpriteRenderer[] EmptyRenderers = new SpriteRenderer[0];

        private VisualState _currentState = (VisualState)(-1);
        private Sprite[] _activeSprites;
        private float _activeFps;
        private int _frameIndex;
        private float _frameTimer;
        private bool _warnedNoController;
        private bool _warnedNoRenderer;
        private SpriteRenderer[] _resolvedFlipRenderers = EmptyRenderers;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            _currentState = (VisualState)(-1);
            _frameIndex = 0;
            _frameTimer = 0f;
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

            if (syncFacingFromController)
            {
                ApplyFacing(isFacingRight);
            }

            var nextState = ResolveState(isGrounded, isMoving, isGliding, isDodging);
            if (_currentState != nextState)
            {
                SwitchState(nextState);
            }

            if (!IsAnimatorModeActive())
            {
                TickSpriteSequence();
            }
        }

        private void ResolveReferences()
        {
            if (mockController == null)
            {
                mockController = GetComponentInParent<PlayerPlatformerMockController>();
            }

            if (statsController == null)
            {
                statsController = GetComponentInParent<PlayerController>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = GetComponentInParent<Animator>();
                }
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
                if (targetRenderer == null)
                {
                    targetRenderer = GetComponentInParent<SpriteRenderer>();
                }
            }

            if (flipRenderers == null || flipRenderers.Length == 0)
            {
                if (autoCollectFlipRenderers && targetRenderer != null)
                {
                    _resolvedFlipRenderers = new[] { targetRenderer };
                }
                else
                {
                    _resolvedFlipRenderers = EmptyRenderers;
                }
            }
            else
            {
                _resolvedFlipRenderers = flipRenderers;
            }
        }

        private bool TryReadControllerState(out bool isGrounded, out bool isMoving, out bool isGliding, out bool isDodging, out bool isFacingRight)
        {
            if (mockController != null)
            {
                isGrounded = mockController.IsGrounded;
                isMoving = mockController.IsMoving;
                isGliding = mockController.IsGliding;
                isDodging = mockController.IsDodging;
                isFacingRight = mockController.IsFacingRight;
                return true;
            }

            if (statsController != null)
            {
                isGrounded = statsController.IsGrounded;
                isMoving = statsController.IsMoving;
                isGliding = statsController.IsGliding;
                isDodging = false;
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

        private static VisualState ResolveState(bool isGrounded, bool isMoving, bool isGliding, bool isDodging)
        {
            if (isDodging)
            {
                return VisualState.Dodge;
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

        private bool IsAnimatorModeActive()
        {
            return useAnimator &&
                   animator != null &&
                   animatorLayer >= 0 &&
                   animator.runtimeAnimatorController != null;
        }

        private void SwitchState(VisualState nextState)
        {
            _currentState = nextState;
            _frameIndex = 0;
            _frameTimer = 0f;

            if (IsAnimatorModeActive())
            {
                var stateName = ResolveAnimatorStateName(nextState);
                if (!string.IsNullOrEmpty(stateName))
                {
                    animator.Play(stateName, animatorLayer, 0f);
                }
                return;
            }

            _activeSprites = GetSprites(nextState);
            _activeFps = GetFps(nextState);
            ApplyCurrentSprite();
        }

        private string GetAnimatorStateName(VisualState state)
        {
            switch (state)
            {
                case VisualState.Run:
                    return runStateName;
                case VisualState.Jump:
                    return jumpStateName;
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

        private Sprite[] GetSprites(VisualState state)
        {
            switch (state)
            {
                case VisualState.Run:
                    return runSprites;
                case VisualState.Jump:
                    return jumpSprites;
                case VisualState.Dodge:
                    return dodgeSprites;
                default:
                    return idleSprites;
            }
        }

        private float GetFps(VisualState state)
        {
            switch (state)
            {
                case VisualState.Run:
                    return runFps;
                case VisualState.Jump:
                    return jumpFps;
                case VisualState.Dodge:
                    return dodgeFps;
                default:
                    return idleFps;
            }
        }

        private bool IsLoopState(VisualState state)
        {
            if (state == VisualState.Jump)
            {
                return loopJumpSprites;
            }

            if (state == VisualState.Dodge)
            {
                return loopDodgeSprites;
            }

            return true;
        }

        private void TickSpriteSequence()
        {
            if (targetRenderer == null)
            {
                if (!_warnedNoRenderer)
                {
                    Debug.LogWarning("PlayerSpriteAnimator: target SpriteRenderer is missing.", this);
                    _warnedNoRenderer = true;
                }
                return;
            }
            _warnedNoRenderer = false;

            if (_activeSprites == null || _activeSprites.Length == 0)
            {
                return;
            }

            if (_activeSprites.Length == 1 || _activeFps <= 0f)
            {
                ApplyCurrentSprite();
                return;
            }

            _frameTimer += Time.deltaTime * _activeFps;
            while (_frameTimer >= 1f)
            {
                _frameTimer -= 1f;

                if (!IsLoopState(_currentState) && _frameIndex >= _activeSprites.Length - 1)
                {
                    _frameIndex = _activeSprites.Length - 1;
                }
                else
                {
                    _frameIndex = (_frameIndex + 1) % _activeSprites.Length;
                }
            }

            ApplyCurrentSprite();
        }

        private void ApplyCurrentSprite()
        {
            if (targetRenderer == null || _activeSprites == null || _activeSprites.Length == 0)
            {
                return;
            }

            _frameIndex = Mathf.Clamp(_frameIndex, 0, _activeSprites.Length - 1);
            targetRenderer.sprite = _activeSprites[_frameIndex];
        }
    }
}
