using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("CaseStudy/Story/Story Event Actor")]
public sealed class StoryEventActor : MonoBehaviour
{
    [SerializeField] private string actorKey = "actor";
    [SerializeField] private Transform bubbleTarget;

    public string ActorKey => string.IsNullOrWhiteSpace(actorKey) ? name : actorKey.Trim();
    public Transform Root => transform;
    public Transform BubbleTarget => bubbleTarget != null ? bubbleTarget : transform;

    private void Reset()
    {
        actorKey = name;
        bubbleTarget = transform;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(actorKey))
        {
            actorKey = name;
        }
    }
}
