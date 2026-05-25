using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("CaseStudy/Story/Story Event Marker")]
public sealed class StoryEventMarker : MonoBehaviour
{
    [SerializeField, Min(1)] private int markerNo = 1;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.85f, 1f, 0.85f);
    [SerializeField, Min(0.05f)] private float gizmoRadius = 0.18f;

    public int MarkerNo => Mathf.Max(1, markerNo);
    public Transform Target => transform;

    private void OnValidate()
    {
        markerNo = Mathf.Max(1, markerNo);

        string expectedName = $"Marker_{markerNo:00}";
        if (gameObject.name.StartsWith("Marker_", System.StringComparison.Ordinal) &&
            gameObject.name != expectedName)
        {
            gameObject.name = expectedName;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);
        Gizmos.DrawWireSphere(transform.position, gizmoRadius * 1.8f);

#if UNITY_EDITOR
        Handles.color = gizmoColor;
        Handles.Label(transform.position + Vector3.up * (gizmoRadius * 2.5f), $"M{MarkerNo:00}");
#endif
    }
}
