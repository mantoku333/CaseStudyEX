using System.Collections.Generic;
using Player;
using UnityEngine;

/// <summary>
/// 発射前の反動移動を、実際の初速・重力・減速に合わせて予測表示する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GunController))]
public sealed class RecoilTrajectoryPreview : MonoBehaviour
{
    private const string PreviewObjectName = "RecoilTrajectoryPreviewVisual";
    private const string GroundLayerName = "Ground";
    private const float RecoilLinearDamping = 2.0f;
    private const float CollisionSkin = 0.03f;

    [Header("軌道予測")]
    [SerializeField, Min(0.1f)] private float predictionSeconds = 1.25f;
    [SerializeField, Range(8, 96)] private int maxPointCount = 64;
    [SerializeField, Min(0.005f)] private float lineWidth = 0.07f;
    [SerializeField, Min(0.02f)] private float arrowSize = 0.28f;

    [Header("点線デザイン")]
    [Tooltip("プレイヤー中心から軌道線の描画を開始するまでの距離")]
    [SerializeField, Range(0f, 3f)] private float startOffset = 0.9f;
    [Tooltip("点線1本あたりの長さ")]
    [SerializeField, Range(0.02f, 1f)] private float dashLength = 0.18f;
    [Tooltip("点線同士の空白の長さ")]
    [SerializeField, Range(0.02f, 1f)] private float dashGap = 0.12f;
    [SerializeField] private Color trajectoryColor = new Color(0.75f, 0.95f, 1.0f, 0.72f);

    private readonly List<Vector3> trajectoryPoints = new List<Vector3>(64);
    private readonly List<Vector3> displayPoints = new List<Vector3>(64);
    private readonly List<Vector3> currentDashPoints = new List<Vector3>(8);
    private readonly List<LineRenderer> dashRenderers = new List<LineRenderer>(32);

    private GunController gunController;
    private PlayerController playerController;
    private PlayerAbilityController abilityController;
    private UmbrellaController umbrellaController;
    private Rigidbody2D playerRigidbody;
    private Collider2D playerCollider;
    private Transform dashRoot;
    private LineRenderer arrowLine;
    private Material previewMaterial;
    private LayerMask solidLayerMask;
    private int activeDashCount;

    public void Initialize(GunController gun)
    {
        gunController = gun;
        ResolveReferences();
        EnsureRenderers();
        SetVisible(false);
    }

    private void Awake()
    {
        gunController = GetComponent<GunController>();
        ResolveReferences();
        EnsureRenderers();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (!ShouldShowPreview() ||
            !playerController.TryGetRecoilDirectionForPreview(out Vector2 shotDirection))
        {
            SetVisible(false);
            return;
        }

        Vector2 launchVelocity = gunController.GetCurrentRecoilLaunchVelocity(shotDirection);
        if (launchVelocity.sqrMagnitude <= 0.0001f)
        {
            SetVisible(false);
            return;
        }

        BuildTrajectory(launchVelocity);
        DrawTrajectory();
    }

    private bool ShouldShowPreview()
    {
        if (gunController == null ||
            playerController == null ||
            abilityController == null ||
            umbrellaController == null ||
            playerRigidbody == null ||
            playerCollider == null)
        {
            ResolveReferences();
        }

        return gunController != null &&
            playerController != null &&
            abilityController != null &&
            umbrellaController != null &&
            playerRigidbody != null &&
            playerCollider != null &&
            Time.timeScale > 0.0f &&
            !playerController.IsExternalControlLocked &&
            !playerController.IsGrounded &&
            abilityController.GetCanGunRecoil() &&
            umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open &&
            !gunController.GetRecoiling() &&
            gunController.CurrentCoolTime <= 0.0f;
    }

    private void ResolveReferences()
    {
        Transform root = transform.root;
        playerController = root.GetComponentInChildren<PlayerController>();
        abilityController = root.GetComponentInChildren<PlayerAbilityController>();
        umbrellaController = root.GetComponentInChildren<UmbrellaController>();
        playerRigidbody = root.GetComponentInChildren<Rigidbody2D>();
        playerCollider = playerRigidbody != null
            ? playerRigidbody.GetComponent<Collider2D>()
            : null;
        solidLayerMask = LayerMask.GetMask(GroundLayerName);
    }

    private void EnsureRenderers()
    {
        if (dashRoot != null && arrowLine != null)
        {
            return;
        }

        Transform existing = transform.Find(PreviewObjectName);
        GameObject previewObject;
        if (existing != null)
        {
            previewObject = existing.gameObject;
        }
        else
        {
            previewObject = new GameObject(PreviewObjectName);
            previewObject.transform.SetParent(transform, false);
        }

        Transform oldTrajectoryLine = previewObject.transform.Find("TrajectoryLine");
        if (oldTrajectoryLine != null)
        {
            oldTrajectoryLine.gameObject.SetActive(false);
        }

        dashRoot = GetOrCreateChild(previewObject.transform, "TrajectoryDashes");
        arrowLine = GetOrCreateLineRenderer(previewObject.transform, "ArrowLine");

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            previewMaterial = new Material(shader)
            {
                name = "RecoilTrajectoryPreviewMaterial"
            };
            arrowLine.sharedMaterial = previewMaterial;
        }

        ConfigureRenderer(arrowLine, lineWidth * 1.35f);
        arrowLine.positionCount = 3;
    }

    private static Transform GetOrCreateChild(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static LineRenderer GetOrCreateLineRenderer(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        GameObject lineObject;
        if (existing != null)
        {
            lineObject = existing.gameObject;
        }
        else
        {
            lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(parent, false);
        }

        LineRenderer renderer = lineObject.GetComponent<LineRenderer>();
        return renderer != null ? renderer : lineObject.AddComponent<LineRenderer>();
    }

    private void ConfigureRenderer(LineRenderer renderer, float width)
    {
        renderer.useWorldSpace = true;
        renderer.loop = false;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.startColor = trajectoryColor;
        renderer.endColor = trajectoryColor;
        renderer.numCapVertices = 2;
        renderer.numCornerVertices = 2;
        renderer.textureMode = LineTextureMode.Stretch;

        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer != null)
        {
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + 10;
        }
    }

    private void BuildTrajectory(Vector2 launchVelocity)
    {
        trajectoryPoints.Clear();

        float stepSeconds = Mathf.Max(0.001f, Time.fixedDeltaTime);
        int stepCount = Mathf.Clamp(
            Mathf.CeilToInt(predictionSeconds / stepSeconds),
            2,
            maxPointCount - 1);
        Vector2 position = GetColliderWorldCenter();
        Vector2 velocity = launchVelocity;
        float elapsed = 0.0f;
        trajectoryPoints.Add(position);

        for (int i = 0; i < stepCount; i++)
        {
            bool recoilActive = elapsed < gunController.GetRecoilDuration();
            velocity += Physics2D.gravity * playerRigidbody.gravityScale * stepSeconds;

            if (recoilActive)
            {
                velocity /= 1.0f + RecoilLinearDamping * stepSeconds;
            }
            else
            {
                velocity.x *= 0.95f;
                float maxFallVelocity = -Mathf.Abs(umbrellaController.GetFallSpeed());
                velocity.y = Mathf.Max(velocity.y, maxFallVelocity);
            }

            Vector2 desiredDelta = velocity * stepSeconds;
            if (TryFindCollision(position, desiredDelta, out Vector2 collisionPosition))
            {
                trajectoryPoints.Add(collisionPosition);
                break;
            }

            position += desiredDelta;
            trajectoryPoints.Add(position);
            elapsed += stepSeconds;
        }
    }

    private bool TryFindCollision(
        Vector2 position,
        Vector2 desiredDelta,
        out Vector2 collisionPosition)
    {
        float distance = desiredDelta.magnitude;
        if (distance <= 0.0001f || solidLayerMask.value == 0)
        {
            collisionPosition = position;
            return false;
        }

        // PolygonCollider2D には PolygonCast がないため、軌道表示だけは外接カプセルで予測する。
        // 実際の移動・衝突判定は PlayerCollisionMover2D の Collider2D.Cast でポリゴン形状のまま行う。
        Vector2 capsuleSize = playerCollider.bounds.size;
        RaycastHit2D hit = Physics2D.CapsuleCast(
            position,
            capsuleSize,
            CapsuleDirection2D.Vertical,
            playerRigidbody.rotation,
            desiredDelta / distance,
            distance + CollisionSkin,
            solidLayerMask);

        if (hit.collider == null)
        {
            collisionPosition = position;
            return false;
        }

        collisionPosition = position +
            desiredDelta.normalized * Mathf.Max(0.0f, hit.distance - CollisionSkin);
        return true;
    }

    private Vector2 GetColliderWorldCenter()
    {
        // The player's collider is a PolygonCollider2D whose points sit above and to the left of
        // the transform origin, with a zero offset. TransformPoint(offset) therefore lands on the
        // shape's bottom-right corner, which is where the preview used to start. The world bounds
        // give the actual centre, and they already follow the sprite's facing flip.
        return playerCollider.bounds.center;
    }

    private void DrawTrajectory()
    {
        BuildDisplayPoints();
        if (displayPoints.Count < 2)
        {
            SetVisible(false);
            return;
        }

        DrawDashSegments();

        Vector3 end = displayPoints[displayPoints.Count - 1];
        Vector2 tangent = end - displayPoints[displayPoints.Count - 2];
        if (tangent.sqrMagnitude <= 0.0001f)
        {
            tangent = Vector2.right;
        }
        tangent.Normalize();

        Vector2 back = -tangent * arrowSize;
        Vector2 leftWing = Quaternion.Euler(0.0f, 0.0f, 28.0f) * back;
        Vector2 rightWing = Quaternion.Euler(0.0f, 0.0f, -28.0f) * back;
        arrowLine.SetPosition(0, end + (Vector3)leftWing);
        arrowLine.SetPosition(1, end);
        arrowLine.SetPosition(2, end + (Vector3)rightWing);
        SetVisible(true);
    }

    private void DrawDashSegments()
    {
        activeDashCount = 0;
        currentDashPoints.Clear();
        bool drawingDash = true;
        float patternRemaining = Mathf.Max(0.01f, dashLength);

        for (int i = 1; i < displayPoints.Count; i++)
        {
            Vector3 cursor = displayPoints[i - 1];
            Vector3 segmentEnd = displayPoints[i];
            Vector3 segment = segmentEnd - cursor;
            float segmentRemaining = segment.magnitude;
            if (segmentRemaining <= 0.0001f)
            {
                continue;
            }

            Vector3 direction = segment / segmentRemaining;
            while (segmentRemaining > 0.0001f)
            {
                float step = Mathf.Min(segmentRemaining, patternRemaining);
                Vector3 next = cursor + direction * step;

                if (drawingDash)
                {
                    if (currentDashPoints.Count == 0)
                    {
                        currentDashPoints.Add(cursor);
                    }
                    currentDashPoints.Add(next);
                }

                cursor = next;
                segmentRemaining -= step;
                patternRemaining -= step;

                if (patternRemaining <= 0.0001f)
                {
                    if (drawingDash)
                    {
                        CommitCurrentDash();
                    }

                    drawingDash = !drawingDash;
                    patternRemaining = drawingDash
                        ? Mathf.Max(0.01f, dashLength)
                        : Mathf.Max(0.01f, dashGap);
                }
            }
        }

        if (drawingDash)
        {
            CommitCurrentDash();
        }

        for (int i = activeDashCount; i < dashRenderers.Count; i++)
        {
            dashRenderers[i].enabled = false;
        }
    }

    private void CommitCurrentDash()
    {
        if (currentDashPoints.Count < 2)
        {
            currentDashPoints.Clear();
            return;
        }

        LineRenderer renderer = GetDashRenderer(activeDashCount);
        renderer.positionCount = currentDashPoints.Count;
        renderer.SetPositions(currentDashPoints.ToArray());
        renderer.enabled = true;
        activeDashCount++;
        currentDashPoints.Clear();
    }

    private LineRenderer GetDashRenderer(int index)
    {
        while (dashRenderers.Count <= index)
        {
            LineRenderer renderer = GetOrCreateLineRenderer(
                dashRoot,
                $"Dash_{dashRenderers.Count:00}");
            ConfigureRenderer(renderer, lineWidth);
            if (previewMaterial != null)
            {
                renderer.sharedMaterial = previewMaterial;
            }
            dashRenderers.Add(renderer);
        }

        return dashRenderers[index];
    }

    private void BuildDisplayPoints()
    {
        displayPoints.Clear();
        if (trajectoryPoints.Count < 2)
        {
            return;
        }

        float remainingOffset = Mathf.Max(0.0f, startOffset);
        for (int i = 1; i < trajectoryPoints.Count; i++)
        {
            Vector3 segmentStart = trajectoryPoints[i - 1];
            Vector3 segmentEnd = trajectoryPoints[i];
            float segmentLength = Vector3.Distance(segmentStart, segmentEnd);

            if (remainingOffset >= segmentLength)
            {
                remainingOffset -= segmentLength;
                continue;
            }

            if (displayPoints.Count == 0)
            {
                float ratio = segmentLength > 0.0001f
                    ? remainingOffset / segmentLength
                    : 0.0f;
                displayPoints.Add(Vector3.Lerp(segmentStart, segmentEnd, ratio));
                remainingOffset = 0.0f;
            }

            displayPoints.Add(segmentEnd);
        }
    }

    private void SetVisible(bool visible)
    {
        for (int i = 0; i < dashRenderers.Count; i++)
        {
            dashRenderers[i].enabled = visible && i < activeDashCount;
        }

        if (arrowLine != null)
        {
            arrowLine.enabled = visible;
        }
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            Destroy(previewMaterial);
        }
        else
        {
            DestroyImmediate(previewMaterial);
        }
    }
}
