using System.Collections;
using UnityEngine;

public class StasisJumpController : MonoBehaviour
{
    [Header("Child References")]
    [SerializeField] private string jumpPlatformName = "JumpPlatform";
    [SerializeField] private string jumpDestinationName = "JumpDestination";

    [Header("Detection")]
    [SerializeField, Min(0.01f)] private float standingProbeHeight = 0.35f;
    [SerializeField, Min(0f)] private float standingProbeHorizontalPadding = 0.2f;
    [SerializeField, Min(0f)] private float standingProbeVerticalOffset = 0.03f;
    [SerializeField, Min(0f)] private float standingTopToleranceY = 0.2f;
    [SerializeField, Min(0f)] private float standingTopHorizontalInset = 0.05f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float prepWaitBeforeClose = 1f;
    [SerializeField, Min(0f)] private float prepWaitAfterClose = 1f;
    [SerializeField, Min(0.05f)] private float flightTime = 3f;
    [SerializeField, Range(0f, 1f)] private float openUmbrellaProgress = 0.6667f;

    [Header("Guided Glide")]
    [SerializeField, Min(0f)] private float guidedGlideHorizontalBrake = 60f;
    [SerializeField, Min(0f)] private float guidedGlideArrivalRadiusX = 0.25f;
    [SerializeField, Min(0.01f)] private float minGuidedGlideTime = 0.25f;
    [SerializeField, Min(0.01f)] private float guidedGlideMaxCorrectionTime = 1.25f;

    [Header("Launch Recovery")]
    [SerializeField, Min(0f)] private float blockedLaunchGraceSeconds = 0.35f;
    [SerializeField, Min(0f)] private float blockedLaunchSpeedThreshold = 0.5f;

    [Header("Landing")]
    [SerializeField, Min(0f)] private float landingRadius = 1f;
    [SerializeField, Min(0.1f)] private float maxSequenceSeconds = 10f;

    private Transform jumpPlatform;
    private Collider2D jumpPlatformCollider;
    private Transform jumpDestination;
    private Coroutine jumpRoutine;
    private PlayerController lockedPlayer;
    private bool warnedMissingReferences;

    private void Awake()
    {
        ResolveChildReferences();
    }

    private void OnEnable()
    {
        ResolveChildReferences();
    }

    private void OnDisable()
    {
        if (jumpRoutine != null)
        {
            StopCoroutine(jumpRoutine);
            jumpRoutine = null;
        }

        if (lockedPlayer != null)
        {
            lockedPlayer.SetExternalFacingLocked(false, true);
            lockedPlayer.SetExternalControlLocked(false);
            lockedPlayer = null;
        }
    }

    private void Update()
    {
        if (jumpRoutine != null)
        {
            return;
        }

        if (!HasRequiredReferences())
        {
            ResolveChildReferences();
            WarnMissingReferencesOnce();
            return;
        }

        if (!TryGetStandingPlayer(out var playerController, out var playerRigidbody, out var umbrellaController, out var playerCollider))
        {
            return;
        }

        if (HasPassedPlatformMiddle(playerCollider.bounds))
        {
            jumpRoutine = StartCoroutine(RunJumpSequence(playerController, playerRigidbody, umbrellaController));
        }
    }

    private IEnumerator RunJumpSequence(
        PlayerController playerController,
        Rigidbody2D playerRigidbody,
        UmbrellaController umbrellaController)
    {
        lockedPlayer = playerController;
        playerController.SetExternalControlLocked(true);
        playerController.SetExternalFacingLocked(true, true);

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = new Vector2(0f, playerRigidbody.linearVelocity.y);
        }

        yield return new WaitForSeconds(prepWaitBeforeClose);

        if (umbrellaController != null)
        {
            umbrellaController.CloseUmbrella();
        }

        yield return new WaitForSeconds(prepWaitAfterClose);

        if (playerRigidbody == null || jumpDestination == null)
        {
            RestorePlayerControl();
            yield break;
        }

        playerRigidbody.linearVelocity = CalculateLaunchVelocity(playerRigidbody.position, jumpDestination.position, playerRigidbody.gravityScale);

        float elapsed = 0f;
        bool umbrellaOpened = false;
        bool checkedBlockedLaunch = false;
        float openUmbrellaTime = flightTime * openUmbrellaProgress;

        while (elapsed < maxSequenceSeconds)
        {
            if (!checkedBlockedLaunch && elapsed >= blockedLaunchGraceSeconds)
            {
                checkedBlockedLaunch = true;
                if (IsBlockedNearPlatform(playerRigidbody))
                {
                    break;
                }
            }

            if (!umbrellaOpened && elapsed >= openUmbrellaTime)
            {
                if (umbrellaController != null)
                {
                    umbrellaController.OpenUmbrella();
                }

                umbrellaOpened = true;
            }

            if (umbrellaOpened)
            {
                ApplyGuidedGlideHorizontalVelocity(playerRigidbody, umbrellaController);
            }

            if (HasLandedNearDestination(playerController, playerRigidbody))
            {
                break;
            }

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        RestorePlayerControl();
    }

    private void RestorePlayerControl()
    {
        if (lockedPlayer != null)
        {
            lockedPlayer.SetExternalFacingLocked(false, true);
            lockedPlayer.SetExternalControlLocked(false);
            lockedPlayer = null;
        }

        jumpRoutine = null;
    }

    private Vector2 CalculateLaunchVelocity(Vector2 startPosition, Vector2 destinationPosition, float gravityScale)
    {
        float safeFlightTime = Mathf.Max(0.05f, flightTime);
        Vector2 displacement = destinationPosition - startPosition;
        float gravityY = Physics2D.gravity.y * gravityScale;

        return new Vector2(
            displacement.x / safeFlightTime,
            (displacement.y - (0.5f * gravityY * safeFlightTime * safeFlightTime)) / safeFlightTime);
    }

    private void ApplyGuidedGlideHorizontalVelocity(Rigidbody2D playerRigidbody, UmbrellaController umbrellaController)
    {
        if (playerRigidbody == null || umbrellaController == null || jumpDestination == null)
        {
            return;
        }

        float remainingX = jumpDestination.position.x - playerRigidbody.position.x;
        float targetVelocityX = 0f;

        if (Mathf.Abs(remainingX) > guidedGlideArrivalRadiusX)
        {
            float remainingTime = EstimateRemainingGuidedGlideTime(playerRigidbody, umbrellaController);
            targetVelocityX = remainingX / remainingTime;
        }

        Vector2 velocity = playerRigidbody.linearVelocity;
        velocity.x = Mathf.MoveTowards(
            velocity.x,
            targetVelocityX,
            guidedGlideHorizontalBrake * Time.fixedDeltaTime);
        playerRigidbody.linearVelocity = velocity;
    }

    private float EstimateRemainingGuidedGlideTime(Rigidbody2D playerRigidbody, UmbrellaController umbrellaController)
    {
        float verticalDistance = playerRigidbody.position.y - jumpDestination.position.y;
        float glideFallSpeed = umbrellaController != null ? Mathf.Abs(umbrellaController.GetFallSpeed()) : 0f;

        if (verticalDistance <= 0f || glideFallSpeed <= 0.001f)
        {
            return Mathf.Max(0.01f, minGuidedGlideTime);
        }

        float estimatedTime = Mathf.Max(minGuidedGlideTime, verticalDistance / glideFallSpeed);
        return Mathf.Min(guidedGlideMaxCorrectionTime, estimatedTime);
    }

    private bool HasLandedNearDestination(PlayerController playerController, Rigidbody2D playerRigidbody)
    {
        if (playerController == null || playerRigidbody == null || jumpDestination == null)
        {
            return false;
        }

        if (!playerController.IsGrounded)
        {
            return false;
        }

        return Vector2.Distance(playerRigidbody.position, jumpDestination.position) <= landingRadius;
    }

    private bool TryGetStandingPlayer(
        out PlayerController playerController,
        out Rigidbody2D playerRigidbody,
        out UmbrellaController umbrellaController,
        out Collider2D playerCollider)
    {
        playerController = null;
        playerRigidbody = null;
        umbrellaController = null;
        playerCollider = null;

        Bounds bounds = jumpPlatformCollider.bounds;
        Vector2 probeSize = new Vector2(
            bounds.size.x + standingProbeHorizontalPadding,
            standingProbeHeight);
        Vector2 probeCenter = new Vector2(
            bounds.center.x,
            bounds.max.y + standingProbeVerticalOffset + (probeSize.y * 0.5f));

        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            PlayerController candidatePlayer = hit.GetComponentInParent<PlayerController>();
            if (candidatePlayer == null ||
                candidatePlayer.IsExternalControlLocked ||
                !candidatePlayer.IsGrounded)
            {
                continue;
            }

            Collider2D candidateCollider = candidatePlayer.GetComponent<Collider2D>();
            if (candidateCollider == null ||
                !IsStandingOnPlatformTop(candidateCollider.bounds, jumpPlatformCollider.bounds))
            {
                continue;
            }

            Rigidbody2D candidateRigidbody = candidatePlayer.GetComponent<Rigidbody2D>();
            if (candidateRigidbody == null)
            {
                continue;
            }

            playerController = candidatePlayer;
            playerRigidbody = candidateRigidbody;
            umbrellaController = candidatePlayer.GetComponentInChildren<UmbrellaController>();
            playerCollider = candidateCollider;
            return true;
        }

        return false;
    }

    private bool HasPassedPlatformMiddle(Bounds playerBounds)
    {
        if (jumpPlatformCollider == null || jumpDestination == null)
        {
            return false;
        }

        float platformCenterX = jumpPlatformCollider.bounds.center.x;
        float destinationDirectionX = jumpDestination.position.x - platformCenterX;

        if (destinationDirectionX >= 0f)
        {
            return playerBounds.center.x >= platformCenterX;
        }

        return playerBounds.center.x <= platformCenterX;
    }

    private bool IsStandingOnPlatformTop(Bounds playerBounds, Bounds platformBounds)
    {
        float verticalGap = Mathf.Abs(playerBounds.min.y - platformBounds.max.y);
        if (verticalGap > standingTopToleranceY)
        {
            return false;
        }

        float minX = platformBounds.min.x + standingTopHorizontalInset;
        float maxX = platformBounds.max.x - standingTopHorizontalInset;
        if (minX > maxX)
        {
            minX = platformBounds.min.x;
            maxX = platformBounds.max.x;
        }

        float playerCenterX = playerBounds.center.x;
        return playerCenterX >= minX && playerCenterX <= maxX;
    }

    private bool IsBlockedNearPlatform(Rigidbody2D playerRigidbody)
    {
        if (playerRigidbody == null || jumpPlatformCollider == null)
        {
            return false;
        }

        if (playerRigidbody.linearVelocity.magnitude > blockedLaunchSpeedThreshold)
        {
            return false;
        }

        Bounds platformBounds = jumpPlatformCollider.bounds;
        platformBounds.Expand(new Vector3(
            standingTopHorizontalInset + 0.1f,
            standingTopToleranceY + 0.1f,
            0f));

        Collider2D playerCollider = playerRigidbody.GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            return platformBounds.Intersects(playerCollider.bounds);
        }

        return platformBounds.Contains(playerRigidbody.position);
    }

    private void ResolveChildReferences()
    {
        jumpPlatform = FindChildByName(transform, jumpPlatformName);
        jumpDestination = FindChildByName(transform, jumpDestinationName);
        jumpPlatformCollider = jumpPlatform != null ? jumpPlatform.GetComponent<Collider2D>() : null;
    }

    private bool HasRequiredReferences()
    {
        return jumpPlatform != null &&
               jumpPlatformCollider != null &&
               jumpDestination != null;
    }

    private void WarnMissingReferencesOnce()
    {
        if (warnedMissingReferences)
        {
            return;
        }

        if (jumpPlatform == null)
        {
            Debug.LogWarning($"StasisJumpController could not find child '{jumpPlatformName}'.", this);
        }
        else if (jumpPlatformCollider == null)
        {
            Debug.LogWarning($"StasisJumpController child '{jumpPlatformName}' needs a Collider2D.", this);
        }

        if (jumpDestination == null)
        {
            Debug.LogWarning($"StasisJumpController could not find child '{jumpDestinationName}'.", this);
        }

        warnedMissingReferences = true;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        ResolveChildReferences();
        if (jumpPlatformCollider == null)
        {
            return;
        }

        Bounds bounds = jumpPlatformCollider.bounds;
        Vector2 probeSize = new Vector2(
            bounds.size.x + standingProbeHorizontalPadding,
            standingProbeHeight);
        Vector2 probeCenter = new Vector2(
            bounds.center.x,
            bounds.max.y + standingProbeVerticalOffset + (probeSize.y * 0.5f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(probeCenter, probeSize);

        if (jumpDestination != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(jumpDestination.position, landingRadius);
        }
    }
}
