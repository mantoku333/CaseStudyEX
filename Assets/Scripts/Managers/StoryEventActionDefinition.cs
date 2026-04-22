using System;
using UnityEngine;

public enum StoryEventActionType
{
    DelayRealtime = 0,
    FadeOverlay = 1,
    SwitchCameraPriority = 2,
    PlayTimeline = 3
}

[Serializable]
public sealed class StoryEventActionDefinition
{
    public StoryEventActionType actionType = StoryEventActionType.DelayRealtime;

    [Header("Common")]
    public string targetName = string.Empty;
    public bool waitForCompletion = true;

    [Header("Delay")]
    public float seconds = 0.5f;

    [Header("Fade")]
    public float durationSeconds = 0.5f;
    public float targetAlpha = 1f;
    public Color overlayColor = Color.black;

    [Header("Camera Priority")]
    public int priority = 100;
    public string secondaryTargetName = string.Empty;
    public int secondaryPriority = 0;

    [Header("Timeline")]
    public bool stopBeforePlay = true;
    public bool forceUnscaledTime = true;
}
