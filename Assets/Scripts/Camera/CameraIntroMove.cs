using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class CameraIntroMove : MonoBehaviour
{
    [SerializeField] private CinemachineCamera introCamera;
    [SerializeField] private CinemachineCamera followCamera;
    [SerializeField] private Transform player;

    [SerializeField] private Vector3 endOffset = Vector3.zero;
    [SerializeField] private float moveDuration = 2.5f;
    [SerializeField] private float switchDelay = 0.1f;

    [Header("Follow Camera Offset")]
    [SerializeField] private float startFollowOffsetX = 3f;
    [SerializeField] private float centerFollowOffsetX = 0f;
    [SerializeField] private float followOffsetReturnSpeed = 2f;
    [SerializeField] private float returnDelay = 0.2f;

    private CinemachineFollow followComponent;
    private bool hasStartedMoving = false;
    private bool canReturnToCenter = false;

    private void Awake()
    {
        introCamera.Priority = 100;
        followCamera.Priority = 0;

        followComponent = followCamera.GetComponent<CinemachineFollow>();
    }

    private void Start()
    {
        if (followComponent != null)
        {
            Vector3 offset = followComponent.FollowOffset;
            offset.x = startFollowOffsetX;
            followComponent.FollowOffset = offset;
        }

        StartCoroutine(BeginIntro());
    }

    private void Update()
    {
        if (followComponent == null) return;

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

        if (canReturnToCenter)
        {
            Vector3 offset = followComponent.FollowOffset;
            offset.x = Mathf.Lerp(offset.x, centerFollowOffsetX, Time.deltaTime * followOffsetReturnSpeed);
            followComponent.FollowOffset = offset;
        }
    }

    private IEnumerator StartReturnToCenterAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        canReturnToCenter = true;
    }

    private IEnumerator BeginIntro()
    {
        Vector3 startPos = introCamera.transform.position;
        Vector3 targetPos = player.position + endOffset;
        targetPos.z = startPos.z;

        float startLens = introCamera.Lens.OrthographicSize;
        float targetLens = followCamera.Lens.OrthographicSize;

        float time = 0f;

        while (time < moveDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / moveDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            introCamera.transform.position = Vector3.Lerp(startPos, targetPos, easedT);

            var lens = introCamera.Lens;
            lens.OrthographicSize = Mathf.Lerp(startLens, targetLens, easedT);
            introCamera.Lens = lens;

            yield return null;
        }

        introCamera.transform.position = targetPos;

        var finalLens = introCamera.Lens;
        finalLens.OrthographicSize = targetLens;
        introCamera.Lens = finalLens;

        yield return new WaitForSeconds(switchDelay);

        introCamera.Priority = 0;
        followCamera.Priority = 100;
    }
}
