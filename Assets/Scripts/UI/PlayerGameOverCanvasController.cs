using Player;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameName.UI
{
    /// <summary>
    /// シーンに配置したゲームオーバー Canvas の表示と復帰導線を管理する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerGameOverCanvasController : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private Graphic fogRevealGraphic;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;

        [Header("Player References")]
        [SerializeField] private PlayerHealth targetHealth;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool autoFindPlayerHealth = true;

        [Header("Scene Navigation")]
        [SerializeField] private string titleSceneName = "Title";

        [Header("Reveal Animation")]
        [SerializeField, Min(0.01f)] private float revealDuration = 0.35f;
        [SerializeField, Range(0.6f, 1f)] private float revealStartScale = 0.88f;
        [SerializeField] private float fogRevealStart = -0.18f;
        [SerializeField] private float fogRevealEnd = 1.12f;
        [SerializeField] private AnimationCurve revealEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly int FogRevealPropertyId = Shader.PropertyToID("_Reveal");

        private PlayerInput playerInput;
        private Rigidbody2D playerRigidbody;
        private Behaviour[] gameplayBehaviours;
        private CanvasGroup panelCanvasGroup;
        private Canvas rootCanvas;
        private GraphicRaycaster graphicRaycaster;
        private Vector3 cardInitialScale = Vector3.one;
        private Material fogRevealMaterialInstance;
        private Coroutine revealCoroutine;
        private bool isVisible;
        private bool hitStopExternalPauseRegistered;

        private void Awake()
        {
            ResolveReferences();
            RegisterButtonListeners();
            HidePanel();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeToHealth();
            HidePanel();
        }

        private void Update()
        {
            if (targetHealth == null && autoFindPlayerHealth)
            {
                ResolveReferences();
                SubscribeToHealth();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromHealth();
        }

        private void OnDestroy()
        {
            UnsubscribeFromHealth();
            UnregisterButtonListeners();
            ReleaseFogMaterialInstance();
            Time.timeScale = 1f;
            ReleaseHitStopExternalPause();
        }

        private void ResolveReferences()
        {
            rootCanvas ??= GetComponent<Canvas>();
            graphicRaycaster ??= GetComponent<GraphicRaycaster>();

            if (panelRoot == null)
            {
                panelRoot = FindChildGameObject("PanelRoot");
            }

            if (retryButton == null)
            {
                retryButton = FindChildComponent<Button>("PanelRoot/Card/RetryButton");
            }

            if (fogRevealGraphic == null)
            {
                fogRevealGraphic = FindChildComponent<Graphic>("PanelRoot/FogReveal");
            }

            if (cardRoot == null)
            {
                cardRoot = FindChildComponent<RectTransform>("PanelRoot/Card");
            }

            if (titleButton == null)
            {
                titleButton = FindChildComponent<Button>("PanelRoot/Card/TitleButton");
            }

            if (panelRoot != null)
            {
                panelCanvasGroup = EnsureCanvasGroup(panelRoot);
            }

            if (cardRoot != null)
            {
                cardInitialScale = cardRoot.localScale;
            }

            EnsureFogMaterialInstance();

            if (targetHealth == null && autoFindPlayerHealth)
            {
                GameObject playerObject = global::PlayerReferenceCache.GetGameObject(playerTag);
                if (playerObject != null)
                {
                    targetHealth = playerObject.GetComponent<PlayerHealth>();
                    if (targetHealth == null)
                    {
                        targetHealth = playerObject.GetComponentInChildren<PlayerHealth>(true);
                    }
                }

                if (targetHealth == null)
                {
                    targetHealth = FindFirstObjectByType<PlayerHealth>();
                }
            }

            var playerController = FindFirstObjectByType<global::PlayerController>();
            playerInput = playerController != null ? playerController.GetComponent<PlayerInput>() : null;
            playerRigidbody = playerController != null ? playerController.GetComponent<Rigidbody2D>() : null;

            if (playerController != null)
            {
                gameplayBehaviours = new Behaviour[]
                {
                    playerController,
                    playerController.GetComponent<DodgeController>(),
                    playerController.GetComponent<FallThroughController>(),
                    playerController.GetComponent<UmbrellaController>(),
                    playerController.GetComponent<GunController>()
                };
            }
            else
            {
                gameplayBehaviours = null;
            }
        }

        private void SubscribeToHealth()
        {
            if (targetHealth == null)
            {
                return;
            }

            targetHealth.Died -= ShowGameOver;
            targetHealth.Died += ShowGameOver;
        }

        private void UnsubscribeFromHealth()
        {
            if (targetHealth == null)
            {
                return;
            }

            targetHealth.Died -= ShowGameOver;
        }

        private void RegisterButtonListeners()
        {
            BindButton(retryButton, RestartFromLastSavePoint);
            BindButton(titleButton, ReturnToTitle);
        }

        private void UnregisterButtonListeners()
        {
            UnbindButton(retryButton, RestartFromLastSavePoint);
            UnbindButton(titleButton, ReturnToTitle);
        }

        private void ShowGameOver()
        {
            if (isVisible || panelRoot == null)
            {
                return;
            }

            isVisible = true;
            SetCanvasRenderingEnabled(true);
            PauseGameplay();
            RefreshRetryButtonState();
            panelRoot.SetActive(true);
            BeginRevealAnimation();

        }

        private void HidePanel()
        {
            isVisible = false;

            if (revealCoroutine != null)
            {
                StopCoroutine(revealCoroutine);
                revealCoroutine = null;
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
            }

            if (cardRoot != null)
            {
                cardRoot.localScale = cardInitialScale;
            }

            SetFogRevealValue(fogRevealStart);

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            SetCanvasRenderingEnabled(false);
        }

        private void PrepareForSceneTransition()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            HidePanel();
            SetCanvasRenderingEnabled(false);
            Canvas.ForceUpdateCanvases();
        }

        private void PauseGameplay()
        {
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }

            if (gameplayBehaviours != null)
            {
                for (int i = 0; i < gameplayBehaviours.Length; i++)
                {
                    if (gameplayBehaviours[i] != null)
                    {
                        gameplayBehaviours[i].enabled = false;
                    }
                }
            }

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
                playerRigidbody.Sleep();
            }

            RegisterHitStopExternalPause();
            Time.timeScale = 0f;
        }

        private void RestartFromLastSavePoint()
        {
            if (!SaveManager.TryGetLatestSaveSlot(out int latestSlotIndex, out _))
            {
                Debug.LogWarning(
                    "[PlayerGameOverCanvasController] No readable save found.",
                    this);
                RefreshRetryButtonState();
                return;
            }

            PrepareForSceneTransition();
            Time.timeScale = 1f;
            ReleaseHitStopExternalPause();

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (!SaveManager.TryLoadGame(latestSlotIndex, activeSceneName, reloadCurrentScene: true))
            {
                Debug.LogWarning(
                    $"[PlayerGameOverCanvasController] Failed to load latest save slot {latestSlotIndex}. Returning to title.",
                    this);
                SceneManager.LoadScene(titleSceneName);
            }
        }

        private void ReturnToTitle()
        {
            PrepareForSceneTransition();
            Time.timeScale = 1f;
            ReleaseHitStopExternalPause();
            SceneManager.LoadScene(titleSceneName);
        }

        private void RegisterHitStopExternalPause()
        {
            if (hitStopExternalPauseRegistered)
            {
                return;
            }

            HitStopController.BeginExternalPause();
            hitStopExternalPauseRegistered = true;
        }

        private void ReleaseHitStopExternalPause()
        {
            if (!hitStopExternalPauseRegistered)
            {
                return;
            }

            HitStopController.EndExternalPause();
            hitStopExternalPauseRegistered = false;
        }

        private void BeginRevealAnimation()
        {
            if (revealCoroutine != null)
            {
                StopCoroutine(revealCoroutine);
            }

            revealCoroutine = StartCoroutine(PlayRevealAnimation());
        }

        private IEnumerator PlayRevealAnimation()
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
            }

            if (cardRoot != null)
            {
                cardRoot.localScale = cardInitialScale * revealStartScale;
            }

            SetFogRevealValue(fogRevealStart);

            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / revealDuration);
                float eased = revealEase != null ? revealEase.Evaluate(normalized) : normalized;

                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = eased;
                }

                if (cardRoot != null)
                {
                    float scale = Mathf.LerpUnclamped(revealStartScale, 1f, eased);
                    cardRoot.localScale = cardInitialScale * scale;
                }

                float fogReveal = Mathf.LerpUnclamped(fogRevealStart, fogRevealEnd, eased);
                SetFogRevealValue(fogReveal);

                yield return null;
            }

            if (cardRoot != null)
            {
                cardRoot.localScale = cardInitialScale;
            }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 1f;
            }

            SetFogRevealValue(fogRevealEnd);

            revealCoroutine = null;

            if (EventSystem.current != null && retryButton != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
            }
        }

        private GameObject FindChildGameObject(string path)
        {
            Transform child = transform.Find(path);
            return child != null ? child.gameObject : null;
        }

        private T FindChildComponent<T>(string path) where T : Component
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        private void EnsureFogMaterialInstance()
        {
            if (fogRevealGraphic == null)
            {
                return;
            }

            if (fogRevealMaterialInstance != null)
            {
                if (fogRevealGraphic.material != fogRevealMaterialInstance)
                {
                    fogRevealGraphic.material = fogRevealMaterialInstance;
                }

                return;
            }

            Material sourceMaterial = fogRevealGraphic.material;
            if (sourceMaterial == null)
            {
                return;
            }

            fogRevealMaterialInstance = new Material(sourceMaterial);
            fogRevealMaterialInstance.name = $"{sourceMaterial.name} (Runtime)";
            fogRevealGraphic.material = fogRevealMaterialInstance;
        }

        private void ReleaseFogMaterialInstance()
        {
            if (fogRevealMaterialInstance == null)
            {
                return;
            }

            if (fogRevealGraphic != null)
            {
                fogRevealGraphic.material = null;
            }

            Destroy(fogRevealMaterialInstance);
            fogRevealMaterialInstance = null;
        }

        private void SetFogRevealValue(float value)
        {
            EnsureFogMaterialInstance();

            if (fogRevealMaterialInstance == null)
            {
                return;
            }

            fogRevealMaterialInstance.SetFloat(FogRevealPropertyId, value);
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return null;
            }

            if (!targetObject.TryGetComponent(out CanvasGroup canvasGroup))
            {
                canvasGroup = targetObject.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        private void RefreshRetryButtonState()
        {
            if (retryButton == null)
            {
                return;
            }

            retryButton.interactable = SaveManager.TryGetLatestSaveSlot(out _, out _);
        }

        private void SetCanvasRenderingEnabled(bool isEnabled)
        {
            if (rootCanvas != null)
            {
                rootCanvas.enabled = isEnabled;
            }

            if (graphicRaycaster != null)
            {
                graphicRaycaster.enabled = isEnabled;
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }
    }
}
