using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryAnimatorPlayable : PlayableBehaviour
{
    public StoryAnimatorActionType actionType;
    public string parameterOrStateName;
    public int layer;
    public float normalizedTime;
    public bool boolValue;
    public bool restoreBoolOnClipEnd;
    public float floatValue;
    public int integerValue;

    private Animator animator;
    private bool applied;
    private bool hadPreviousBoolValue;
    private bool previousBoolValue;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        applied = false;
        hadPreviousBoolValue = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying || applied)
        {
            return;
        }

        animator = ResolveAnimator(playerData);
        if (animator == null || string.IsNullOrWhiteSpace(parameterOrStateName))
        {
            return;
        }

        string name = parameterOrStateName.Trim();
        int resolvedLayer = Mathf.Max(0, layer);

        switch (actionType)
        {
            case StoryAnimatorActionType.PlayState:
                animator.Play(name, resolvedLayer, Mathf.Clamp01(normalizedTime));
                break;
            case StoryAnimatorActionType.SetTrigger:
                animator.SetTrigger(name);
                break;
            case StoryAnimatorActionType.SetBool:
                if (restoreBoolOnClipEnd)
                {
                    previousBoolValue = animator.GetBool(name);
                    hadPreviousBoolValue = true;
                }

                animator.SetBool(name, boolValue);
                break;
            case StoryAnimatorActionType.SetFloat:
                animator.SetFloat(name, floatValue);
                break;
            case StoryAnimatorActionType.SetInteger:
                animator.SetInteger(name, integerValue);
                break;
        }

        applied = true;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (!Application.isPlaying ||
            actionType != StoryAnimatorActionType.SetBool ||
            !restoreBoolOnClipEnd ||
            !hadPreviousBoolValue ||
            animator == null ||
            string.IsNullOrWhiteSpace(parameterOrStateName))
        {
            return;
        }

        animator.SetBool(parameterOrStateName.Trim(), previousBoolValue);
        hadPreviousBoolValue = false;
    }

    private static Animator ResolveAnimator(object playerData)
    {
        if (playerData is Animator directAnimator)
        {
            return directAnimator;
        }

        if (playerData is GameObject gameObject)
        {
            return gameObject.GetComponent<Animator>();
        }

        if (playerData is Component component)
        {
            return component.GetComponent<Animator>();
        }

        return null;
    }
}
