using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[CustomTimelineEditor(typeof(StoryYarnDialogueMarker))]
public sealed class StoryYarnDialogueMarkerEditor : MarkerEditor
{
    private static readonly Color MarkerColor = new Color(0.75f, 0.45f, 1f, 0.9f);

    public override MarkerDrawOptions GetMarkerOptions(IMarker marker)
    {
        StoryYarnDialogueMarker dialogueMarker = marker as StoryYarnDialogueMarker;
        return new MarkerDrawOptions
        {
            tooltip = dialogueMarker != null ? $"Dialogue: {dialogueMarker.NodeName}" : "Dialogue",
            errorText = string.Empty,
        };
    }

    public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region)
    {
        StoryMarkerEditorDrawing.DrawMarker(region, MarkerColor, uiState);
    }
}

[CustomTimelineEditor(typeof(StoryAudioMarker))]
public sealed class StoryAudioMarkerEditor : MarkerEditor
{
    private static readonly Color MarkerColor = new Color(0.85f, 0.55f, 0.15f, 0.9f);

    public override MarkerDrawOptions GetMarkerOptions(IMarker marker)
    {
        StoryAudioMarker audioMarker = marker as StoryAudioMarker;
        if (audioMarker == null)
        {
            return new MarkerDrawOptions { tooltip = "Audio", errorText = string.Empty };
        }

        string clipName = audioMarker.AudioClip != null ? audioMarker.AudioClip.name : "None";
        return new MarkerDrawOptions
        {
            tooltip = $"{audioMarker.AudioKind} {audioMarker.Action}: {clipName}",
            errorText = audioMarker.Action == StoryTimelineAudioAction.Play && audioMarker.AudioClip == null
                ? "Audio clip is not assigned."
                : string.Empty,
        };
    }

    public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region)
    {
        StoryMarkerEditorDrawing.DrawMarker(region, MarkerColor, uiState);
    }
}

[CustomTimelineEditor(typeof(StoryCameraShakeMarker))]
public sealed class StoryCameraShakeMarkerEditor : MarkerEditor
{
    private static readonly Color MarkerColor = new Color(1f, 0.3f, 0.25f, 0.9f);

    public override MarkerDrawOptions GetMarkerOptions(IMarker marker)
    {
        StoryCameraShakeMarker shakeMarker = marker as StoryCameraShakeMarker;
        return new MarkerDrawOptions
        {
            tooltip = shakeMarker != null
                ? $"Camera Shake: force {shakeMarker.Force:0.##}, {shakeMarker.Direction}"
                : "Camera Shake",
            errorText = string.Empty,
        };
    }

    public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region)
    {
        StoryMarkerEditorDrawing.DrawMarker(region, MarkerColor, uiState);
    }
}

[CustomTimelineEditor(typeof(EventPanelMarker))]
public sealed class EventPanelMarkerEditor : MarkerEditor
{
    private static readonly Color MarkerColor = new Color(0.95f, 0.78f, 0.32f, 0.9f);

    public override MarkerDrawOptions GetMarkerOptions(IMarker marker)
    {
        EventPanelMarker panelMarker = marker as EventPanelMarker;
        if (panelMarker == null)
        {
            return new MarkerDrawOptions { tooltip = "Panel", errorText = string.Empty };
        }

        if (panelMarker.UsesExistingPanel)
        {
            string targetName = panelMarker.HasPanelReference
                ? "Reference"
                : string.IsNullOrWhiteSpace(panelMarker.PanelPresenterName)
                ? "Default"
                : panelMarker.PanelPresenterName;
            return new MarkerDrawOptions
            {
                tooltip = $"Panel: Existing Panel ({targetName})",
                errorText = string.Empty,
            };
        }

        string title = string.IsNullOrWhiteSpace(panelMarker.ResolvedTitle)
            ? panelMarker.PanelKind.ToString()
            : panelMarker.ResolvedTitle;
        return new MarkerDrawOptions
        {
            tooltip = $"Panel: {title}",
            errorText = string.Empty,
        };
    }

    public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region)
    {
        StoryMarkerEditorDrawing.DrawMarker(region, MarkerColor, uiState);
    }
}

internal static class StoryMarkerEditorDrawing
{
    public static void DrawMarker(MarkerOverlayRegion region, Color color, MarkerUIStates uiState)
    {
        Color markerColor = color;
        if (uiState.HasFlag(MarkerUIStates.Selected))
        {
            markerColor = Color.Lerp(markerColor, Color.white, 0.25f);
            markerColor.a = 1f;
        }

        Rect markerRect = region.markerRegion;
        Rect tintRect = markerRect;
        tintRect.xMin += 1f;
        tintRect.xMax -= 1f;
        tintRect.yMin += 1f;
        tintRect.yMax -= 1f;

        EditorGUI.DrawRect(tintRect, markerColor);
    }
}
