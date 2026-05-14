using Unity.Cinemachine;
using UnityEngine;
using Player;

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

    private CinemachineCamera followCamera;
    private CinemachinePositionComposer positionComposer;
    private IPlayerViewStateProvider facingProvider;

    private void Awake()
    {
        followCamera = GetComponent<CinemachineCamera>();
        positionComposer = GetComponent<CinemachinePositionComposer>();
    }

    private void LateUpdate()
    {
        if (positionComposer == null || !TryResolveFacingProvider(out IPlayerViewStateProvider provider))
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

        Vector3 targetOffset = positionComposer.TargetOffset;
        if (Mathf.Approximately(targetOffset.x, desiredOffsetX))
        {
            return;
        }

        targetOffset.x = desiredOffsetX;
        positionComposer.TargetOffset = targetOffset;
    }

    private bool TryResolveFacingProvider(out IPlayerViewStateProvider provider)
    {
        if (facingProvider != null)
        {
            provider = facingProvider;
            return true;
        }

        provider = null;
        if (followCamera == null || followCamera.Target.TrackingTarget == null)
        {
            return false;
        }

        Transform current = followCamera.Target.TrackingTarget;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerViewStateProvider stateProvider)
                {
                    facingProvider = stateProvider;
                    provider = stateProvider;
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }
}
