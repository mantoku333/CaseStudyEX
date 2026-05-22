using UnityEngine;

[DisallowMultipleComponent]
public class ReloadIndicatorController : MonoBehaviour
{
    private const string FillObjectName = "ReloadIndicatorFill";

    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

    [SerializeField] private GunController gunController;
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField, Min(3)] private int segmentCount = 48;
    [SerializeField] private float startAngleDegrees = 90.0f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private Color startColor = Color.red;
    [SerializeField] private Color middleColor = new Color(1.0f, 0.45f, 0.0f, 1.0f);
    [SerializeField] private Color completeColor = Color.white;
    [SerializeField, Range(0.01f, 0.99f)] private float orangeAtCompletionRatio = 0.25f;
    [SerializeField, Min(0.0f)] private float radius = 0.0f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh indicatorMesh;
    private MaterialPropertyBlock materialPropertyBlock;
    private Transform fillTransform;

    private void Awake()
    {
        ResolveReferences();
        ConfigureRenderer();
        HideIndicator();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureRenderer();
    }

    private void Update()
    {
        DisableSourceRenderer();

        if (gunController == null)
        {
            gunController = ResolveGunController();
        }

        if (gunController == null || !gunController.IsReloading)
        {
            HideIndicator();
            return;
        }

        float visibleRatio = gunController.ReloadRemainingRatio;
        if (visibleRatio <= 0.0f)
        {
            HideIndicator();
            return;
        }

        BuildIndicatorMesh(visibleRatio, EvaluateReloadColor(1.0f - visibleRatio));
    }

    private void ResolveReferences()
    {
        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        if (meshFilter == null)
        {
            meshFilter = ResolveFillComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = fillTransform.gameObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = ResolveFillComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = fillTransform.gameObject.AddComponent<MeshRenderer>();
            }
        }

        if (indicatorMesh == null)
        {
            indicatorMesh = new Mesh
            {
                name = "ReloadIndicatorMesh"
            };
            indicatorMesh.MarkDynamic();
        }

        if (meshFilter.sharedMesh != indicatorMesh)
        {
            meshFilter.sharedMesh = indicatorMesh;
        }

        if (materialPropertyBlock == null)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        if (gunController == null)
        {
            gunController = ResolveGunController();
        }
    }

    private T ResolveFillComponent<T>() where T : Component
    {
        ResolveFillTransform();
        return fillTransform.GetComponent<T>();
    }

    private void ResolveFillTransform()
    {
        if (fillTransform != null)
        {
            return;
        }

        fillTransform = transform.Find(FillObjectName);
        if (fillTransform == null)
        {
            GameObject fillObject = new GameObject(FillObjectName);
            fillTransform = fillObject.transform;
            fillTransform.SetParent(transform, false);
        }

        fillTransform.localPosition = Vector3.zero;
        fillTransform.localRotation = Quaternion.identity;
        fillTransform.localScale = Vector3.one;
    }

    private GunController ResolveGunController()
    {
        GunController parentGunController = GetComponentInParent<GunController>();
        if (parentGunController != null)
        {
            return parentGunController;
        }

        PlayerController playerController = GetComponentInParent<PlayerController>();
        if (playerController != null)
        {
            return playerController.GetComponentInChildren<GunController>(true);
        }

        return null;
    }

    private void ConfigureRenderer()
    {
        if (sourceRenderer != null)
        {
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
                meshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                meshRenderer.sortingOrder = sourceRenderer.sortingOrder;
            }

            sourceRenderer.enabled = false;
        }

        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
    }

    private void BuildIndicatorMesh(float visibleRatio, Color color)
    {
        ResolveReferences();

        visibleRatio = Mathf.Clamp01(visibleRatio);
        int segmentTotal = Mathf.Max(3, segmentCount);
        int activeSegments = Mathf.Max(1, Mathf.CeilToInt(segmentTotal * visibleRatio));
        float indicatorRadius = ResolveRadius();
        float arcDegrees = 360.0f * visibleRatio;
        float elapsedDegrees = 360.0f * (1.0f - visibleRatio);
        float direction = clockwise ? -1.0f : 1.0f;
        float firstAngle = startAngleDegrees + direction * elapsedDegrees;

        Vector3 center = ResolveCenter();
        Vector3[] vertices = new Vector3[activeSegments + 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[activeSegments * 3];

        vertices[0] = center;
        colors[0] = color;

        for (int i = 0; i <= activeSegments; i++)
        {
            float angleRatio = i / (float)activeSegments;
            float angleRadians =
                (firstAngle + direction * arcDegrees * angleRatio) *
                Mathf.Deg2Rad;

            vertices[i + 1] = center + new Vector3(
                Mathf.Cos(angleRadians) * indicatorRadius,
                Mathf.Sin(angleRadians) * indicatorRadius,
                0.0f);
            colors[i + 1] = color;
        }

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(0.5f, 0.5f);
        }

        for (int i = 0; i < activeSegments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;

            if (clockwise)
            {
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }
            else
            {
                triangles[triangleIndex + 1] = i + 2;
                triangles[triangleIndex + 2] = i + 1;
            }
        }

        indicatorMesh.Clear();
        indicatorMesh.vertices = vertices;
        indicatorMesh.colors = colors;
        indicatorMesh.uv = uvs;
        indicatorMesh.triangles = triangles;
        indicatorMesh.RecalculateBounds();

        ApplyRendererColor(color);

        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
        }
    }

    private Vector3 ResolveCenter()
    {
        if (sourceRenderer != null && sourceRenderer.sprite != null)
        {
            return sourceRenderer.sprite.bounds.center;
        }

        return Vector3.zero;
    }

    private float ResolveRadius()
    {
        if (radius > 0.0f)
        {
            return radius;
        }

        if (sourceRenderer != null && sourceRenderer.sprite != null)
        {
            Vector3 extents = sourceRenderer.sprite.bounds.extents;
            return Mathf.Max(extents.x, extents.y);
        }

        return 0.5f;
    }

    private Color EvaluateReloadColor(float completionRatio)
    {
        completionRatio = Mathf.Clamp01(completionRatio);

        if (completionRatio <= orangeAtCompletionRatio)
        {
            float t = completionRatio / orangeAtCompletionRatio;
            return Color.Lerp(startColor, middleColor, t);
        }

        float whiteT =
            (completionRatio - orangeAtCompletionRatio) /
            (1.0f - orangeAtCompletionRatio);
        return Color.Lerp(middleColor, completeColor, whiteT);
    }

    private void ApplyRendererColor(Color color)
    {
        if (meshRenderer == null)
        {
            return;
        }

        meshRenderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetColor(ColorPropertyId, color);
        materialPropertyBlock.SetColor(BaseColorPropertyId, color);
        materialPropertyBlock.SetTexture(MainTexPropertyId, Texture2D.whiteTexture);
        meshRenderer.SetPropertyBlock(materialPropertyBlock);
    }

    private void DisableSourceRenderer()
    {
        if (sourceRenderer != null && sourceRenderer.enabled)
        {
            sourceRenderer.enabled = false;
        }
    }

    private void HideIndicator()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        if (indicatorMesh != null)
        {
            indicatorMesh.Clear();
        }
    }

    private void OnDestroy()
    {
        if (indicatorMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(indicatorMesh);
        }
        else
        {
            DestroyImmediate(indicatorMesh);
        }
    }
}
