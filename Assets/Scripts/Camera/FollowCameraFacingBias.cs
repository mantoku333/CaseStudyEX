using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    private const string FollowCameraName = "CN_FollowCam";

    [SerializeField, Min(0f)] private float horizontalLookAheadOffset = 2.25f;
    [SerializeField] private bool recenterWhenIdle = true;
    [SerializeField] private float idleOffsetX = 0f;
    [SerializeField, Min(0f)] private float fallingLookAheadOffsetY = 3f;
    [SerializeField, Min(0f)] private float fallingVelocityThreshold = 0.01f;
    [SerializeField, Min(0.01f)] private float verticalOffsetSmoothTime = 0.4f;
    [SerializeField, Min(0f)] private float downwardViewReleaseDelay = 0.35f;

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
    private float currentDownwardCameraOffset;
    private float downwardCameraOffsetVelocity;
    private bool wasLookingDown;
    private float lastDescendingTime = float.NegativeInfinity;
    private Transform sourceTrackingTarget;
    private Transform lookAheadTrackingTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CinemachineCamera[] cameras =
            Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera != null &&
                camera.gameObject.name == FollowCameraName &&
                camera.GetComponent<FollowCameraFacingBias>() == null)
            {
                camera.gameObject.AddComponent<FollowCameraFacingBias>();
            }
        }
    }

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

    private void Update()
    {
        if (!EnsureLookAheadTrackingTarget())
        {
            return;
        }

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

        bool isDescendingNow = IsDescendingNow(provider);
        if (isDescendingNow)
        {
            lastDescendingTime = Time.time;
        }

        // Glide velocity oscillates around zero because of collision/ground correction.
        // Keep the downward view latched briefly instead of cancelling it every frame.
        bool isFalling = isDescendingNow ||
            (wasLookingDown && Time.time - lastDescendingTime <= downwardViewReleaseDelay);
        if (isFalling != wasLookingDown)
        {
            wasLookingDown = isFalling;
            float velocityY = targetRigidbody != null ? targetRigidbody.linearVelocity.y : 0f;
            Debug.Log(
                $"[FollowCameraFacingBias] Downward view {(isFalling ? "started" : "ended")}. " +
                $"umbrellaOpen={provider.IsUmbrellaOpen}, grounded={provider.IsGrounded}, velocityY={velocityY:0.0000}",
                this);
        }

        float desiredDownwardCameraOffset = isFalling ? fallingLookAheadOffsetY : 0f;
        currentDownwardCameraOffset = Mathf.SmoothDamp(
            currentDownwardCameraOffset,
            desiredDownwardCameraOffset,
            ref downwardCameraOffsetVelocity,
            verticalOffsetSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);

        lookAheadTrackingTarget.localPosition =
            Vector3.down * currentDownwardCameraOffset;

        UpdatePositionComposer(desiredOffsetX);
        RestoreDirectFollowOffset();
    }

    private bool EnsureLookAheadTrackingTarget()
    {
        if (followCamera == null)
        {
            return false;
        }

        if (lookAheadTrackingTarget != null &&
            followCamera.Target.TrackingTarget == lookAheadTrackingTarget)
        {
            return true;
        }

        Transform currentTarget = followCamera.Target.TrackingTarget;
        if (currentTarget == null)
        {
            return false;
        }

        sourceTrackingTarget = currentTarget;
        GameObject targetObject = new GameObject("FollowCameraDownwardLookAheadTarget");
        targetObject.hideFlags = HideFlags.HideAndDontSave;
        lookAheadTrackingTarget = targetObject.transform;
        lookAheadTrackingTarget.SetParent(sourceTrackingTarget, false);
        lookAheadTrackingTarget.localPosition = Vector3.zero;
        followCamera.Target.TrackingTarget = lookAheadTrackingTarget;
        ClearCachedTargetState();
        return true;
    }

    private void OnDestroy()
    {
        if (followCamera != null &&
            lookAheadTrackingTarget != null &&
            followCamera.Target.TrackingTarget == lookAheadTrackingTarget)
        {
            followCamera.Target.TrackingTarget = sourceTrackingTarget;
        }

        if (lookAheadTrackingTarget != null)
        {
            Destroy(lookAheadTrackingTarget.gameObject);
        }
    }

    private void UpdatePositionComposer(float desiredOffsetX)
    {
        if (positionComposer == null)
        {
            return;
        }

        Vector3 targetOffset = positionComposer.TargetOffset;

        if (Mathf.Approximately(targetOffset.x, desiredOffsetX) &&
            Mathf.Approximately(targetOffset.y, baseComposerOffsetY) &&
            positionComposer.Damping == baseComposerDamping)
        {
            return;
        }

        targetOffset.x = desiredOffsetX;
        targetOffset.y = baseComposerOffsetY;
        positionComposer.TargetOffset = targetOffset;
        positionComposer.Damping = baseComposerDamping;
    }

    private void RestoreDirectFollowOffset()
    {
        if (directFollow == null)
        {
            return;
        }

        Vector3 followOffset = directFollow.FollowOffset;

        if (Mathf.Approximately(followOffset.y, baseDirectFollowOffset.y) &&
            directFollow.TrackerSettings.PositionDamping == baseDirectFollowPositionDamping)
        {
            return;
        }

        followOffset.y = baseDirectFollowOffset.y;
        directFollow.FollowOffset = followOffset;

        var trackerSettings = directFollow.TrackerSettings;
        trackerSettings.PositionDamping = baseDirectFollowPositionDamping;
        directFollow.TrackerSettings = trackerSettings;
    }

    private bool IsDescendingNow(IPlayerViewStateProvider provider)
    {
        if (targetRigidbody == null)
        {
            return false;
        }

        float verticalVelocity = targetRigidbody.linearVelocity.y;

        // Glide descent is extremely slow (-0.001 in the current player prefab).
        // Do not gate it behind the ground state: that state can lag briefly while
        // leaving a platform.  The open umbrella plus negative velocity is enough.
        bool isDescendingWithUmbrella =
            provider.IsUmbrellaOpen && verticalVelocity < 0f;
        bool isOrdinaryFall =
            !provider.IsGrounded && verticalVelocity < -fallingVelocityThreshold;

        return isDescendingWithUmbrella || isOrdinaryFall;
    }

    private bool TryResolveTargetState(out IPlayerViewStateProvider provider)
    {
        Transform trackingTarget = sourceTrackingTarget;

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
