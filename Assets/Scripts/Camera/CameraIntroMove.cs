using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraIntroMove : MonoBehaviour
{
    [SerializeField] private CinemachineCamera introCamera;
    [SerializeField] private CinemachineCamera followCamera;
    [SerializeField] private Transform player;
    [SerializeField] private bool autoPlayOnStart = true;

    [SerializeField] private Vector3 endOffset = Vector3.zero;
    [SerializeField] private float moveDuration = 2.5f;
    [SerializeField] private float switchDelay = 0.1f;
    [SerializeField] private bool switchToFollowCameraOnComplete = true;

    [Header("Manual Intro Start")]
    [SerializeField] private bool useCustomStartPose;
    [SerializeField] private Vector3 introStartPosition = new Vector3(0f, 0f, -30f);
    [SerializeField] private float introStartOrthographicSize = 12f;

    [Header("Player Setup")]
    [SerializeField] private bool placePlayerAtMarkerBeforeIntro;
    [SerializeField] private string playerStartMarkerId = "bed_right";
    [SerializeField] private bool lockPlayerControlDuringIntro = true;

    [Header("Follow Camera Offset")]
    [SerializeField] private bool manageFollowOffset = true;
    [SerializeField] private float startFollowOffsetX = 3f;
    [SerializeField] private float centerFollowOffsetX = 0f;
    [SerializeField] private float followOffsetReturnSpeed = 2f;
    [SerializeField] private float returnDelay = 0.2f;

    [Header("Fade")]
    [SerializeField] private bool fadeToBlackOnComplete;
    [SerializeField] private bool restoreIntroCameraPoseAfterFade = true;
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float blackHoldDuration = 0.05f;
    [SerializeField] private float fadeInDuration = 0.35f;

    [Header("Camera Data")]
    [SerializeField] private CameraData cameraData;

    private CinemachineFollow followComponent;
    private bool hasStartedMoving;
    private bool canReturnToCenter;
    private bool introCompleted;
    private bool isPlayingIntro;
    private bool followCameraPrepared;
    private Vector3 configuredIntroCameraPosition;
    private float configuredIntroCameraOrthographicSize;
    private PlayerController cachedPlayerController;

    private void Awake()
    {
        if (followCamera != null)
        {
            followComponent = followCamera.GetComponent<CinemachineFollow>();
        }

        if (player != null)
        {
            cachedPlayerController = player.GetComponent<PlayerController>();
            if (cachedPlayerController == null)
            {
                cachedPlayerController = player.GetComponentInParent<PlayerController>();
            }
        }

        if (introCamera != null)
        {
            configuredIntroCameraPosition = introCamera.transform.position;
            configuredIntroCameraOrthographicSize = introCamera.Lens.OrthographicSize;
        }

        if (autoPlayOnStart)
        {
            SetIntroCameraPriority();
        }
    }

    private void Start()
    {
        PrepareFollowCamera();
        ApplyCameraDataLens();

        if (autoPlayOnStart)
        {
            StartCoroutine(PlayIntroSequence());
        }
    }

    private void Update()
    {
        if (!manageFollowOffset || followComponent == null)
        {
            return;
        }

        if (!hasStartedMoving && Keyboard.current != null)
        {
            bool isMovingInput =
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.dKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed;

            if (isMovingInput)
            {
                hasStartedMoving = true;
                StartCoroutine(StartReturnToCenterAfterDelay());
            }
        }

        Vector3 offset = followComponent.FollowOffset;

        if (canReturnToCenter)
        {
            offset.x = Mathf.Lerp(offset.x, centerFollowOffsetX, Time.deltaTime * followOffsetReturnSpeed);
        }

        offset = ApplyCameraDataOffset(offset);

        if (offset != followComponent.FollowOffset)
        {
            followComponent.FollowOffset = offset;
        }

        ApplyCameraDataLens();
    }

    public IEnumerator PlayIntroSequence()
    {
        if (isPlayingIntro || introCompleted || introCamera == null || player == null)
        {
            yield break;
        }

        isPlayingIntro = true;

        try
        {
            PreparePlayerForIntro();
            PrepareFollowCamera();
            ApplyCameraDataLens();
            SetIntroCameraPriority();
            yield return BeginIntro();
            introCompleted = true;
        }
        finally
        {
            ReleasePlayerAfterIntro();
            isPlayingIntro = false;
        }
    }

    private void PreparePlayerForIntro()
    {
        if (placePlayerAtMarkerBeforeIntro)
        {
            ProloguePresentationRuntime.Instance.PlaceActor("iris", playerStartMarkerId);
        }

        if (!lockPlayerControlDuringIntro || cachedPlayerController == null)
        {
            return;
        }

        cachedPlayerController.SetExternalControlLocked(true);
    }

    private void ReleasePlayerAfterIntro()
    {
        if (!lockPlayerControlDuringIntro || cachedPlayerController == null)
        {
            return;
        }

        cachedPlayerController.SetExternalControlLocked(false);
    }

    private Vector3 ApplyCameraDataOffset(Vector3 offset)
    {
        if (cameraData == null)
        {
            return offset;
        }

        offset.y = cameraData.FollowOffsetY;
        return offset;
    }

    private void ApplyCameraDataLens()
    {
        if (cameraData == null || followCamera == null)
        {
            return;
        }

        LensSettings lens = followCamera.Lens;

        if (Mathf.Approximately(lens.OrthographicSize, cameraData.OrthographicSize))
        {
            return;
        }

        lens.OrthographicSize = cameraData.OrthographicSize;
        followCamera.Lens = lens;
    }

    private void PrepareFollowCamera()
    {
        if (followCameraPrepared || followComponent == null || !manageFollowOffset)
        {
            return;
        }

        Vector3 offset = followComponent.FollowOffset;
        offset.x = startFollowOffsetX;
        offset = ApplyCameraDataOffset(offset);
        followComponent.FollowOffset = offset;
        followCameraPrepared = true;
    }

    private void SetIntroCameraPriority()
    {
        if (introCamera == null)
        {
            return;
        }

        int followPriority = followCamera != null ? followCamera.Priority.Value : 0;
        int desiredPriority = Mathf.Max(100, followPriority + 10);
        introCamera.Priority.Enabled = true;
        introCamera.Priority.Value = desiredPriority;

        if (followCamera != null)
        {
            followCamera.Priority.Enabled = true;
        }
    }

    private IEnumerator StartReturnToCenterAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        canReturnToCenter = true;
    }

    private IEnumerator BeginIntro()
    {
        Vector3 restorePosition = configuredIntroCameraPosition;
        float restoreLens = configuredIntroCameraOrthographicSize;

        if (useCustomStartPose)
        {
            introCamera.transform.position = introStartPosition;

            LensSettings initialLens = introCamera.Lens;
            initialLens.OrthographicSize = introStartOrthographicSize;
            introCamera.Lens = initialLens;
        }

        Vector3 startPos = introCamera.transform.position;
        Vector3 targetPos = player.position + endOffset;
        targetPos.z = startPos.z;

        float startLens = introCamera.Lens.OrthographicSize;
        float targetLens = cameraData != null
            ? cameraData.OrthographicSize
            : followCamera != null
                ? followCamera.Lens.OrthographicSize
                : introCamera.Lens.OrthographicSize;

        float time = 0f;

        while (time < moveDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / moveDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            introCamera.transform.position = Vector3.Lerp(startPos, targetPos, easedT);

            LensSettings lens = introCamera.Lens;
            lens.OrthographicSize = Mathf.Lerp(startLens, targetLens, easedT);
            introCamera.Lens = lens;

            yield return null;
        }

        introCamera.transform.position = targetPos;

        LensSettings finalLens = introCamera.Lens;
        finalLens.OrthographicSize = targetLens;
        introCamera.Lens = finalLens;

        yield return new WaitForSeconds(switchDelay);

        if (fadeToBlackOnComplete)
        {
            StoryOverlayFader.Instance.SetImmediate(0f, Color.black);
            yield return StoryOverlayFader.Instance.FadeTo(1f, fadeOutDuration, Color.black);

            if (blackHoldDuration > 0f)
            {
                yield return WaitForUnscaledSeconds(blackHoldDuration);
            }
        }

        if (fadeToBlackOnComplete && restoreIntroCameraPoseAfterFade)
        {
            introCamera.transform.position = restorePosition;

            LensSettings restoredLens = introCamera.Lens;
            restoredLens.OrthographicSize = restoreLens;
            introCamera.Lens = restoredLens;
        }

        if (switchToFollowCameraOnComplete && followCamera != null)
        {
            introCamera.Priority.Value = 0;
            followCamera.Priority.Value = 100;
        }

        if (fadeToBlackOnComplete)
        {
            yield return StoryOverlayFader.Instance.FadeTo(0f, fadeInDuration, Color.black);
        }
    }

    private static IEnumerator WaitForUnscaledSeconds(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
