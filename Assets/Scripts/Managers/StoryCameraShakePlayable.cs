using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryCameraShakePlayable : PlayableBehaviour
{
    public float force = 1f;

    private bool fired;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (fired || !Application.isPlaying)
        {
            return;
        }

        fired = true;

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.PlayShake(force);
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        fired = false;
    }
}
