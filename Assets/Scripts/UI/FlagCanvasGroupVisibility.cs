using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class FlagCanvasGroupVisibility : MonoBehaviour
{
    [SerializeField] private string triggerFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool expectedFlagValue = true;
    [SerializeField] private bool startHidden = true;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (startHidden)
        {
            ApplyVisible(false);
        }
        else
        {
            EvaluateAndApply();
        }
    }

    private void Update()
    {
        EvaluateAndApply();
    }

    public void EvaluateAndApply()
    {
        bool visible = false;

        if (!string.IsNullOrWhiteSpace(triggerFlagKey))
        {
            visible = GameProgressFlags.Get(triggerFlagKey) == expectedFlagValue;
        }

        ApplyVisible(visible);
    }

    private void ApplyVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
