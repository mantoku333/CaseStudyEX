using Unity.Cinemachine;
using UnityEngine;
using Player;
using System.Collections.Generic;

/// <summary>
/// Shifts the follow target slightly ahead of the player based on facing.
/// This avoids moving the screen composition itself, which can feel nauseating.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
public sealed class FollowCameraFacingBias : MonoBehaviour
{
    [SerializeField, Min(0f)] private float horizontalLookAheadOffset = 2.25f;
    [SerializeField] private bool recenterWhenIdle = true;
    [SerializeField] private float idleOffsetX = 0f;
    [SerializeField, Min(0f)] private float fallingLookAheadOffsetY = 3f;
    [SerializeField, Min(0f)] private float fallingVelocityThreshold = 0.01f;
    [SerializeField, Min(0f)] private float verticalOffsetSpeed = 18f;
    [SerializeField, Min(0f)] private float fallingDampingY = 0.25f;

    private CinemachineCamera followCamera;
    private CinemachinePositionComposer positionComposer;
    private CinemachineFollow directFollow;
    private IPlayerViewStateProvider facingProvider;
    private Rigidbody2D targetRigidbody;
    private Transform cachedTrackingTarget;
    private static readonly List<MonoBehaviour> BehaviourBuffer = new List<MonoBehaviour>();
    private float baseComposerOffsetY;
    private Vector3 baseComposerDamping;
    private Vector3 baseDirectFollowOffset;
    private Vector3 baseDirectFollowPositionDamping;

    private void Awake()
    {
        followCamera = GetComponent<CinemachineCamera>();
        positionComposer = GetComponent<CinemachinePositionComposer>();
        directFollow = GetComponent<CinemachineFollow>();

        baseComposerOffsetY = positionComposer != null ? positionComposer.TargetOffset.y : 0f;
        baseComposerDamping = positionComposer != null ? positionComposer.Damping : Vector3.zero;
        baseDirectFollowOffset = directFollow != null ? directFollow.FollowOffset : Vector3.zero;
        baseDirectFollowPositionDamping = directFollow != null
            ? directFollow.TrackerSettings.PositionDamping
            : Vector3.zero;
    }

    private void LateUpdate()
    {
        if ((positionComposer == null && directFollow == null) ||
            !TryResolveTargetState(out IPlayerViewStateProvider provider))
        {
            return;
        }

        float desiredOffsetX = idleOffsetX;
        if (provider.IsMoving)
        {
            desiredOffsetX = provider.IsFacingRight
                ? horizontalLookAheadOffset
                : -horizontalLookAheadOffset;
        }
        else if (!recenterWhenIdle)
        {
            desiredOffsetX = provider.IsFacingRight
                ? horizontalLookAheadOffset
                : -horizontalLookAheadOffset;
        }

        bool isFalling = IsFalling(provider);

        UpdatePositionComposer(desiredOffsetX, isFalling);
        UpdateDirectFollow(isFalling);
    }

    private void UpdatePositionComposer(float desiredOffsetX, bool isFalling)
    {
        if (positionComposer == null)
        {
            return;
        }

        Vector3 targetOffset = positionComposer.TargetOffset;
        float desiredOffsetY = isFalling
            ? baseComposerOffsetY - fallingLookAheadOffsetY
            : baseComposerOffsetY;
        float nextOffsetY = MoveVerticalOffset(targetOffset.y, desiredOffsetY);
        Vector3 nextDamping = GetDesiredComposerDamping(isFalling);

        if (Mathf.Approximately(targetOffset.x, desiredOffsetX) &&
            Mathf.Approximately(targetOffset.y, nextOffsetY) &&
            positionComposer.Damping == nextDamping)
        {
            return;
        }

        targetOffset.x = desiredOffsetX;
        targetOffset.y = nextOffsetY;
        positionComposer.TargetOffset = targetOffset;
        positionComposer.Damping = nextDamping;
    }

    private void UpdateDirectFollow(bool isFalling)
    {
        if (directFollow == null)
        {
            return;
        }

        Vector3 followOffset = directFollow.FollowOffset;
        float desiredOffsetY = isFalling
            ? baseDirectFollowOffset.y - fallingLookAheadOffsetY
            : baseDirectFollowOffset.y;
        float nextOffsetY = MoveVerticalOffset(followOffset.y, desiredOffsetY);
        Vector3 nextDamping = GetDesiredDirectFollowPositionDamping(isFalling);

        if (Mathf.Approximately(followOffset.y, nextOffsetY) &&
            directFollow.TrackerSettings.PositionDamping == nextDamping)
        {
            return;
        }

        followOffset.y = nextOffsetY;
        directFollow.FollowOffset = followOffset;

        var trackerSettings = directFollow.TrackerSettings;
        trackerSettings.PositionDamping = nextDamping;
        directFollow.TrackerSettings = trackerSettings;
    }

    private float MoveVerticalOffset(float currentOffsetY, float desiredOffsetY)
    {
        return verticalOffsetSpeed > 0f
            ? Mathf.MoveTowards(currentOffsetY, desiredOffsetY, verticalOffsetSpeed * Time.deltaTime)
            : desiredOffsetY;
    }

    private bool IsFalling(IPlayerViewStateProvider provider)
    {
        return
            targetRigidbody != null &&
            !provider.IsGrounded &&
            targetRigidbody.linearVelocity.y < -fallingVelocityThreshold;
    }

    private Vector3 GetDesiredComposerDamping(bool isFalling)
    {
        Vector3 desiredDamping = baseComposerDamping;
        if (isFalling)
        {
            desiredDamping.y = Mathf.Min(baseComposerDamping.y, fallingDampingY);
        }

        return desiredDamping;
    }

    private Vector3 GetDesiredDirectFollowPositionDamping(bool isFalling)
    {
        Vector3 desiredDamping = baseDirectFollowPositionDamping;
        if (isFalling)
        {
            desiredDamping.y = Mathf.Min(baseDirectFollowPositionDamping.y, fallingDampingY);
        }

        return desiredDamping;
    }

    private bool TryResolveTargetState(out IPlayerViewStateProvider provider)
    {
        Transform trackingTarget = followCamera != null
            ? followCamera.Target.TrackingTarget
            : null;

        if (trackingTarget == null)
        {
            ClearCachedTargetState();
            provider = null;
            return false;
        }

        if (cachedTrackingTarget != trackingTarget)
        {
            ClearCachedTargetState();
            cachedTrackingTarget = trackingTarget;
        }

        if (facingProvider == null)
        {
            facingProvider = ResolveFacingProvider(trackingTarget);
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = ResolveRigidbody(trackingTarget);
        }

        provider = facingProvider;
        return provider != null;
    }

    private void ClearCachedTargetState()
    {
        cachedTrackingTarget = null;
        facingProvider = null;
        targetRigidbody = null;
    }

    private static IPlayerViewStateProvider ResolveFacingProvider(Transform trackingTarget)
    {
        Transform current = trackingTarget;
        while (current != null)
        {
            BehaviourBuffer.Clear();
            current.GetComponents(BehaviourBuffer);
            for (int i = 0; i < BehaviourBuffer.Count; i++)
            {
                if (BehaviourBuffer[i] is IPlayerViewStateProvider stateProvider)
                {
                    return stateProvider;
                }
            }

            current = current.parent;
        }

        return null;
    }

    private static Rigidbody2D ResolveRigidbody(Transform trackingTarget)
    {
        Transform current = trackingTarget;
        while (current != null)
        {
            Rigidbody2D rigidbody2d = current.GetComponent<Rigidbody2D>();
            if (rigidbody2d != null)
            {
                return rigidbody2d;
            }

            current = current.parent;
        }

        return null;
    }
}
