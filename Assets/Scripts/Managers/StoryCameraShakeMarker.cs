using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
[DisplayName("Story/Camera Shake Point")]
public sealed class StoryCameraShakeMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField, Min(0f)] private float force = 1f;
    [SerializeField] private StoryCameraShakeDirection direction = StoryCameraShakeDirection.Horizontal;
    [SerializeField] private Vector2 customDirection = Vector2.right;

    public float Force => Mathf.Max(0f, force);
    public StoryCameraShakeDirection Direction => direction;
    public Vector2 CustomDirection => customDirection;
    public PropertyName id => new PropertyName(nameof(StoryCameraShakeMarker));
    public NotificationFlags flags => NotificationFlags.TriggerOnce;
}
