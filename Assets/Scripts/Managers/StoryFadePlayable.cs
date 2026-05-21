using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryFadePlayable : PlayableBehaviour
{
    public float fromAlpha;
    public float toAlpha = 1f;
    public bool useCurrentAlphaAsStart = true;
    public bool smoothStep = true;
    public Color color = Color.black;

    private bool initialized;
    private float resolvedFromAlpha;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        initialized = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!initialized)
        {
            resolvedFromAlpha = useCurrentAlphaAsStart ? StoryOverlayFader.Instance.CurrentAlpha : fromAlpha;
            initialized = true;
        }

        double duration = playable.GetDuration();
        float t = duration > 0d ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
        if (smoothStep)
        {
            t = t * t * (3f - 2f * t);
        }

        StoryOverlayFader.Instance.SetImmediate(Mathf.Lerp(resolvedFromAlpha, toAlpha, t), color);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (Application.isPlaying && initialized && playable.GetTime() >= playable.GetDuration())
        {
            StoryOverlayFader.Instance.SetImmediate(toAlpha, color);
        }
    }
}
