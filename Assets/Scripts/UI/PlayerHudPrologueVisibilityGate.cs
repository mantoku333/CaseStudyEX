using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerHudPrologueVisibilityGate : MonoBehaviour
{
    private const string HudCanvasName = "PlayerHUDCanvas";
    private const string PlayerHpName = "Player HP";

    [SerializeField] private string triggerFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool expectedFlagValue = true;
    [SerializeField] private string targetChildName = PlayerHpName;

    private GameObject targetObject;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToHud();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToHud();
    }

    private static void AttachToHud()
    {
        GameObject hud = GameObject.Find(HudCanvasName);
        if (hud == null || hud.GetComponent<PlayerHudPrologueVisibilityGate>() != null)
        {
            return;
        }

        hud.AddComponent<PlayerHudPrologueVisibilityGate>();
    }

    private void Awake()
    {
        ResolveTarget();
        EvaluateAndApply();
    }

    private void OnEnable()
    {
        GameProgressFlags.FlagChanged += OnProgressFlagChanged;
        EvaluateAndApply();
    }

    private void OnDisable()
    {
        GameProgressFlags.FlagChanged -= OnProgressFlagChanged;
    }

    private void OnTransformChildrenChanged()
    {
        targetObject = null;
        EvaluateAndApply();
    }

    private void OnProgressFlagChanged(string flagKey, bool value)
    {
        if (!string.Equals(flagKey, triggerFlagKey, System.StringComparison.Ordinal))
        {
            return;
        }

        EvaluateAndApply();
    }

    private void EvaluateAndApply()
    {
        if (targetObject == null)
        {
            ResolveTarget();
        }

        if (targetObject == null)
        {
            return;
        }

        bool visible = !string.IsNullOrWhiteSpace(triggerFlagKey) &&
            GameProgressFlags.Get(triggerFlagKey) == expectedFlagValue;

        if (targetObject.activeSelf != visible)
        {
            targetObject.SetActive(visible);
        }
    }

    private void ResolveTarget()
    {
        Transform target = FindDeepChild(transform, targetChildName);
        targetObject = target != null ? target.gameObject : null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform match = FindDeepChild(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
