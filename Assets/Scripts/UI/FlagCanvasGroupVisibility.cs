using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class FlagCanvasGroupVisibility : MonoBehaviour
{
    [SerializeField] private string triggerFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool expectedFlagValue = true;
    [SerializeField] private bool startHidden = true;

    private Canvas targetCanvas;
    private GraphicRaycaster targetRaycaster;

    private void Awake()
    {
        targetCanvas = GetComponent<Canvas>();
        targetRaycaster = GetComponent<GraphicRaycaster>();

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
        if (targetCanvas != null)
        {
            targetCanvas.enabled = visible;
        }

        if (targetRaycaster != null)
        {
            targetRaycaster.enabled = visible;
        }
    }
}
