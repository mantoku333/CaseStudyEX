using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[AddComponentMenu("UI/Area Illustration Presenter")]
[DisallowMultipleComponent]
public sealed class AreaIllustrationPresenter : MonoBehaviour
{
    private const string PrefabResourcePath = "UI/AreaIllustrationCanvas";
    private const string DefaultTargetSceneName = "Fix_Alpha2_Fuyuno";

    [Header("Display Conditions")]
    [SerializeField] private string targetSceneName = DefaultTargetSceneName;
    [SerializeField] private string targetRoomId = "Col_1-1";
    [SerializeField] private string requiredFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool expectedFlagValue = true;

    [Header("Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite[] textSprites;

    [Header("Layout")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private int sortingOrder = 30;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 245f);
    [SerializeField] private Vector2 backgroundSize = new Vector2(836f, 392f);
    [SerializeField] private float textSpacing = 12f;
    [SerializeField] private float textScale = 1.15f;
    [SerializeField] private Vector2 textOffset = new Vector2(0f, 22f);

    [Header("Animation")]
    [SerializeField] private float showDuration = 0.45f;
    [SerializeField] private float hideDuration = 0.22f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform revealRoot;
    private MinimapManager subscribedManager;
    private Coroutine visibilityRoutine;
    private bool isVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySpawnForScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawnForScene(scene);
    }

    private static void TrySpawnForScene(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, DefaultTargetSceneName, System.StringComparison.Ordinal))
        {
            return;
        }

        AreaIllustrationPresenter existing = FindFirstObjectByType<AreaIllustrationPresenter>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[AreaIllustrationPresenter] Prefab not found at Resources/{PrefabResourcePath}.");
            return;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = "AreaIllustrationCanvas";
    }

    private void Awake()
    {
        EnsureCanvas();
        BuildView();
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
            string.Equals(manager.CurrentRoomId, targetRoomId.Trim(), System.StringComparison.Ordinal);
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
        if (isVisible == visible)
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
            float alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
            float scaleX = Mathf.Lerp(startScaleX, endScaleX, eased);

            canvasGroup.alpha = alpha;
            revealRoot.localScale = new Vector3(scaleX, 1f, 1f);
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

    private void EnsureCanvas()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        raycaster.enabled = false;
    }

    private void BuildView()
    {
        if (canvasGroup != null && revealRoot != null)
        {
            return;
        }

        RectTransform root = EnsureRectTransform(transform);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        revealRoot = CreateRect("RevealRoot", transform);
        revealRoot.anchorMin = new Vector2(0.5f, 0.5f);
        revealRoot.anchorMax = new Vector2(0.5f, 0.5f);
        revealRoot.pivot = new Vector2(0.5f, 0.5f);
        revealRoot.anchoredPosition = anchoredPosition;
        revealRoot.sizeDelta = backgroundSize;

        Image background = CreateImage("BackgroundImage", revealRoot, backgroundSprite);
        RectTransform backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = backgroundSize;

        BuildTextGroup();
    }

    private void BuildTextGroup()
    {
        if (textSprites == null || textSprites.Length == 0)
        {
            return;
        }

        RectTransform textRoot = CreateRect("TextGroup", revealRoot);
        textRoot.anchorMin = new Vector2(0.5f, 0.5f);
        textRoot.anchorMax = new Vector2(0.5f, 0.5f);
        textRoot.pivot = new Vector2(0.5f, 0.5f);
        textRoot.anchoredPosition = textOffset;

        float totalWidth = 0f;
        float maxHeight = 0f;
        for (int i = 0; i < textSprites.Length; i++)
        {
            Sprite sprite = textSprites[i];
            if (sprite == null)
            {
                continue;
            }

            totalWidth += sprite.rect.width * textScale;
            maxHeight = Mathf.Max(maxHeight, sprite.rect.height * textScale);
        }

        totalWidth += Mathf.Max(0, textSprites.Length - 1) * textSpacing;
        textRoot.sizeDelta = new Vector2(totalWidth, maxHeight);

        float x = -totalWidth * 0.5f;
        for (int i = 0; i < textSprites.Length; i++)
        {
            Sprite sprite = textSprites[i];
            if (sprite == null)
            {
                continue;
            }

            Vector2 size = new Vector2(sprite.rect.width, sprite.rect.height) * textScale;
            Image image = CreateImage("Text_" + i, textRoot, sprite);
            RectTransform rect = (RectTransform)image.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(x + size.x * 0.5f, 0f);
            x += size.x + textSpacing;
        }
    }

    private Image CreateImage(string objectName, Transform parent, Sprite sprite)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }

    private static RectTransform EnsureRectTransform(Transform target)
    {
        RectTransform rect = target as RectTransform;
        if (rect != null)
        {
            return rect;
        }

        return target.gameObject.AddComponent<RectTransform>();
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
