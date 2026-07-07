using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OptionsCanvasButtonFeedback :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    ISubmitHandler
{
    private const float DimMultiplier = 0.55f;
    private const float PressedScale = 0.9f;
    private const float PressDuration = 0.08f;
    private const float ReleaseDuration = 0.14f;

    private Button button;
    private Graphic singleIllustrationGraphic;
    private Transform[] scaleTargets;
    private Vector3[] baseScales;
    private bool dimWhenIdle;
    private bool pointerInside;
    private Color baseColor = Color.white;
    private bool hasBaseColor;
    private bool pointerDownPulseStarted;
    private Coroutine scaleRoutine;

    public void Configure(Button targetButton, Graphic targetGraphic, bool useDimHover)
    {
        Configure(targetButton, targetGraphic, useDimHover, null);
    }

    public void Configure(Button targetButton, Graphic targetGraphic, bool useDimHover, Transform[] targetScaleRoots)
    {
        bool graphicChanged = singleIllustrationGraphic != targetGraphic;
        button = targetButton;
        singleIllustrationGraphic = targetGraphic;
        dimWhenIdle = useDimHover && targetGraphic != null;
        ConfigureScaleTargets(targetScaleRoots);

        if (singleIllustrationGraphic != null && (graphicChanged || !hasBaseColor))
        {
            baseColor = singleIllustrationGraphic.color;
            hasBaseColor = true;
        }
        else if (singleIllustrationGraphic == null)
        {
            hasBaseColor = false;
        }

        ApplyVisual();
    }

    private void OnEnable()
    {
        CaptureBaseScales();
        pointerInside = false;
        ApplyVisual();
    }

    private void OnDisable()
    {
        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }

        ResetScaleTargets();
        pointerInside = false;
        ApplyColor(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        ApplyVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        ApplyVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAnimatePress())
        {
            return;
        }

        pointerDownPulseStarted = true;
        StartPressPulse();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanAnimatePress())
        {
            return;
        }

        if (!pointerDownPulseStarted)
        {
            StartPressPulse();
        }

        pointerDownPulseStarted = false;
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!CanAnimatePress())
        {
            return;
        }

        StartPressPulse();
    }

    private void StartPressPulse()
    {
        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
        }

        scaleRoutine = StartCoroutine(SubmitPulse());
    }

    private void ApplyVisual()
    {
        ApplyColor(dimWhenIdle && !pointerInside);
    }

    private void ApplyColor(bool dim)
    {
        if (singleIllustrationGraphic == null || !hasBaseColor)
        {
            return;
        }

        Color color = baseColor;
        if (dimWhenIdle && dim)
        {
            color.r *= DimMultiplier;
            color.g *= DimMultiplier;
            color.b *= DimMultiplier;
        }

        singleIllustrationGraphic.color = color;
    }

    private bool CanAnimatePress()
    {
        return button == null || button.IsInteractable();
    }

    private IEnumerator SubmitPulse()
    {
        yield return ScaleTo(PressedScale, PressDuration);
        yield return ScaleTo(1f, ReleaseDuration);
        scaleRoutine = null;
    }

    private IEnumerator ScaleTo(float scaleMultiplier, float duration)
    {
        EnsureScaleTargets();
        Vector3[] startScales = new Vector3[scaleTargets.Length];
        Vector3[] targetScales = new Vector3[scaleTargets.Length];
        for (int i = 0; i < scaleTargets.Length; i++)
        {
            if (scaleTargets[i] == null)
            {
                continue;
            }

            startScales[i] = scaleTargets[i].localScale;
            targetScales[i] = baseScales[i] * scaleMultiplier;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
            t = 1f - Mathf.Pow(1f - t, 3f);
            ApplyScaleTargets(startScales, targetScales, t);
            yield return null;
        }

        ApplyScaleTargets(targetScales);
        scaleRoutine = null;
    }

    private void ConfigureScaleTargets(Transform[] targetScaleRoots)
    {
        if (targetScaleRoots == null || targetScaleRoots.Length == 0)
        {
            scaleTargets = new[] { transform };
            CaptureBaseScales();
            return;
        }

        int count = 0;
        for (int i = 0; i < targetScaleRoots.Length; i++)
        {
            if (targetScaleRoots[i] != null)
            {
                count++;
            }
        }

        if (count == 0)
        {
            scaleTargets = new[] { transform };
            CaptureBaseScales();
            return;
        }

        scaleTargets = new Transform[count];
        int writeIndex = 0;
        for (int i = 0; i < targetScaleRoots.Length; i++)
        {
            if (targetScaleRoots[i] != null)
            {
                scaleTargets[writeIndex++] = targetScaleRoots[i];
            }
        }

        CaptureBaseScales();
    }

    private void EnsureScaleTargets()
    {
        if (scaleTargets == null || scaleTargets.Length == 0 || baseScales == null || baseScales.Length != scaleTargets.Length)
        {
            ConfigureScaleTargets(null);
        }
    }

    private void CaptureBaseScales()
    {
        if (scaleTargets == null || scaleTargets.Length == 0)
        {
            scaleTargets = new[] { transform };
        }

        baseScales = new Vector3[scaleTargets.Length];
        for (int i = 0; i < scaleTargets.Length; i++)
        {
            baseScales[i] = scaleTargets[i] != null ? scaleTargets[i].localScale : Vector3.one;
        }
    }

    private void ResetScaleTargets()
    {
        if (scaleTargets == null || baseScales == null)
        {
            return;
        }

        int count = Mathf.Min(scaleTargets.Length, baseScales.Length);
        for (int i = 0; i < count; i++)
        {
            if (scaleTargets[i] != null)
            {
                scaleTargets[i].localScale = baseScales[i];
            }
        }
    }

    private void ApplyScaleTargets(Vector3[] startScales, Vector3[] targetScales, float t)
    {
        int count = Mathf.Min(scaleTargets.Length, Mathf.Min(startScales.Length, targetScales.Length));
        for (int i = 0; i < count; i++)
        {
            if (scaleTargets[i] != null)
            {
                scaleTargets[i].localScale = Vector3.LerpUnclamped(startScales[i], targetScales[i], t);
            }
        }
    }

    private void ApplyScaleTargets(Vector3[] targetScales)
    {
        int count = Mathf.Min(scaleTargets.Length, targetScales.Length);
        for (int i = 0; i < count; i++)
        {
            if (scaleTargets[i] != null)
            {
                scaleTargets[i].localScale = targetScales[i];
            }
        }
    }
}
