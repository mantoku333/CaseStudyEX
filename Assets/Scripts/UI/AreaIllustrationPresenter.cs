using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[AddComponentMenu("UI/Area Illustration Presenter")]
[DisallowMultipleComponent]
public sealed class AreaIllustrationPresenter : MonoBehaviour
{
    private const string DefaultTargetSceneName = "Fix_Alpha2_Fuyuno";
    private const float EvaluationInterval = 0.1f;

    [Header("Display Conditions")]
    [SerializeField] private string targetSceneName = DefaultTargetSceneName;
    [SerializeField] private string targetRoomId = "Col_1-1,1-1";
    [SerializeField] private string requiredFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool expectedFlagValue = true;
    [SerializeField] private string playerTag = "Player";

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
    private readonly System.Collections.Generic.List<Collider2D> targetRoomColliders2D = new System.Collections.Generic.List<Collider2D>();
    private readonly System.Collections.Generic.List<Collider> targetRoomColliders = new System.Collections.Generic.List<Collider>();
    private GameObject cachedPlayerObject;
    private float nextEvaluationTime;
    private bool targetRoomColliderCacheValid;
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
        SceneManager.sceneLoaded += HandleSceneLoaded;
        targetRoomColliderCacheValid = false;
        EvaluateAndApply();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (subscribedManager != null)
        {
            subscribedManager.Changed -= EvaluateAndApply;
            subscribedManager = null;
        }
    }

    private void Update()
    {
        SubscribeToMinimap();
        if (Time.unscaledTime < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = Time.unscaledTime + EvaluationInterval;
        EvaluateAndApply();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedPlayerObject = null;
        targetRoomColliderCacheValid = false;
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
            MatchesRoomId(manager.CurrentRoomId) &&
            IsPlayerInsideTargetRoom();
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

    private bool IsPlayerInsideTargetRoom()
    {
        GameObject playerObject = ResolvePlayerObject();
        if (playerObject == null)
        {
            return false;
        }

        Vector3 playerPosition = playerObject.transform.position;
        Vector2 playerPosition2D = new Vector2(playerPosition.x, playerPosition.y);
        EnsureTargetRoomColliderCache();

        for (int i = 0; i < targetRoomColliders2D.Count; i++)
        {
            Collider2D roomCollider = targetRoomColliders2D[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.OverlapPoint(playerPosition2D))
            {
                return true;
            }
        }

        for (int i = 0; i < targetRoomColliders.Count; i++)
        {
            Collider roomCollider = targetRoomColliders[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.bounds.Contains(playerPosition))
            {
                return true;
            }
        }

        return false;
    }

    private GameObject ResolvePlayerObject()
    {
        if (cachedPlayerObject != null && cachedPlayerObject.activeInHierarchy)
        {
            return cachedPlayerObject;
        }

        cachedPlayerObject = !string.IsNullOrWhiteSpace(playerTag)
            ? GameObject.FindGameObjectWithTag(playerTag)
            : null;
        return cachedPlayerObject;
    }

    private void EnsureTargetRoomColliderCache()
    {
        if (targetRoomColliderCacheValid)
        {
            return;
        }

        targetRoomColliders2D.Clear();
        targetRoomColliders.Clear();

        MinimapRoom[] rooms = FindObjectsByType<MinimapRoom>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null || !MatchesRoomId(room.RoomId))
            {
                continue;
            }

            Collider2D[] colliders2D = room.GetComponents<Collider2D>();
            for (int colliderIndex = 0; colliderIndex < colliders2D.Length; colliderIndex++)
            {
                targetRoomColliders2D.Add(colliders2D[colliderIndex]);
            }

            Collider[] colliders = room.GetComponents<Collider>();
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                targetRoomColliders.Add(colliders[colliderIndex]);
            }
        }

        targetRoomColliderCacheValid = true;
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
