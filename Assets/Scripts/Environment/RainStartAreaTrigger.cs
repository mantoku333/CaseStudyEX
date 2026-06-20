using Metroidvania.Player;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Environment/Rain Start Area Trigger 2D")]
public sealed class RainStartAreaTrigger : MonoBehaviour
{
    [SerializeField] private RainCycleController rainController;
    [SerializeField, Min(0.01f)] private float rainDurationSeconds = 30f;
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;

    private void Awake()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        ResolveRainController();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((triggerOnlyOnce && hasTriggered) || !PlayerBodyColliderUtility.IsPlayerBodyCollider(other))
        {
            return;
        }

        ResolveRainController();
        if (rainController == null)
        {
            return;
        }

        hasTriggered = true;
        rainController.StartRainFor(rainDurationSeconds);
    }

    private void ResolveRainController()
    {
        if (rainController == null)
        {
            rainController = FindFirstObjectByType<RainCycleController>(FindObjectsInactive.Include);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rainDurationSeconds = Mathf.Max(0.01f, rainDurationSeconds);
    }
#endif
}
