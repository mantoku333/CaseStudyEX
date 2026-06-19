using System.Collections;
using GameName.Enemy;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameName.UI
{
    [DisallowMultipleComponent]
    public sealed class BossHpCanvasController : MonoBehaviour
    {
        private const string BossHpCanvasRootName = "BossHPCanvas";
        private const string BossHpRootName = "Boss HP";
        private const string BossNameObjectName = "Boss_Name";
        private const string FireLeftObjectName = "Boss_fireLeft";
        private const string FireRightObjectName = "Boss_fireRight";
        private const string HpFullObjectName = "Boss_HPFull";
        private const string HpReducedObjectName = "Boss_HPReduced";
        private const string HpDamagedObjectName = "Boss_Damaged";

        [Header("References")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private RectTransform fireLeft;
        [SerializeField] private RectTransform fireRight;
        [SerializeField] private GameObject hpFullObject;
        [SerializeField] private GameObject hpReducedObject;
        [SerializeField] private GameObject hpDamagedObject;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeInDuration = 1.00f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.5f;
        [SerializeField, Min(0f)] private float damagedCatchupDuration = 0.55f;
        [SerializeField, Min(0f)] private float defeatedHoldSeconds = 1.5f;

        [Header("Name Layout")]
        [SerializeField, Min(0f)] private float fireNamePadding = 90f;

        private readonly BarBinding hpFullBar = new BarBinding();
        private readonly BarBinding hpReducedBar = new BarBinding();
        private readonly BarBinding hpDamagedBar = new BarBinding();

        private global::BossAreaController activeArea;
        private IBossHealthSource activeHealthSource;
        private Coroutine visibilityRoutine;
        private Coroutine damagedRoutine;
        private bool hasTakenDamage;
        private float damagedRatio = 1f;
        private float reducedRatio = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureSceneInstance();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureSceneInstance();
        }

        private static void EnsureSceneInstance()
        {
            BossHpCanvasController existing = Object.FindFirstObjectByType<BossHpCanvasController>();
            if (existing != null)
            {
                existing.AutoBindReferences();
                existing.HideImmediately();
                return;
            }

            GameObject root = GameObject.Find(BossHpCanvasRootName);
            if (root == null)
            {
                root = GameObject.Find(BossHpRootName);
            }

            if (root == null)
            {
                return;
            }

            BossHpCanvasController controller = root.AddComponent<BossHpCanvasController>();
            controller.AutoBindReferences();
            controller.HideImmediately();
        }

        private void Awake()
        {
            AutoBindReferences();
            HideImmediately();
        }

        private void OnEnable()
        {
            global::BossAreaController.EncounterStarted -= HandleEncounterStarted;
            global::BossAreaController.EncounterStarted += HandleEncounterStarted;
            global::BossAreaController.EncounterCompleted -= HandleEncounterCompleted;
            global::BossAreaController.EncounterCompleted += HandleEncounterCompleted;
            global::BossAreaController.EncounterReset -= HandleEncounterReset;
            global::BossAreaController.EncounterReset += HandleEncounterReset;
        }

        private void OnDisable()
        {
            global::BossAreaController.EncounterStarted -= HandleEncounterStarted;
            global::BossAreaController.EncounterCompleted -= HandleEncounterCompleted;
            global::BossAreaController.EncounterReset -= HandleEncounterReset;
            UnsubscribeFromHealthSource();
        }

        private void OnDestroy()
        {
            UnsubscribeFromHealthSource();
        }

        private void HandleEncounterStarted(global::BossAreaController bossArea)
        {
            if (bossArea == null)
            {
                return;
            }

            BindToBossArea(bossArea);
        }

        private void HandleEncounterCompleted(global::BossAreaController bossArea)
        {
            if (bossArea == null || bossArea != activeArea)
            {
                return;
            }

            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
            }

            visibilityRoutine = StartCoroutine(PlayDefeatedHideRoutine());
        }

        private void HandleEncounterReset(global::BossAreaController bossArea)
        {
            if (bossArea == null || bossArea == activeArea)
            {
                HideImmediately();
            }
        }

        private void BindToBossArea(global::BossAreaController bossArea)
        {
            AutoBindReferences();
            UnsubscribeFromHealthSource();

            activeArea = bossArea;
            activeHealthSource = bossArea.BossHealthSource;
            hasTakenDamage = false;
            reducedRatio = 1f;
            damagedRatio = 1f;

            ApplyBossName(bossArea.BossDisplayName);
            ShowFullBarState();
            SetCanvasRenderingEnabled(true);

            if (activeHealthSource != null)
            {
                activeHealthSource.HealthChanged -= HandleBossHealthChanged;
                activeHealthSource.HealthChanged += HandleBossHealthChanged;
                activeHealthSource.Died -= HandleBossDied;
                activeHealthSource.Died += HandleBossDied;
                ApplyHealth(activeHealthSource.CurrentHealth, activeHealthSource.MaxHealth, force: true);
            }

            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
            }

            visibilityRoutine = StartCoroutine(FadeCanvas(canvasGroup != null ? canvasGroup.alpha : 0f, 1f, fadeInDuration));
        }

        private void HandleBossHealthChanged(int currentHealth, int maxHealth)
        {
            ApplyHealth(currentHealth, maxHealth, force: false);
        }

        private void HandleBossDied()
        {
            int maxHealth = activeHealthSource != null ? activeHealthSource.MaxHealth : 1;
            ApplyHealth(0, maxHealth, force: false);
        }

        private void ApplyHealth(int currentHealth, int maxHealth, bool force)
        {
            float nextRatio = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;

            if (force && nextRatio >= 0.999f)
            {
                ShowFullBarState();
                return;
            }

            if (!hasTakenDamage && nextRatio < 0.999f)
            {
                ShowDamagedBarState();
            }

            if (!hasTakenDamage)
            {
                return;
            }

            reducedRatio = nextRatio;
            hpReducedBar.SetRatio(reducedRatio);

            if (force || nextRatio >= damagedRatio)
            {
                damagedRatio = nextRatio;
                hpDamagedBar.SetRatio(damagedRatio);
                return;
            }

            if (damagedRoutine != null)
            {
                StopCoroutine(damagedRoutine);
            }

            damagedRoutine = StartCoroutine(AnimateDamagedBarTo(nextRatio));
        }

        private IEnumerator AnimateDamagedBarTo(float targetRatio)
        {
            float startRatio = damagedRatio;
            float duration = Mathf.Max(0.01f, damagedCatchupDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = normalized * normalized * (3f - 2f * normalized);
                damagedRatio = Mathf.Lerp(startRatio, targetRatio, eased);
                hpDamagedBar.SetRatio(damagedRatio);
                yield return null;
            }

            damagedRatio = targetRatio;
            hpDamagedBar.SetRatio(damagedRatio);
            damagedRoutine = null;
        }

        private IEnumerator PlayDefeatedHideRoutine()
        {
            yield return new WaitForSecondsRealtime(defeatedHoldSeconds);
            yield return FadeCanvas(canvasGroup != null ? canvasGroup.alpha : 1f, 0f, fadeOutDuration);
            HideImmediately();
            visibilityRoutine = null;
        }

        private IEnumerator FadeCanvas(float fromAlpha, float toAlpha, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                SetCanvasAlpha(toAlpha);
                yield break;
            }

            float elapsed = 0f;
            SetCanvasAlpha(fromAlpha);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                SetCanvasAlpha(Mathf.Lerp(fromAlpha, toAlpha, normalized));
                yield return null;
            }

            SetCanvasAlpha(toAlpha);
        }

        private void ShowFullBarState()
        {
            hasTakenDamage = false;
            reducedRatio = 1f;
            damagedRatio = 1f;

            hpFullBar.SetActive(true);
            hpReducedBar.SetActive(false);
            hpDamagedBar.SetActive(false);
            hpFullBar.SetRatio(1f);
            hpReducedBar.SetRatio(1f);
            hpDamagedBar.SetRatio(1f);
        }

        private void ShowDamagedBarState()
        {
            hasTakenDamage = true;
            hpFullBar.SetActive(false);
            hpReducedBar.SetActive(true);
            hpDamagedBar.SetActive(true);
        }

        private void HideImmediately()
        {
            if (visibilityRoutine != null)
            {
                StopCoroutine(visibilityRoutine);
                visibilityRoutine = null;
            }

            if (damagedRoutine != null)
            {
                StopCoroutine(damagedRoutine);
                damagedRoutine = null;
            }

            UnsubscribeFromHealthSource();
            activeArea = null;
            hasTakenDamage = false;
            reducedRatio = 1f;
            damagedRatio = 1f;

            ShowFullBarState();
            SetCanvasAlpha(0f);
            SetCanvasRenderingEnabled(false);
        }

        private void SetCanvasAlpha(float alpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ApplyBossName(string bossDisplayName)
        {
            if (bossNameText == null)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(bossDisplayName) ? "Boss" : bossDisplayName.Trim();
            bossNameText.text = displayName;
            bossNameText.ForceMeshUpdate();
            PositionFireIcons();
        }

        private void PositionFireIcons()
        {
            if (bossNameText == null)
            {
                return;
            }

            float preferredWidth = Mathf.Max(0f, bossNameText.preferredWidth);
            float fireOffsetX = preferredWidth * 0.5f + fireNamePadding;

            if (fireLeft != null)
            {
                Vector2 position = fireLeft.anchoredPosition;
                position.x = -fireOffsetX;
                fireLeft.anchoredPosition = position;
            }

            if (fireRight != null)
            {
                Vector2 position = fireRight.anchoredPosition;
                position.x = fireOffsetX;
                fireRight.anchoredPosition = position;
            }
        }

        private void AutoBindReferences()
        {
            rootCanvas ??= GetComponent<Canvas>();
            rootCanvas ??= GetComponentInParent<Canvas>(true);
            graphicRaycaster ??= GetComponent<GraphicRaycaster>();
            graphicRaycaster ??= GetComponentInParent<GraphicRaycaster>(true);

            if (canvasGroup == null)
            {
                if (!TryGetComponent(out canvasGroup))
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (bossNameText == null)
            {
                bossNameText = FindNamedComponent<TextMeshProUGUI>(BossNameObjectName);
            }

            if (fireLeft == null)
            {
                fireLeft = FindNamedComponent<RectTransform>(FireLeftObjectName);
            }

            if (fireRight == null)
            {
                fireRight = FindNamedComponent<RectTransform>(FireRightObjectName);
            }

            if (hpFullObject == null)
            {
                hpFullObject = FindNamedGameObject(HpFullObjectName);
            }

            if (hpReducedObject == null)
            {
                hpReducedObject = FindNamedGameObject(HpReducedObjectName);
            }

            if (hpDamagedObject == null)
            {
                hpDamagedObject = FindNamedGameObject(HpDamagedObjectName);
            }

            hpFullBar.Bind(hpFullObject);
            hpReducedBar.Bind(hpReducedObject);
            hpDamagedBar.Bind(hpDamagedObject);
        }

        private void SetCanvasRenderingEnabled(bool visible)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = visible;
            }

            if (graphicRaycaster != null)
            {
                graphicRaycaster.enabled = visible;
            }
        }

        private GameObject FindNamedGameObject(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == childName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private T FindNamedComponent<T>(string childName) where T : Component
        {
            GameObject child = FindNamedGameObject(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private void UnsubscribeFromHealthSource()
        {
            if (activeHealthSource == null)
            {
                return;
            }

            activeHealthSource.HealthChanged -= HandleBossHealthChanged;
            activeHealthSource.Died -= HandleBossDied;
            activeHealthSource = null;
        }

        private static void SetPivotKeepingPosition(RectTransform rectTransform, Vector2 pivot)
        {
            if (rectTransform == null || rectTransform.pivot == pivot)
            {
                return;
            }

            Vector2 oldPivot = rectTransform.pivot;
            Vector2 size = rectTransform.rect.size;
            Vector3 scale = rectTransform.localScale;
            Vector2 delta = new Vector2(
                (pivot.x - oldPivot.x) * size.x * scale.x,
                (pivot.y - oldPivot.y) * size.y * scale.y);

            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition += delta;
        }

        private sealed class BarBinding
        {
            private GameObject rootObject;
            private RectTransform rectTransform;
            private Image image;
            private Vector3 initialScale = Vector3.one;
            private bool initialized;
            private bool useImageFill;

            public void Bind(GameObject targetObject)
            {
                if (rootObject == targetObject && initialized)
                {
                    return;
                }

                rootObject = targetObject;
                rectTransform = rootObject != null ? rootObject.GetComponent<RectTransform>() : null;
                image = rootObject != null ? rootObject.GetComponent<Image>() : null;
                initialized = false;
                ConfigureForRightToLeftDrain();
            }

            public void SetActive(bool active)
            {
                if (rootObject != null)
                {
                    rootObject.SetActive(active);
                }
            }

            public void SetRatio(float ratio)
            {
                if (!initialized)
                {
                    ConfigureForRightToLeftDrain();
                }

                ratio = Mathf.Clamp01(ratio);

                if (useImageFill && image != null)
                {
                    image.fillAmount = ratio;
                    return;
                }

                if (rectTransform == null)
                {
                    return;
                }

                Vector3 scale = initialScale;
                scale.x = initialScale.x * ratio;
                rectTransform.localScale = scale;
            }

            private void ConfigureForRightToLeftDrain()
            {
                if (rectTransform == null)
                {
                    return;
                }

                useImageFill = image != null;
                if (useImageFill)
                {
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Horizontal;
                    image.fillOrigin = (int)Image.OriginHorizontal.Left;
                    image.fillAmount = 1f;
                }
                else
                {
                    SetPivotKeepingPosition(rectTransform, new Vector2(0f, 0.5f));
                }

                initialScale = rectTransform.localScale;
                initialized = true;
            }
        }
    }
}
