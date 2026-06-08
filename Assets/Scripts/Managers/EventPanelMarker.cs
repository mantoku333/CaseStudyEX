using System.ComponentModel;
using System.Globalization;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public enum EventPanelMarkerMode
{
    ExistingPanel = 0,
    Content = 1
}

[System.Serializable]
[HideInMenu]
[DisplayName("Event/Panel Point")]
public sealed class EventPanelMarker : Marker
{
    [SerializeField, HideInInspector] private string markerId = string.Empty;
    [SerializeField] private EventPanelMarkerMode markerMode = EventPanelMarkerMode.ExistingPanel;
    [SerializeField] private ExposedReference<GameObject> panelObject;
    [SerializeField] private ExposedReference<EventPanelPresenter> panelPresenter;
    [SerializeField] private string panelPresenterName = string.Empty;
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

    public string TriggerKey =>
        string.Join(
            "|",
            string.IsNullOrWhiteSpace(markerId) ? nameof(EventPanelMarker) : markerId.Trim(),
            markerMode.ToString(),
            panelObject.exposedName.ToString(),
            panelPresenter.exposedName.ToString(),
            ResolvePanelPresenterName(),
            panelKind.ToString(),
            ResolveTitle(),
            time.ToString("0.######", CultureInfo.InvariantCulture));

    public EventPanelMarkerMode MarkerMode => markerMode;
    public bool UsesExistingPanel => markerMode == EventPanelMarkerMode.ExistingPanel;
    public bool HasPanelReference =>
        !string.IsNullOrWhiteSpace(panelObject.exposedName.ToString()) ||
        !string.IsNullOrWhiteSpace(panelPresenter.exposedName.ToString());
    public string PanelPresenterName => ResolvePanelPresenterName();
    public bool PauseTimelineUntilClosed => pauseTimelineUntilClosed;
    public float AutoCloseSecondsWhenNoButton => Mathf.Max(0f, autoCloseSecondsWhenNoButton);
    public EventPanelKind PanelKind => panelKind;
    public string ResolvedTitle => ResolveTitle();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(markerId))
        {
            markerId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    public EventPanelContent BuildContent()
    {
        return new EventPanelContent
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
    }

    public EventPanelPresenter ResolvePanelPresenter(PlayableDirector director)
    {
        if (director == null)
        {
            return null;
        }

        GameObject resolvedPanelObject = panelObject.Resolve(director);
        if (resolvedPanelObject != null)
        {
            EventPanelPresenter resolvedPresenter =
                resolvedPanelObject.GetComponent<EventPanelPresenter>();
            if (resolvedPresenter != null)
            {
                return resolvedPresenter;
            }

            resolvedPresenter =
                resolvedPanelObject.GetComponentInChildren<EventPanelPresenter>(includeInactive: true);
            if (resolvedPresenter != null)
            {
                return resolvedPresenter;
            }
        }

        return panelPresenter.Resolve(director);
    }

    private string ResolveTitle()
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return diaryEntryData != null ? diaryEntryData.GetTitle() : string.Empty;
    }

    private string ResolvePanelPresenterName()
    {
        return string.IsNullOrWhiteSpace(panelPresenterName) ? string.Empty : panelPresenterName.Trim();
    }

    private string ResolveBody()
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            return body.Trim();
        }

        return diaryEntryData != null ? diaryEntryData.GetContent() : string.Empty;
    }
}
