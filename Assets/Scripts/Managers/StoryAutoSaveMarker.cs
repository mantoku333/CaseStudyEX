using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[System.Serializable]
[HideInMenu]
[DisplayName("Story/Auto Save")]
public sealed class StoryAutoSaveMarker : Marker
{
    [SerializeField] private bool applyCompleteMutationsBeforeSave;

    public bool ApplyCompleteMutationsBeforeSave => applyCompleteMutationsBeforeSave;
}
