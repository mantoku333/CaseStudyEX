using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("CaseStudy/Story/Story Event Actor")]
public sealed class StoryEventActor : MonoBehaviour
{
    [SerializeField] private string actorKey = "actor";
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private Transform bubbleTarget;
    [SerializeField] private Vector3 bubbleOffset;

    public string ActorKey => string.IsNullOrWhiteSpace(actorKey) ? name : actorKey.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ActorKey : displayName.Trim();
    public Transform Root => transform;
    public Transform BubbleTarget => bubbleTarget != null ? bubbleTarget : transform;
    public Vector3 BubbleOffset => bubbleOffset;

    private void Reset()
    {
        actorKey = name;
        displayName = name;
        bubbleTarget = transform;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(actorKey))
        {
            actorKey = name;
        }
    }

    public bool MatchesSpeakerName(string speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return false;
        }

        string speaker = speakerName.Trim();
        return string.Equals(ActorKey, speaker, System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(DisplayName, speaker, System.StringComparison.OrdinalIgnoreCase);
    }
}
