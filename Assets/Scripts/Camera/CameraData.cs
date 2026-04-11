using UnityEngine;

[CreateAssetMenu(fileName = "CameraData", menuName = "Camera/Camera Data")]
public class CameraData : ScriptableObject
{
    public const float MinFollowOffsetY = -10f;
    public const float MaxFollowOffsetY = 10f;
    public const float MinOrthographicSize = 1f;
    public const float MaxOrthographicSize = 30f;

    [Header("Follow Offset")]
    [SerializeField] private float followOffsetY = 1f;

    [Header("Zoom")]
    [SerializeField] private float orthographicSize = 10f;

    public float FollowOffsetY => followOffsetY;
    public float OrthographicSize => orthographicSize;

    public void SetFollowOffsetY(float y)
    {
        followOffsetY = Mathf.Clamp(y, MinFollowOffsetY, MaxFollowOffsetY);
    }

    public void SetOrthographicSize(float size)
    {
        orthographicSize = Mathf.Clamp(size, MinOrthographicSize, MaxOrthographicSize);
    }

    private void OnValidate()
    {
        SetFollowOffsetY(followOffsetY);
        SetOrthographicSize(orthographicSize);
    }
}