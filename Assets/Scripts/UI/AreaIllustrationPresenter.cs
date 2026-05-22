using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[AddComponentMenu("UI/Area Illustration Presenter")]
[DisallowMultipleComponent]
public sealed class AreaIllustrationPresenter : MonoBehaviour
{
    private const string DefaultTargetSceneName = "Fix_Alpha2_Fuyuno";

    [Header("Display Conditions")]
    [SerializeField] private string targetSceneName = DefaultTargetSceneName;
    [SerializeField] private string targetRoomId = "Col_1-1,1-1";
    [SerializeField] private string requiredFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool expectedFlagValue = true;

    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform revealRoot;
    [SerializeField] private int sortingOrder = 30;

    [Header("Animation")]
    [SerializeField] private float showDuration = 0.45f;
    [SerializeField] private float hideDuration = 0.22f;

    private MinimapManager subscribedManager;
    private Coroutine visibilityRoutine;
    private bool isVisible;

    private void Awake()
    {
        ResolveReferences();
        ConfigureCanvas();
        ApplyImmediate(false);
    }

    private void OnEnable()
    {
        SubscribeToMinimap();
        EvaluateAndApply();
    }

    private void OnDisable()
    {
        if (subscribedManager != null)
        {
            subscribedManager.Changed -= EvaluateAndApply;
            subscribedManager = null;
        }
    }

    private void Update()
    {
        SubscribeToMinimap();
        EvaluateAndApply();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ConfigureCanvas();
    }

    public void EvaluateAndApply()
    {
        SetVisible(ShouldBeVisible());
    }

    private bool ShouldBeVisible()
    {
        if (!string.IsNullOrWhiteSpace(targetSceneName) &&
            !string.Equals(SceneManager.GetActiveScene().name, targetSceneName.Trim(), System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requiredFlagKey) &&
            GameProgressFlags.Get(requiredFlagKey.Trim()) != expectedFlagValue)
        {
            return false;
        }

        MinimapManager manager = MinimapManager.Instance;
        return manager != null &&
            !string.IsNullOrWhiteSpace(targetRoomId) &&
            MatchesRoomId(manager.CurrentRoomId);
    }

    private bool MatchesRoomId(string currentRoomId)
    {
        if (string.IsNullOrWhiteSpace(currentRoomId) || string.IsNullOrWhiteSpace(targetRoomId))
        {
            return false;
        }

        string[] candidates = targetRoomId.Split(',', ';', '|');
        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i].Trim();
            if (candidate.Length > 0 && string.Equals(currentRoomId, candidate, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SubscribeToMinimap()
    {
        MinimapManager manager = MinimapManager.Instance;
        if (manager == subscribedManager)
        {
            return;
        }

        if (subscribedManager != null)
        {
            subscribedManager.Changed -= EvaluateAndApply;
        }

        subscribedManager = manager;
        if (subscribedManager != null)
        {
            subscribedManager.Changed += EvaluateAndApply;
        }
    }

    private void SetVisible(bool visible)
    {
        ResolveReferences();
        if (canvasGroup == null || revealRoot == null || isVisible == visible)
        {
            return;
        }

        isVisible = visible;

        if (visibilityRoutine != null)
        {
            StopCoroutine(visibilityRoutine);
        }

        visibilityRoutine = StartCoroutine(AnimateVisibility(visible));
    }

    private IEnumerator AnimateVisibility(bool visible)
    {
        float duration = Mathf.Max(0.01f, visible ? showDuration : hideDuration);
        float startAlpha = canvasGroup.alpha;
        float endAlpha = visible ? 1f : 0f;
        float startScaleX = revealRoot.localScale.x;
        float endScaleX = visible ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = visible ? EaseOutCubic(t) : EaseInCubic(t);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
            revealRoot.localScale = new Vector3(Mathf.Lerp(startScaleX, endScaleX, eased), 1f, 1f);
            yield return null;
        }

        ApplyImmediate(visible);
        visibilityRoutine = null;
    }

    private void ApplyImmediate(bool visible)
    {
        if (canvasGroup == null || revealRoot == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        revealRoot.localScale = new Vector3(visible ? 1f : 0f, 1f, 1f);
    }

    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (revealRoot == null)
        {
            Transform reveal = transform.Find("RevealRoot");
            revealRoot = reveal as RectTransform;
        }
    }

    private void ConfigureCanvas()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
        }

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            raycaster.enabled = false;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float t)
    {
        return t * t * t;
    }
}
