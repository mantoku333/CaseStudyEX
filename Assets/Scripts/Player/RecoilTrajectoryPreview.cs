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
    [SerializeField] private Color trajectoryColor = new Color(0.75f, 0.95f, 1.0f, 0.72f);

    private readonly List<Vector3> trajectoryPoints = new List<Vector3>(64);

    private GunController gunController;
    private PlayerController playerController;
    private PlayerAbilityController abilityController;
    private UmbrellaController umbrellaController;
    private Rigidbody2D playerRigidbody;
    private CapsuleCollider2D playerCollider;
    private LineRenderer trajectoryLine;
    private LineRenderer arrowLine;
    private Material previewMaterial;
    private LayerMask solidLayerMask;

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
            ? playerRigidbody.GetComponent<CapsuleCollider2D>()
            : null;
        solidLayerMask = LayerMask.GetMask(GroundLayerName);
    }

    private void EnsureRenderers()
    {
        if (trajectoryLine != null && arrowLine != null)
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

        trajectoryLine = GetOrCreateLineRenderer(previewObject.transform, "TrajectoryLine");
        arrowLine = GetOrCreateLineRenderer(previewObject.transform, "ArrowLine");

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            previewMaterial = new Material(shader)
            {
                name = "RecoilTrajectoryPreviewMaterial"
            };
            trajectoryLine.sharedMaterial = previewMaterial;
            arrowLine.sharedMaterial = previewMaterial;
        }

        ConfigureRenderer(trajectoryLine, lineWidth);
        ConfigureRenderer(arrowLine, lineWidth * 1.35f);
        arrowLine.positionCount = 3;
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

        Vector2 lossyScale = transform.root.lossyScale;
        Vector2 capsuleSize = Vector2.Scale(
            playerCollider.size,
            new Vector2(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y)));
        RaycastHit2D hit = Physics2D.CapsuleCast(
            position,
            capsuleSize,
            playerCollider.direction,
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
        return playerCollider.transform.TransformPoint(playerCollider.offset);
    }

    private void DrawTrajectory()
    {
        if (trajectoryPoints.Count < 2)
        {
            SetVisible(false);
            return;
        }

        trajectoryLine.positionCount = trajectoryPoints.Count;
        trajectoryLine.SetPositions(trajectoryPoints.ToArray());

        Vector3 end = trajectoryPoints[trajectoryPoints.Count - 1];
        Vector2 tangent = end - trajectoryPoints[trajectoryPoints.Count - 2];
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

    private void SetVisible(bool visible)
    {
        if (trajectoryLine != null)
        {
            trajectoryLine.enabled = visible;
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
        if (previewMaterial == null)
        {
            return;
        }

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
