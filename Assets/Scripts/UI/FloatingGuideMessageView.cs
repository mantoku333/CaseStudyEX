using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("CaseStudy/UI/Floating Guide Message View")]
[DisallowMultipleComponent]
public sealed class FloatingGuideMessageView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text tmpMessageLabel;
    [SerializeField] private Text legacyMessageLabel;

    [Header("Animation")]
    [SerializeField] private float showSeconds = 0.45f;
    [SerializeField] private float hideSeconds = 0.45f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool visibleOnAwake;

    private Coroutine fadeRoutine;

    private void Reset()
    {
        ResolveReferences();
        ApplyImmediate(visibleOnAwake);
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyImmediate(visibleOnAwake);
    }

    private void OnValidate()
    {
        ResolveReferences();
        showSeconds = Mathf.Max(0f, showSeconds);
        hideSeconds = Mathf.Max(0f, hideSeconds);
    }

    public void Show()
    {
        FadeTo(1f, showSeconds);
    }

    public void Hide()
    {
        FadeTo(0f, hideSeconds);
    }

    public void ShowMessage(string message)
    {
        SetMessage(message);
        Show();
    }

    public void SetMessage(string message)
    {
        if (tmpMessageLabel != null)
        {
            tmpMessageLabel.text = message;
        }

        if (legacyMessageLabel != null)
        {
            legacyMessageLabel.text = message;
        }
    }

    public void ApplyImmediate(bool visible)
    {
        ResolveReferences();
        if (canvasGroup == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void FadeTo(float targetAlpha, float seconds)
    {
        ResolveReferences();
        if (canvasGroup == null)
        {
            Debug.LogWarning($"[FloatingGuideMessageView] CanvasGroup is missing. object='{name}'", this);
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, Mathf.Max(0f, seconds)));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float seconds)
    {
        float startAlpha = canvasGroup.alpha;
        if (seconds <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float eased = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (tmpMessageLabel == null)
        {
            tmpMessageLabel = GetComponentInChildren<TMP_Text>(true);
        }

        if (legacyMessageLabel == null)
        {
            legacyMessageLabel = GetComponentInChildren<Text>(true);
        }
    }
}
