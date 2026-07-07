using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class FallHoleGradient2D : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private Vector2 size = new Vector2(6.45f, 8.46f);
    [SerializeField, Min(1)] private int verticalSegments = 24;
    [SerializeField] private Vector2 pivot = new Vector2(0.5f, 1f);

    [Header("Color")]
    [SerializeField] private Color gradientColor = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField, Range(0f, 0.9f)] private float transparentTopRatio = 0.3f;
    [SerializeField, Range(0.1f, 5f)] private float depthAlphaPower = 1.35f;

    [Header("Render")]
    [SerializeField] private Material gradientMaterial;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 3;

    private Mesh generatedMesh;
    private Material runtimeMaterial;

    private void Reset()
    {
        Rebuild();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        size.x = Mathf.Max(0.01f, size.x);
        size.y = Mathf.Max(0.01f, size.y);
        verticalSegments = Mathf.Max(1, verticalSegments);
        Rebuild();
    }

    private void OnDisable()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
        {
            meshFilter.sharedMesh = null;
        }

        DestroyGeneratedObject(generatedMesh);
        DestroyGeneratedObject(runtimeMaterial);
        generatedMesh = null;
        runtimeMaterial = null;
    }

    private void Rebuild()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshFilter == null || meshRenderer == null)
        {
            return;
        }

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh
            {
                name = "Fall Hole Gradient Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        BuildMesh(generatedMesh);
        meshFilter.sharedMesh = generatedMesh;

        Material material = gradientMaterial != null ? gradientMaterial : GetRuntimeMaterial();
        if (material != null)
        {
            meshRenderer.sharedMaterial = material;
        }

        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
    }

    private void BuildMesh(Mesh targetMesh)
    {
        int rows = verticalSegments + 1;
        int vertexCount = rows * 2;
        int triangleCount = verticalSegments * 6;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[triangleCount];

        float width = Mathf.Max(0.01f, size.x);
        float height = Mathf.Max(0.01f, size.y);
        float left = -width * Mathf.Clamp01(pivot.x);
        float right = left + width;
        float top = height * (1f - Mathf.Clamp01(pivot.y));
        float bottom = top - height;

        for (int row = 0; row < rows; row++)
        {
            float depth = row / (float)verticalSegments;
            float fadeDepth = Mathf.InverseLerp(transparentTopRatio, 1f, depth);
            float y = Mathf.Lerp(top, bottom, depth);
            Color color = gradientColor;
            color.a = Mathf.Lerp(0f, gradientColor.a, Mathf.Pow(fadeDepth, depthAlphaPower));

            int vertexIndex = row * 2;
            vertices[vertexIndex] = new Vector3(left, y, 0f);
            vertices[vertexIndex + 1] = new Vector3(right, y, 0f);
            uv[vertexIndex] = new Vector2(0f, 1f - depth);
            uv[vertexIndex + 1] = new Vector2(1f, 1f - depth);
            colors[vertexIndex] = color;
            colors[vertexIndex + 1] = color;
        }

        for (int row = 0; row < verticalSegments; row++)
        {
            int vertexIndex = row * 2;
            int triangleIndex = row * 6;
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex + 2;
            triangles[triangleIndex + 4] = vertexIndex + 1;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        targetMesh.Clear();
        targetMesh.vertices = vertices;
        targetMesh.uv = uv;
        targetMesh.colors = colors;
        targetMesh.triangles = triangles;
        targetMesh.RecalculateBounds();
    }

    private Material GetRuntimeMaterial()
    {
        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        Shader shader = Shader.Find("CaseStudy/Fall Hole Gradient");
        if (shader == null)
        {
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Fall Hole Gradient Runtime Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        return runtimeMaterial;
    }

    private static void DestroyGeneratedObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void OnDrawGizmosSelected()
    {
        float width = Mathf.Max(0.01f, size.x);
        float height = Mathf.Max(0.01f, size.y);
        float left = -width * Mathf.Clamp01(pivot.x);
        float right = left + width;
        float top = height * (1f - Mathf.Clamp01(pivot.y));
        float bottom = top - height;

        Vector3 topLeft = transform.TransformPoint(left, top, 0f);
        Vector3 topRight = transform.TransformPoint(right, top, 0f);
        Vector3 bottomRight = transform.TransformPoint(right, bottom, 0f);
        Vector3 bottomLeft = transform.TransformPoint(left, bottom, 0f);

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
}
