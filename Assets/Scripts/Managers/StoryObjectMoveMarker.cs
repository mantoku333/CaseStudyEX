using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
[HideInMenu]
[DisplayName("Story/Object Move Point")]
public sealed class StoryObjectMoveMarker : Marker
{
    [SerializeField] private string actorKey = "iris";
    [SerializeField] private ExposedReference<Transform> target;
    [SerializeField] private StoryObjectMoveTargetMode targetMode = StoryObjectMoveTargetMode.Marker;
    [SerializeField, Min(1)] private int markerNo = 1;
    [SerializeField] private Vector3 worldPosition;
    [SerializeField] private bool keepCurrentZ = true;
    [SerializeField] private bool moveX = true;
    [SerializeField] private bool moveY = true;
    [SerializeField] private bool useUmbrellaWalk;

    public string ActorKey => string.IsNullOrWhiteSpace(actorKey) ? "iris" : actorKey.Trim();
    public StoryObjectMoveTargetMode TargetMode => targetMode;
    public int MarkerNo => Mathf.Max(1, markerNo);
    public Vector3 WorldPosition => worldPosition;
    public bool KeepCurrentZ => keepCurrentZ;
    public bool MoveX => moveX;
    public bool MoveY => moveY;
    public bool UseUmbrellaWalk => useUmbrellaWalk;
    public string TargetLabel => targetMode == StoryObjectMoveTargetMode.Marker
        ? $"Marker {MarkerNo:00}"
        : $"World {worldPosition.x:0.##}, {worldPosition.y:0.##}, {worldPosition.z:0.##}";

    public Transform ResolveTarget(IExposedPropertyTable resolver)
    {
        return target.Resolve(resolver);
    }
}
