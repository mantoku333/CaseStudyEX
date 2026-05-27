using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryCameraShakePlayable : PlayableBehaviour
{
    public float force = 1f;
    public StoryCameraShakeDirection direction = StoryCameraShakeDirection.Horizontal;
    public Vector2 customDirection = Vector2.right;

    private bool fired;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (fired || !Application.isPlaying)
        {
            return;
        }

        fired = true;

        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager == null)
        {
            cameraManager = Object.FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
        }

        if (cameraManager != null)
        {
            cameraManager.PlayShake(force, ResolveDirection());
            return;
        }

        Debug.LogWarning("[StoryCameraShakePlayable] CameraManager was not found. Shake was skipped.");
    }

    public override void OnGraphStop(Playable playable)
    {
        fired = false;
    }

    private Vector3 ResolveDirection()
    {
        Vector2 resolved = direction switch
        {
            StoryCameraShakeDirection.Vertical => Vector2.up,
            StoryCameraShakeDirection.Diagonal => new Vector2(1f, 1f),
            StoryCameraShakeDirection.Custom => customDirection,
            _ => Vector2.right,
        };

        if (resolved.sqrMagnitude <= Mathf.Epsilon)
        {
            resolved = Vector2.right;
        }

        resolved.Normalize();
        return new Vector3(resolved.x, resolved.y, 0f);
    }
}
