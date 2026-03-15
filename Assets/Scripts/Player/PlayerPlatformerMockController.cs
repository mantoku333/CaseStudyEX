using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.Player
{
    public enum FacingDirection
    {
        Left = -1,
        Right = 1
    }

    public enum UmbrellaState
    {
        Closed = 0,
        Opened = 1
    }

    /// <summary>
    /// Minimal 2D platformer controller for metroidvania mock:
    /// horizontal movement (A/D) and jump (Space) via Input System.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerPlatformerMockController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float jumpImpulse = 10f;
        [SerializeField, Min(0f)] private float glideMoveSpeedMultiplier = 1.15f;

        [Header("Physics")]
        [SerializeField] private bool freezeRotationZ = true;
        [SerializeField] private bool applyNoFrictionMaterial = true;
        [SerializeField, Min(0f)] private float baseGravityScale = 2.2f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.1f;

        [Header("Landing Squash")]
        [SerializeField] private bool enableLandingSquash = true;
        [SerializeField] private Transform squashTarget;
        [SerializeField, Min(0f)] private float landingVelocityThreshold = 4f;
        [SerializeField, Range(0.5f, 1f)] private float landingScaleY = 0.82f;
        [SerializeField, Range(1f, 1.5f)] private float landingScaleX = 1.12f;
        [SerializeField, Min(0.01f)] private float squashDuration = 0.06f;
        [SerializeField, Min(0.01f)] private float recoverDuration = 0.12f;
        [SerializeField] private Ease squashEase = Ease.OutQuad;
        [SerializeField] private Ease recoverEase = Ease.OutBack;

        [Header("Jump Squash")]
        [SerializeField] private bool enableJumpStartSquash = true;
        [SerializeField, Range(0.5f, 1f)] private float jumpStartScaleY = 0.88f;
        [SerializeField, Range(1f, 1.5f)] private float jumpStartScaleX = 1.1f;
        [SerializeField, Min(0.01f)] private float jumpStartSquashDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float jumpStartRecoverDuration = 0.1f;
        [SerializeField] private Ease jumpStartSquashEase = Ease.OutQuad;
        [SerializeField] private Ease jumpStartRecoverEase = Ease.OutBack;

        [Header("Umbrella")]
        [SerializeField] private UmbrellaState umbrellaState = UmbrellaState.Closed;
        [SerializeField] private GameObject umbrellaOpenedVisual;
        [SerializeField] private bool mirrorUmbrellaLocalPositionXWithFacing = true;

        [Header("Umbrella Glide")]
        [SerializeField] private bool enableUmbrellaGlide = true;
        [SerializeField, Min(0f)] private float glideGravityScale = 0.35f;
        [SerializeField, Min(0f)] private float glideMaxFallSpeed = 3.5f;
        [SerializeField] private float glideStartVerticalVelocity = -0.1f;

        [Header("Facing")]
        [SerializeField] private FacingDirection facingDirection = FacingDirection.Right;
        [SerializeField, Range(0f, 1f)] private float facingInputThreshold = 0.01f;
        [SerializeField] private SpriteRenderer[] facingSpriteRenderers;

        private Rigidbody2D _rigidbody2D;
        private Collider2D _collider2D;
        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _umbrellaToggleAction;
        private bool _ownsUmbrellaToggleAction;

        private float _moveInputX;
        private bool _isGrounded;
        private bool _wasGrounded;
        private bool _groundStateInitialized;
        private bool _jumpQueued;
        private float _minAirborneVelocityY;
        private PhysicsMaterial2D _runtimeNoFrictionMaterial;
        private Sequence _squashSequence;
        private Vector3 _initialSquashScale;
        private float _umbrellaRightLocalX;
        private float _umbrellaLeftLocalX;
        private float _umbrellaLocalY;
        private float _umbrellaLocalZ;
        private bool _hasUmbrellaFacingOffsets;
        private float _defaultGravityScale;

        public FacingDirection CurrentFacingDirection => facingDirection;
        public bool IsFacingRight => facingDirection == FacingDirection.Right;
        public UmbrellaState CurrentUmbrellaState => umbrellaState;
        public bool IsUmbrellaOpen => umbrellaState == UmbrellaState.Opened;
        public bool IsGliding => ShouldGlide();
        public event Action<UmbrellaState> UmbrellaStateChanged;

        /// <summary>
        /// Spineアニメーター用：接地しているか
        /// </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>
        /// Spineアニメーター用：移動入力があるか
        /// </summary>
        public bool IsMoving => Mathf.Abs(_moveInputX) > facingInputThreshold;

        /// <summary>
        /// Spineアニメーター用：垂直方向の速度
        /// </summary>
        public float VerticalVelocity => _rigidbody2D != null ? _rigidbody2D.linearVelocity.y : 0f;

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _collider2D = GetComponent<Collider2D>();
            _playerInput = GetComponent<PlayerInput>();
            _defaultGravityScale = baseGravityScale;
            _rigidbody2D.gravityScale = _defaultGravityScale;
            squashTarget = squashTarget == null ? transform : squashTarget;
            _initialSquashScale = squashTarget.localScale;
            if (facingSpriteRenderers == null || facingSpriteRenderers.Length == 0)
            {
                facingSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
            CacheUmbrellaFacingOffsets();

            if (freezeRotationZ)
            {
                _rigidbody2D.constraints |= RigidbodyConstraints2D.FreezeRotation;
            }

            if (applyNoFrictionMaterial && _collider2D != null)
            {
                ApplyNoFrictionMaterial();
            }
        }

        private void OnEnable()
        {
            _groundStateInitialized = false;
            _wasGrounded = false;
            _minAirborneVelocityY = 0f;
            _rigidbody2D.gravityScale = _defaultGravityScale;
            SetUmbrellaState(UmbrellaState.Closed, true);
            ApplyFacingVisual();
            BindActions();
        }

        private void OnDisable()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= OnJumpPerformed;
            }

            if (_umbrellaToggleAction != null)
            {
                _umbrellaToggleAction.performed -= OnUmbrellaTogglePerformed;
                if (_ownsUmbrellaToggleAction)
                {
                    _umbrellaToggleAction.Disable();
                }
            }

            _squashSequence?.Kill();
            if (squashTarget != null)
            {
                squashTarget.localScale = _initialSquashScale;
            }

            _moveInputX = 0f;
            _jumpQueued = false;
            _rigidbody2D.gravityScale = _defaultGravityScale;
        }

        private void OnDestroy()
        {
            if (_ownsUmbrellaToggleAction && _umbrellaToggleAction != null)
            {
                _umbrellaToggleAction.Dispose();
                _umbrellaToggleAction = null;
            }
        }

        private void Update()
        {
            if (_moveAction != null)
            {
                var move = _moveAction.ReadValue<Vector2>();
                _moveInputX = Mathf.Clamp(move.x, -1f, 1f);
                UpdateFacingFromMoveInput();
            }

            UpdateGroundState();
        }

        private void FixedUpdate()
        {
            var velocity = _rigidbody2D.linearVelocity;
            var currentMoveSpeed = moveSpeed;
            if (IsGliding)
            {
                currentMoveSpeed *= glideMoveSpeedMultiplier;
            }

            velocity.x = _moveInputX * currentMoveSpeed;
            _rigidbody2D.linearVelocity = velocity;

            if (_jumpQueued && _isGrounded)
            {
                if (enableJumpStartSquash)
                {
                    PlaySquash(
                        jumpStartScaleX,
                        jumpStartScaleY,
                        jumpStartSquashDuration,
                        jumpStartRecoverDuration,
                        jumpStartSquashEase,
                        jumpStartRecoverEase);
                }

                velocity = _rigidbody2D.linearVelocity;
                velocity.y = 0f;
                _rigidbody2D.linearVelocity = velocity;
                _rigidbody2D.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
            }

            ApplyGlidePhysics();
            _jumpQueued = false;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _jumpQueued = true;
            }
        }

        private void OnUmbrellaTogglePerformed(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                ToggleUmbrella();
            }
        }

        private void UpdateGroundState()
        {
            var isGroundedNow = CheckGrounded();
            var velocityY = _rigidbody2D.linearVelocity.y;

            if (!isGroundedNow)
            {
                if (_wasGrounded)
                {
                    _minAirborneVelocityY = velocityY;
                }
                else
                {
                    _minAirborneVelocityY = Mathf.Min(_minAirborneVelocityY, velocityY);
                }
            }

            if (_groundStateInitialized && !_wasGrounded && isGroundedNow)
            {
                if (enableLandingSquash && _minAirborneVelocityY <= -landingVelocityThreshold)
                {
                    PlayLandingSquash();
                }
            }

            if (isGroundedNow)
            {
                _minAirborneVelocityY = 0f;
            }

            _isGrounded = isGroundedNow;
            _wasGrounded = isGroundedNow;
            _groundStateInitialized = true;
        }

        private void PlayLandingSquash()
        {
            PlaySquash(
                landingScaleX,
                landingScaleY,
                squashDuration,
                recoverDuration,
                squashEase,
                recoverEase);
        }

        private void PlaySquash(
            float scaleXMultiplier,
            float scaleYMultiplier,
            float squashTime,
            float recoverTime,
            Ease squashTweenEase,
            Ease recoverTweenEase)
        {
            if (squashTarget == null)
            {
                return;
            }

            _squashSequence?.Kill();
            squashTarget.localScale = _initialSquashScale;

            var squashScale = new Vector3(
                _initialSquashScale.x * scaleXMultiplier,
                _initialSquashScale.y * scaleYMultiplier,
                _initialSquashScale.z);

            _squashSequence = DOTween.Sequence();
            _squashSequence.Append(squashTarget.DOScale(squashScale, squashTime).SetEase(squashTweenEase));
            _squashSequence.Append(squashTarget.DOScale(_initialSquashScale, recoverTime).SetEase(recoverTweenEase));
            _squashSequence.SetLink(gameObject);
        }

        public void OpenUmbrella()
        {
            SetUmbrellaState(UmbrellaState.Opened);
        }

        public void CloseUmbrella()
        {
            SetUmbrellaState(UmbrellaState.Closed);
        }

        public void ToggleUmbrella()
        {
            SetUmbrellaState(IsUmbrellaOpen ? UmbrellaState.Closed : UmbrellaState.Opened);
        }

        public void SetUmbrellaState(UmbrellaState newState)
        {
            SetUmbrellaState(newState, false);
        }

        private void SetUmbrellaState(UmbrellaState newState, bool forceNotify)
        {
            if (!forceNotify && umbrellaState == newState)
            {
                return;
            }

            umbrellaState = newState;
            ApplyUmbrellaVisual();
            UmbrellaStateChanged?.Invoke(umbrellaState);
        }

        private void UpdateFacingFromMoveInput()
        {
            if (_moveInputX > facingInputThreshold)
            {
                SetFacingDirection(FacingDirection.Right);
                return;
            }

            if (_moveInputX < -facingInputThreshold)
            {
                SetFacingDirection(FacingDirection.Left);
            }
        }

        private void SetFacingDirection(FacingDirection newDirection)
        {
            if (facingDirection == newDirection)
            {
                return;
            }

            facingDirection = newDirection;
            ApplyFacingVisual();
        }

        private void BindActions()
        {
            if (_playerInput == null || _playerInput.actions == null)
            {
                Debug.LogWarning("PlayerInput or Actions asset is missing.", this);
                return;
            }

            _moveAction = _playerInput.actions.FindAction("Move", false);
            _jumpAction = _playerInput.actions.FindAction("Jump", false);

            if (_moveAction == null || _jumpAction == null)
            {
                Debug.LogWarning("Input Actions 'Move' and/or 'Jump' not found.", this);
                return;
            }

            _jumpAction.performed -= OnJumpPerformed;
            _jumpAction.performed += OnJumpPerformed;

            BindUmbrellaToggleAction();
        }

        private void BindUmbrellaToggleAction()
        {
            var umbrellaActionFromAsset = _playerInput.actions.FindAction("UmbrellaToggle", false);
            if (umbrellaActionFromAsset != null)
            {
                _umbrellaToggleAction = umbrellaActionFromAsset;
                _ownsUmbrellaToggleAction = false;
            }
            else
            {
                if (_umbrellaToggleAction == null || !_ownsUmbrellaToggleAction)
                {
                    _umbrellaToggleAction = new InputAction(
                        name: "UmbrellaToggle_MouseRight",
                        type: InputActionType.Button,
                        binding: "<Mouse>/rightButton");
                    _ownsUmbrellaToggleAction = true;
                }

                if (!_umbrellaToggleAction.enabled)
                {
                    _umbrellaToggleAction.Enable();
                }
            }

            _umbrellaToggleAction.performed -= OnUmbrellaTogglePerformed;
            _umbrellaToggleAction.performed += OnUmbrellaTogglePerformed;
        }

        private void ApplyUmbrellaVisual()
        {
            if (umbrellaOpenedVisual == null)
            {
                return;
            }

            umbrellaOpenedVisual.SetActive(IsUmbrellaOpen);
        }

        private void ApplyFacingVisual()
        {
            if (facingSpriteRenderers == null || facingSpriteRenderers.Length == 0)
            {
                return;
            }

            var shouldFlipX = facingDirection == FacingDirection.Left;
            for (var i = 0; i < facingSpriteRenderers.Length; i++)
            {
                var spriteRenderer = facingSpriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.flipX = shouldFlipX;
            }

            ApplyUmbrellaFacingOffset();
        }

        private void CacheUmbrellaFacingOffsets()
        {
            _hasUmbrellaFacingOffsets = false;
            if (umbrellaOpenedVisual == null)
            {
                return;
            }

            var localPos = umbrellaOpenedVisual.transform.localPosition;
            _umbrellaLocalY = localPos.y;
            _umbrellaLocalZ = localPos.z;

            if (facingDirection == FacingDirection.Right)
            {
                _umbrellaRightLocalX = localPos.x;
                _umbrellaLeftLocalX = -localPos.x;
            }
            else
            {
                _umbrellaLeftLocalX = localPos.x;
                _umbrellaRightLocalX = -localPos.x;
            }

            _hasUmbrellaFacingOffsets = true;
        }

        private void ApplyUmbrellaFacingOffset()
        {
            if (!mirrorUmbrellaLocalPositionXWithFacing || umbrellaOpenedVisual == null)
            {
                return;
            }

            if (!_hasUmbrellaFacingOffsets)
            {
                CacheUmbrellaFacingOffsets();
                if (!_hasUmbrellaFacingOffsets)
                {
                    return;
                }
            }

            var localPos = umbrellaOpenedVisual.transform.localPosition;
            localPos.x = IsFacingRight ? _umbrellaRightLocalX : _umbrellaLeftLocalX;
            localPos.y = _umbrellaLocalY;
            localPos.z = _umbrellaLocalZ;
            umbrellaOpenedVisual.transform.localPosition = localPos;
        }

        private bool ShouldGlide()
        {
            if (!enableUmbrellaGlide || !IsUmbrellaOpen || _isGrounded)
            {
                return false;
            }

            return _rigidbody2D.linearVelocity.y <= glideStartVerticalVelocity;
        }

        private void ApplyGlidePhysics()
        {
            if (ShouldGlide())
            {
                _rigidbody2D.gravityScale = glideGravityScale;

                var velocity = _rigidbody2D.linearVelocity;
                if (velocity.y < -glideMaxFallSpeed)
                {
                    velocity.y = -glideMaxFallSpeed;
                    _rigidbody2D.linearVelocity = velocity;
                }

                return;
            }

            if (_rigidbody2D.gravityScale != _defaultGravityScale)
            {
                _rigidbody2D.gravityScale = _defaultGravityScale;
            }
        }

        private bool CheckGrounded()
        {
            if (groundCheck != null)
            {
                return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayers) != null;
            }

            if (_collider2D == null)
            {
                return false;
            }

            var bounds = _collider2D.bounds;
            var center = new Vector2(bounds.center.x, bounds.min.y + (groundCheckRadius * 0.5f));
            var size = new Vector2(bounds.size.x * 0.9f, groundCheckRadius);

            return Physics2D.OverlapBox(center, size, 0f, groundLayers) != null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;

            if (groundCheck != null)
            {
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }
            else
            {
                var collider2D = GetComponent<Collider2D>();
                if (collider2D == null)
                {
                    return;
                }

                var bounds = collider2D.bounds;
                var center = new Vector2(bounds.center.x, bounds.min.y + (groundCheckRadius * 0.5f));
                var size = new Vector2(bounds.size.x * 0.9f, groundCheckRadius);
                Gizmos.DrawWireCube(center, size);
            }
        }

        private void ApplyNoFrictionMaterial()
        {
            if (_collider2D.sharedMaterial != null)
            {
                _runtimeNoFrictionMaterial = new PhysicsMaterial2D($"{_collider2D.sharedMaterial.name}_RuntimeNoFriction")
                {
                    friction = 0f,
                    bounciness = 0f
                };
                _collider2D.sharedMaterial = _runtimeNoFrictionMaterial;
                return;
            }

            _runtimeNoFrictionMaterial = new PhysicsMaterial2D("Runtime_Player_NoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
            _collider2D.sharedMaterial = _runtimeNoFrictionMaterial;
        }
    }
}
