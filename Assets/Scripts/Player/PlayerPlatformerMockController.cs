using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.Player
{
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

        [Header("Physics")]
        [SerializeField] private bool freezeRotationZ = true;
        [SerializeField] private bool applyNoFrictionMaterial = true;

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

        private Rigidbody2D _rigidbody2D;
        private Collider2D _collider2D;
        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _jumpAction;

        private float _moveInputX;
        private bool _isGrounded;
        private bool _wasGrounded;
        private bool _groundStateInitialized;
        private bool _jumpQueued;
        private float _minAirborneVelocityY;
        private PhysicsMaterial2D _runtimeNoFrictionMaterial;
        private Sequence _squashSequence;
        private Vector3 _initialSquashScale;

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _collider2D = GetComponent<Collider2D>();
            _playerInput = GetComponent<PlayerInput>();
            squashTarget = squashTarget == null ? transform : squashTarget;
            _initialSquashScale = squashTarget.localScale;

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
            BindActions();
        }

        private void OnDisable()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= OnJumpPerformed;
            }

            _squashSequence?.Kill();
            if (squashTarget != null)
            {
                squashTarget.localScale = _initialSquashScale;
            }

            _moveInputX = 0f;
            _jumpQueued = false;
        }

        private void Update()
        {
            if (_moveAction != null)
            {
                var move = _moveAction.ReadValue<Vector2>();
                _moveInputX = Mathf.Clamp(move.x, -1f, 1f);
            }

            UpdateGroundState();
        }

        private void FixedUpdate()
        {
            var velocity = _rigidbody2D.linearVelocity;
            velocity.x = _moveInputX * moveSpeed;
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

            _jumpQueued = false;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _jumpQueued = true;
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
