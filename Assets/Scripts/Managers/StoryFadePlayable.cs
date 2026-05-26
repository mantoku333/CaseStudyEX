using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryFadePlayable : PlayableBehaviour
{
    public float fromAlpha;
    public float toAlpha = 1f;
    public bool useCurrentAlphaAsStart = true;
    public bool returnToStartAlpha = true;
    public float fadeInSeconds = 0.5f;
    public float fadeOutSeconds = 0.5f;
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

        float alpha = EvaluateAlpha(playable);
        StoryOverlayFader.Instance.SetImmediate(alpha, color);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (Application.isPlaying && initialized && playable.GetTime() >= playable.GetDuration())
        {
            StoryOverlayFader.Instance.SetImmediate(returnToStartAlpha ? resolvedFromAlpha : toAlpha, color);
        }
    }

    private float EvaluateAlpha(Playable playable)
    {
        if (!returnToStartAlpha)
        {
            double duration = playable.GetDuration();
            float t = duration > 0d ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
            return Mathf.Lerp(resolvedFromAlpha, toAlpha, Ease(t));
        }

        float durationSeconds = Mathf.Max(0.0001f, (float)playable.GetDuration());
        float inSeconds = Mathf.Max(0f, fadeInSeconds);
        float outSeconds = Mathf.Max(0f, fadeOutSeconds);
        float configuredTotal = inSeconds + outSeconds;

        if (configuredTotal <= 0f)
        {
            inSeconds = durationSeconds * 0.5f;
            outSeconds = durationSeconds * 0.5f;
        }
        else if (configuredTotal > durationSeconds)
        {
            float scale = durationSeconds / configuredTotal;
            inSeconds *= scale;
            outSeconds *= scale;
        }

        float time = Mathf.Clamp((float)playable.GetTime(), 0f, durationSeconds);
        if (inSeconds > 0f && time < inSeconds)
        {
            return Mathf.Lerp(resolvedFromAlpha, toAlpha, Ease(time / inSeconds));
        }

        float holdEnd = durationSeconds - outSeconds;
        if (time <= holdEnd || outSeconds <= 0f)
        {
            return toAlpha;
        }

        float outT = Mathf.Clamp01((time - holdEnd) / outSeconds);
        return Mathf.Lerp(toAlpha, resolvedFromAlpha, Ease(outT));
    }

    private float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return smoothStep ? t * t * (3f - 2f * t) : t;
    }
}
