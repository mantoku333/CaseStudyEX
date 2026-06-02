using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAimArmController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private MonoBehaviour stateProviderSource;
        [SerializeField] private Transform playerRoot;

        [Header("Arm")]
        [SerializeField] private SpriteRenderer armRenderer;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private Vector3 rightFacingLocalPosition = new Vector3(0.71261406f, -0.094211996f, 0.0f);
        [SerializeField] private Vector3 leftFacingLocalPosition = new Vector3(-0.71261406f, -0.094211996f, 0.0f);
        [SerializeField] private bool mirrorRightPositionXForLeftFacing = true;
        [SerializeField] private bool syncSpriteFlipWithFacing = true;

        [Header("Aiming")]
        [SerializeField] private float rightFacingRotationOffsetDegrees = -90.0f;
        [SerializeField] private float leftFacingRotationOffsetDegrees = -90.0f;
        [SerializeField, Min(0.0001f)] private float minimumAimDistance = 0.01f;

        private IPlayerViewStateProvider stateProvider;

        private void Awake()
        {
            ResolveReferences();
            SetArmVisible(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            SetArmVisible(false);
        }

        private void LateUpdate()
        {
            if (!TryReadVisibleState(out bool isFacingRight))
            {
                SetArmVisible(false);
                return;
            }

            ApplyFacing(isFacingRight);

            if (!TryGetAimWorldPosition(out Vector3 aimWorldPosition))
            {
                SetArmVisible(false);
                return;
            }

            if (!RotateToward(aimWorldPosition, isFacingRight))
            {
                SetArmVisible(false);
                return;
            }

            SetArmVisible(true);
        }

        private void ResolveReferences()
        {
            if (aimPivot == null)
            {
                aimPivot = transform;
            }

            if (armRenderer == null)
            {
                armRenderer = GetComponent<SpriteRenderer>();
            }

            if (stateProviderSource == null)
            {
                IPlayerViewStateProvider providerInParent =
                    GetComponentInParent<IPlayerViewStateProvider>();
                if (providerInParent is MonoBehaviour providerBehaviour)
                {
                    stateProviderSource = providerBehaviour;
                }
            }

            stateProvider = stateProviderSource as IPlayerViewStateProvider;

            if (playerRoot == null && stateProviderSource != null)
            {
                playerRoot = stateProviderSource.transform;
            }
        }

        private bool TryReadVisibleState(out bool isFacingRight)
        {
            if (stateProvider == null)
            {
                ResolveReferences();
            }

            if (stateProvider == null)
            {
                isFacingRight = true;
                return false;
            }

            isFacingRight = stateProvider.IsFacingRight;
            return stateProvider.IsUmbrellaOpen && stateProvider.IsRecoilBoosting;
        }

        private void ApplyFacing(bool isFacingRight)
        {
            if (aimPivot != null)
            {
                aimPivot.localPosition = ResolveFacingLocalPosition(isFacingRight);
            }

            if (syncSpriteFlipWithFacing && armRenderer != null)
            {
                armRenderer.flipX = !isFacingRight;
            }
        }

        private Vector3 ResolveFacingLocalPosition(bool isFacingRight)
        {
            if (isFacingRight)
            {
                return rightFacingLocalPosition;
            }

            if (!mirrorRightPositionXForLeftFacing)
            {
                return leftFacingLocalPosition;
            }

            return new Vector3(
                -rightFacingLocalPosition.x,
                rightFacingLocalPosition.y,
                rightFacingLocalPosition.z);
        }

        private bool TryGetAimWorldPosition(out Vector3 aimWorldPosition)
        {
            Camera mainCamera = Camera.main;
            if (playerRoot == null || mainCamera == null)
            {
                aimWorldPosition = Vector3.zero;
                return false;
            }

            return GameCursorController.TryGetClampedAimWorldPosition(
                playerRoot,
                mainCamera,
                out aimWorldPosition);
        }

        private bool RotateToward(Vector3 aimWorldPosition, bool isFacingRight)
        {
            if (aimPivot == null)
            {
                return false;
            }

            Vector2 aimDirection = aimWorldPosition - aimPivot.position;
            if (aimDirection.sqrMagnitude < minimumAimDistance * minimumAimDistance)
            {
                return false;
            }

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            float offset = isFacingRight
                ? rightFacingRotationOffsetDegrees
                : leftFacingRotationOffsetDegrees;

            aimPivot.rotation = Quaternion.Euler(0.0f, 0.0f, angle + offset);
            return true;
        }

        private void SetArmVisible(bool visible)
        {
            if (armRenderer != null)
            {
                armRenderer.enabled = visible;
            }
        }
    }
}
