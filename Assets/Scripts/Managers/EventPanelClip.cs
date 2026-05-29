using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Event/Panel")]
public sealed class EventPanelClip : PlayableAsset, ITimelineClipAsset
{
    [SerializeField, HideInInspector] private string clipId = string.Empty;
    [SerializeField] private EventPanelKind panelKind = EventPanelKind.Custom;
    [SerializeField] private DiaryEntryData diaryEntryData;
    [SerializeField] private string title = string.Empty;
    [SerializeField, TextArea(2, 10)] private string body = string.Empty;
    [SerializeField] private string closeLabel = string.Empty;
    [SerializeField] private Sprite illustration;
    [SerializeField] private Sprite[] animationFrames = System.Array.Empty<Sprite>();
    [SerializeField, Min(0f)] private float animationFramesPerSecond = 12f;
    [SerializeField, Min(0f)] private float animationLoopIntervalSeconds = 0.5f;
    [SerializeField] private bool pauseTimelineUntilClosed = true;
    [SerializeField, Min(0f)] private float autoCloseSecondsWhenNoButton = 3f;

    public ClipCaps clipCaps => ClipCaps.ClipIn;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(clipId))
        {
            clipId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<EventPanelPlayable> playable = ScriptPlayable<EventPanelPlayable>.Create(graph);
        EventPanelPlayable behaviour = playable.GetBehaviour();
        behaviour.clipId = ClipId;
        behaviour.content = BuildContent();
        behaviour.pauseTimelineUntilClosed = pauseTimelineUntilClosed;
        behaviour.autoCloseSecondsWhenNoButton = autoCloseSecondsWhenNoButton;
        return playable;
    }

    private string ClipId => string.IsNullOrWhiteSpace(clipId) ? BuildFallbackClipId() : clipId.Trim();

    private EventPanelContent BuildContent()
    {
        var content = new EventPanelContent
        {
            kind = panelKind,
            title = ResolveTitle(),
            body = ResolveBody(),
            closeLabel = closeLabel,
            illustration = illustration,
            animationFrames = animationFrames,
            animationFramesPerSecond = animationFramesPerSecond,
            animationLoopIntervalSeconds = animationLoopIntervalSeconds
        };

        return content;
    }

    private string ResolveTitle()
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return diaryEntryData != null ? diaryEntryData.GetTitle() : string.Empty;
    }

    private string ResolveBody()
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            return body.Trim();
        }

        return diaryEntryData != null ? diaryEntryData.GetContent() : string.Empty;
    }

    private string BuildFallbackClipId()
    {
        string resolvedTitle = ResolveTitle();
        if (!string.IsNullOrWhiteSpace(resolvedTitle))
        {
            return resolvedTitle.Trim();
        }

        return panelKind.ToString();
    }
}
