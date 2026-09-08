using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleSceneController : MonoBehaviour
{
    private const float ButtonActionDelay = 0.45f;

    [Header("シーン設定")]
    [SerializeField] private string gameSceneName = "Fix_Alpha2_Fuyuno";

    [Header("確認ウィンドウ")]
    [SerializeField] private GameObject quitConfirmPanel;
    [SerializeField] private GameObject titleLogoPanel;

    [Header("ボタン")]
    [SerializeField] private Button noButton;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button continueButton;

    [Header("Save Data List")]
    [SerializeField] private GameObject saveListPanel;
    [SerializeField] private GameObject loadConfirmPanel;
    [SerializeField] private TitleSaveSlotView[] saveSlotViews;
    [SerializeField] private Sprite defaultStageThumbnail;
    [SerializeField] private Sprite emptySlotThumbnail;
    [SerializeField] private LocationDatabase locationDatabase;
    [SerializeField] private StageDisplayInfo[] stageDisplayInfos;

    [Header("Title Rain")]
    [SerializeField] private GameObject titleRainRoot;
    [SerializeField] private AudioSource titleRainAudioSource;
    [SerializeField] private Image titleFadeImage;
    [SerializeField, Min(0.1f), Tooltip("初めからボタンを押してから、雨で画面を埋めるまでの秒数。")]
    private float startRainFillDuration = 3f;
    [SerializeField, Min(0.1f), Tooltip("初めからボタンを押してから、シーンを切り替えるまでの秒数。この間は雨を流しっぱなしにする。")]
    private float startSceneSwitchDelay = 6f;
    [SerializeField, Min(1f), Tooltip("初めからボタン演出の最後に、雨パーティクルのRate over Timeを何倍にするか。")]
    private float startRainRateMultiplier = 15f;
    [SerializeField, Min(1f), Tooltip("初めからボタン演出中に雨粒のStart Lifetimeを何倍にするか。雨が早く消える場合は増やす。")]
    private float startRainLifetimeMultiplier = 2f;
    [SerializeField, Min(3000), Tooltip("初めからボタン演出中に各Rain Particleへ設定するMax Particles。雨が途中で止む場合は増やす。")]
    private int startRainMaxParticles = 50000;
    [SerializeField, Range(0f, 1f), Tooltip("初めからボタン演出の最後に到達する雨音BGMの音量。")]
    private float startRainTargetVolume = 0.8f;
    [SerializeField, Range(0f, 1f), Tooltip("初めからボタン演出の最後に到達する暗転の濃さ。")]
    private float startFadeTargetAlpha = 1f;

    [Header("Loading")]
    [SerializeField] private string loadingText = "Now Loading……";
    [SerializeField] private TMP_FontAsset loadingFont;
    [SerializeField, Min(1f)] private float loadingFontSize = 36f;
    [SerializeField] private Color loadingTextColor = Color.white;
    [SerializeField] private Vector2 loadingTextAreaSize = new Vector2(520f, 96f);
    [SerializeField] private Vector2 loadingTextMargin = new Vector2(72f, 56f);
    [SerializeField, Min(0f)] private float continueFadeOutDuration = 0.45f;
    [SerializeField, Min(0f), Tooltip("ロード完了後に黒画面からゲーム画面へ戻るフェード時間。0ならTitle Rainのフェードアウト時間に合わせる。")]
    private float loadingFadeOutDuration = 0f;

    private int selectedSaveSlotIndex = SaveManager.DefaultSlotIndex;
    private Coroutine delayedButtonActionRoutine;
    private readonly Dictionary<int, Sprite> savePreviewThumbnailCache = new Dictionary<int, Sprite>();
    private bool titleRainAudioWasPlaying;
    private bool startingNewGame;
    private bool loadingGameScene;
    private bool titleLogoPanelDefaultActive = true;
    private Canvas titleLogoCanvas;
    private int titleLogoCanvasDefaultSortingOrder;
    private bool hasTitleLogoCanvasDefaultSortingOrder;
    private readonly object loadingCursorHideOwner = new object();

    private void OnEnable()
    {
        RefreshContinueButtonState();
    }

    private void OnDisable()
    {
        StopDelayedButtonAction();
        ClearSavePreviewThumbnailCache();
    }

    private void Start()
    {
        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        if (saveListPanel != null)
        {
            saveListPanel.SetActive(false);
        }

        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(false);
        }

        ResolveTitleLogoPanel();
        titleLogoPanelDefaultActive = titleLogoPanel == null || titleLogoPanel.activeSelf;
        RefreshTitleLogoPanelVisibility();
        SetTitleFadeAlpha(0f);
        ResolveButtonReferences();
        ResolveRainReferences();
        BindContinueButton();
        ConfigureTitleButtonFeedback();
        RefreshContinueButtonState();
        EnsureTitleRainActive();
    }

    private void RunAfterButtonFeedback(Action action)
    {
        if (delayedButtonActionRoutine != null)
        {
            return;
        }

        StopDelayedButtonAction();
        delayedButtonActionRoutine = StartCoroutine(RunAfterButtonFeedbackRoutine(action));
    }

    private IEnumerator RunAfterButtonFeedbackRoutine(Action action)
    {
        yield return new WaitForSecondsRealtime(ButtonActionDelay);
        delayedButtonActionRoutine = null;
        action?.Invoke();
    }

    private void StopDelayedButtonAction()
    {
        if (delayedButtonActionRoutine == null)
        {
            return;
        }

        StopCoroutine(delayedButtonActionRoutine);
        delayedButtonActionRoutine = null;
    }

    public void OnClickStartButton()
    {
        if (startingNewGame || loadingGameScene)
        {
            return;
        }

        BeginStartNewGameRainTransition();
    }

    private void BeginStartNewGameRainTransition()
    {
        if (startingNewGame || loadingGameScene)
        {
            return;
        }

        startingNewGame = true;
        SetMainTitleButtonsInteractable(false);
        SetLoadingCursorHidden(true);
        StartCoroutine(StartNewGameRainTransitionRoutine());
    }

    private IEnumerator StartNewGameRainTransitionRoutine()
    {
        EnsureTitleRainActive();
        float initialRainVolume = titleRainAudioSource != null ? titleRainAudioSource.volume : 0f;
        ParticleSystem[] rainParticles = titleRainRoot != null
            ? titleRainRoot.GetComponentsInChildren<ParticleSystem>(true)
            : Array.Empty<ParticleSystem>();

        ParticleSystem.MinMaxCurve[] initialRates = new ParticleSystem.MinMaxCurve[rainParticles.Length];
        ParticleSystem.MinMaxCurve[] initialLifetimes = new ParticleSystem.MinMaxCurve[rainParticles.Length];
        for (int i = 0; i < rainParticles.Length; i++)
        {
            ParticleSystem.EmissionModule emission = rainParticles[i].emission;
            emission.enabled = true;
            ParticleSystem.MainModule main = rainParticles[i].main;
            main.loop = true;
            main.useUnscaledTime = true;
            main.maxParticles = Mathf.Max(main.maxParticles, startRainMaxParticles);
            initialLifetimes[i] = main.startLifetime;
            main.startLifetime = MultiplyMinMaxCurve(initialLifetimes[i], startRainLifetimeMultiplier);
            initialRates[i] = emission.rateOverTime;
            rainParticles[i].Play(true);
        }

        float fillDuration = Mathf.Max(0.1f, startRainFillDuration);
        float sceneSwitchDelay = Mathf.Max(fillDuration, startSceneSwitchDelay);
        float elapsed = 0f;
        while (elapsed < sceneSwitchDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fillDuration);
            float eased = t * t * (3f - 2f * t);
            float multiplier = Mathf.Lerp(1f, startRainRateMultiplier, eased);
            ApplyRainRateMultiplier(rainParticles, initialRates, multiplier);
            ApplyRainVolume(initialRainVolume, eased);
            SetTitleFadeAlpha(CalculateFadeAlpha(elapsed, fillDuration));
            SustainRainPlayback(rainParticles);
            yield return null;
        }

        SetTitleFadeAlpha(startFadeTargetAlpha);
        StartNewGame();
    }

    private void ApplyRainVolume(float initialVolume, float t)
    {
        if (titleRainAudioSource == null)
        {
            return;
        }

        titleRainAudioSource.volume = Mathf.Lerp(initialVolume, startRainTargetVolume, Mathf.Clamp01(t));
    }

    private void SetTitleFadeAlpha(float alpha)
    {
        if (titleFadeImage == null)
        {
            return;
        }

        Color color = titleFadeImage.color;
        color.a = Mathf.Clamp01(alpha);
        titleFadeImage.color = color;
    }

    private static void SustainRainPlayback(ParticleSystem[] rainParticles)
    {
        if (rainParticles == null)
        {
            return;
        }

        for (int i = 0; i < rainParticles.Length; i++)
        {
            if (rainParticles[i] != null && !rainParticles[i].isPlaying)
            {
                rainParticles[i].Play(true);
            }
        }
    }

    private float CalculateFadeAlpha(float elapsed, float totalDuration)
    {
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, totalDuration));
        float eased = t * t * (3f - 2f * t);
        return Mathf.Lerp(0f, startFadeTargetAlpha, eased);
    }

    private static void ApplyRainRateMultiplier(
        ParticleSystem[] rainParticles,
        ParticleSystem.MinMaxCurve[] initialRates,
        float multiplier)
    {
        if (rainParticles == null || initialRates == null)
        {
            return;
        }

        int count = Mathf.Min(rainParticles.Length, initialRates.Length);
        for (int i = 0; i < count; i++)
        {
            if (rainParticles[i] == null)
            {
                continue;
            }

            ParticleSystem.EmissionModule emission = rainParticles[i].emission;
            ParticleSystem.MinMaxCurve rate = initialRates[i];
            rate.constant *= multiplier;
            rate.constantMin *= multiplier;
            rate.constantMax *= multiplier;
            emission.rateOverTime = rate;
        }
    }

    private static ParticleSystem.MinMaxCurve MultiplyMinMaxCurve(ParticleSystem.MinMaxCurve source, float multiplier)
    {
        source.constant *= multiplier;
        source.constantMin *= multiplier;
        source.constantMax *= multiplier;
        return source;
    }

    private void StartNewGame()
    {
        if (loadingGameScene)
        {
            return;
        }

        SaveManager.DeleteSave();
        SaveManager.ClearAllFlags();
        SaveManager.ClearAllItems();
        CurrentLocationService.ClearCurrentLocation();
        ElegantPointWallet.Clear();

        BeginLoadingScreen(TitleLoadingOverlay.LoadSceneAsync(gameSceneName), startsFromBlack: true);
    }

    public void OnClickContinueButton()
    {
        RunAfterButtonFeedback(ShowSaveListPanel);
    }

    public void OnClickQuitButton()
    {
        RunAfterButtonFeedback(ShowQuitConfirmPanel);
    }

    private void ShowQuitConfirmPanel()
    {
        if (quitConfirmPanel == null)
        {
            return;
        }

        quitConfirmPanel.SetActive(true);
        RefreshTitleLogoPanelVisibility();
        TitleButtonState.ResetPointerVisualMode();

        if (EventSystem.current != null && noButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
        }
    }

    public void OnClickNoButton()
    {
        RunAfterButtonFeedback(HideQuitConfirmPanel);
    }

    private void HideQuitConfirmPanel()
    {
        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        RefreshTitleLogoPanelVisibility();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void OnClickYesButton()
    {
        RunAfterButtonFeedback(QuitGame);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ResolveButtonReferences()
    {
        if (continueButton == null)
        {
            var obj = GameObject.Find("Btn_Countinue");
            if (obj != null)
            {
                continueButton = obj.GetComponent<Button>();
            }
        }

        if (noButton == null && quitConfirmPanel != null)
        {
            noButton = FindButtonInChildren(quitConfirmPanel.transform, "Btn_NO");
        }

        if (yesButton == null && quitConfirmPanel != null)
        {
            yesButton = FindButtonInChildren(quitConfirmPanel.transform, "Btn_YES");
        }
    }

    private void BindContinueButton()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.onClick.RemoveListener(OnClickContinueButton);
        continueButton.onClick.AddListener(OnClickContinueButton);
        ApplyMainButtonHitArea(continueButton);
    }

    private static void ConfigureTitleButtonFeedback()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (IsMainTitleButton(buttons[i]))
            {
                ApplyMainButtonHitArea(buttons[i]);
            }
            else
            {
                OptionsCanvasButtonUtility.ConfigureFigmaButton(buttons[i]);
            }
        }
    }

    private static void ApplyMainButtonHitArea(Button button)
    {
        if (!IsMainTitleButton(button))
        {
            return;
        }

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            image = button.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
        }

        image.raycastTarget = true;
        image.raycastPadding = Vector4.zero;
        button.targetGraphic = image;
    }

    private static bool IsMainTitleButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        string buttonName = button.name;
        return buttonName == "Btn_NewGame" || buttonName == "Btn_Countinue" || buttonName == "Btn_ExitGame";
    }

    private static void SetMainTitleButtonsInteractable(bool interactable)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (IsMainTitleButton(buttons[i]))
            {
                buttons[i].interactable = interactable;
            }
        }
    }

    private void RefreshContinueButtonState()
    {
        ResolveButtonReferences();
        BindContinueButton();

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }
    }

    private static Button FindButtonInChildren(Transform root, string name)
    {
        Transform found = FindTransformInChildren(root, name);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static Transform FindTransformInChildren(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindTransformInChildren(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void ShowSaveListPanel()
    {
        if (saveListPanel == null)
        {
            Debug.LogWarning("[TitleSceneController] Save list panel is not assigned.");
            return;
        }

        ClearSavePreviewThumbnailCache();
        saveListPanel.transform.SetAsLastSibling();
        RefreshSaveSlotViews();
        saveListPanel.SetActive(true);
        RefreshTitleLogoPanelVisibility();
        SetTitleRainPausedForSaveList(true);

        TitleSaveListPanelDesign2Skin saveListSkin = saveListPanel.GetComponent<TitleSaveListPanelDesign2Skin>();
        if (saveListSkin != null)
        {
            saveListSkin.Initialize(this);
        }

        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(false);
        }
    }

    public void OnClickSaveListBackButton()
    {
        RunAfterButtonFeedback(HideSaveListPanel);
    }

    private void HideSaveListPanel()
    {
        if (saveListPanel != null)
        {
            saveListPanel.SetActive(false);
        }

        RefreshTitleLogoPanelVisibility();
        SetTitleRainPausedForSaveList(false);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void ResolveRainReferences()
    {
        if (titleRainRoot == null)
        {
            titleRainRoot = GameObject.Find("RainOverlayCanvas");
        }

        if (titleRainAudioSource == null)
        {
            GameObject rainAudioObject = GameObject.Find("TitleRainBGM");
            if (rainAudioObject != null)
            {
                titleRainAudioSource = rainAudioObject.GetComponent<AudioSource>();
            }
        }
    }

    private void ResolveTitleLogoPanel()
    {
        if (titleLogoPanel != null)
        {
            CacheTitleLogoCanvasSortingOrder();
            return;
        }

        GameObject logoOverlayCanvas = GameObject.Find("LogoOverlayCanvas");
        if (logoOverlayCanvas != null)
        {
            titleLogoPanel = logoOverlayCanvas;
            CacheTitleLogoCanvasSortingOrder();
            return;
        }

        titleLogoPanel = GameObject.Find("rogo");
        CacheTitleLogoCanvasSortingOrder();
    }

    private void RefreshTitleLogoPanelVisibility()
    {
        ResolveTitleLogoPanel();
        if (titleLogoPanel == null)
        {
            return;
        }

        bool overlayOpen =
            (saveListPanel != null && saveListPanel.activeSelf) ||
            (quitConfirmPanel != null && quitConfirmPanel.activeSelf);

        titleLogoPanel.SetActive(titleLogoPanelDefaultActive);
        ApplyTitleLogoCanvasSortingOrder(overlayOpen);
    }

    private void CacheTitleLogoCanvasSortingOrder()
    {
        if (titleLogoPanel == null || hasTitleLogoCanvasDefaultSortingOrder)
        {
            return;
        }

        titleLogoCanvas = titleLogoPanel.GetComponent<Canvas>();
        if (titleLogoCanvas == null)
        {
            titleLogoCanvas = titleLogoPanel.GetComponentInParent<Canvas>();
        }

        if (titleLogoCanvas == null)
        {
            return;
        }

        titleLogoCanvasDefaultSortingOrder = titleLogoCanvas.sortingOrder;
        hasTitleLogoCanvasDefaultSortingOrder = true;
    }

    private void ApplyTitleLogoCanvasSortingOrder(bool overlayOpen)
    {
        CacheTitleLogoCanvasSortingOrder();
        if (titleLogoCanvas == null || !hasTitleLogoCanvasDefaultSortingOrder)
        {
            return;
        }

        if (!overlayOpen)
        {
            titleLogoCanvas.sortingOrder = titleLogoCanvasDefaultSortingOrder;
            return;
        }

        titleLogoCanvas.sortingOrder = titleLogoCanvasDefaultSortingOrder;
        BringOpenOverlayPanelInFrontOfTitleLogo();
    }

    private void BringOpenOverlayPanelInFrontOfTitleLogo()
    {
        if (saveListPanel != null && saveListPanel.activeSelf)
        {
            BringPanelInFrontOfTitleLogo(saveListPanel);
        }

        if (quitConfirmPanel != null && quitConfirmPanel.activeSelf)
        {
            BringPanelInFrontOfTitleLogo(quitConfirmPanel);
        }
    }

    private void BringPanelInFrontOfTitleLogo(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        Canvas panelCanvas = panel.GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = panel.AddComponent<Canvas>();
        }

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = titleLogoCanvasDefaultSortingOrder + 1;

        if (!panel.TryGetComponent(out GraphicRaycaster _))
        {
            panel.AddComponent<GraphicRaycaster>();
        }
    }

    private void EnsureTitleRainActive()
    {
        ResolveRainReferences();

        if (titleRainRoot != null)
        {
            titleRainRoot.SetActive(true);
        }

        if (titleRainAudioSource != null && !titleRainAudioSource.isPlaying)
        {
            titleRainAudioSource.Play();
        }
    }

    private void SetTitleRainPausedForSaveList(bool paused)
    {
        ResolveRainReferences();

        if (titleRainRoot != null)
        {
            titleRainRoot.SetActive(!paused);
        }

        if (titleRainAudioSource == null)
        {
            return;
        }

        if (paused)
        {
            titleRainAudioWasPlaying = titleRainAudioSource.isPlaying;
            if (titleRainAudioWasPlaying)
            {
                titleRainAudioSource.Pause();
            }
        }
        else if (titleRainAudioWasPlaying)
        {
            titleRainAudioSource.UnPause();
        }
    }

    public void OnClickSaveSlot(int slotIndex)
    {
        RunAfterButtonFeedback(() => SelectSaveSlot(slotIndex));
    }

    public string GetStageDisplayName(string sceneName)
    {
        return GetStageDisplayName(sceneName, string.Empty);
    }

    public string GetStageDisplayName(string sceneName, string locationId)
    {
        if (locationDatabase != null &&
            locationDatabase.TryGetDisplayInfo(locationId, out LocationDisplayInfo locationInfo) &&
            !string.IsNullOrWhiteSpace(locationInfo.displayName))
        {
            return locationInfo.displayName;
        }

        StageDisplayInfo displayInfo = FindStageDisplayInfo(sceneName);
        if (!string.IsNullOrWhiteSpace(displayInfo.displayName))
        {
            return displayInfo.displayName;
        }

        return string.IsNullOrWhiteSpace(sceneName) ? "セーブデータなし" : sceneName;
    }

    public Sprite GetStageThumbnail(string sceneName)
    {
        return GetStageThumbnail(sceneName, string.Empty);
    }

    public Sprite GetStageThumbnail(string sceneName, string locationId)
    {
        if (locationDatabase != null &&
            locationDatabase.TryGetDisplayInfo(locationId, out LocationDisplayInfo locationInfo) &&
            locationInfo.thumbnail != null)
        {
            return locationInfo.thumbnail;
        }

        StageDisplayInfo displayInfo = FindStageDisplayInfo(sceneName);
        if (displayInfo.thumbnail != null)
        {
            return displayInfo.thumbnail;
        }

        return defaultStageThumbnail;
    }

    public Sprite GetSaveSlotThumbnail(int slotIndex, string sceneName, string locationId)
    {
        if (TryGetSavePreviewThumbnail(slotIndex, out Sprite previewThumbnail))
        {
            return previewThumbnail;
        }

        return GetStageThumbnail(sceneName, locationId);
    }

    public Sprite GetEmptySlotThumbnail()
    {
        return emptySlotThumbnail != null ? emptySlotThumbnail : defaultStageThumbnail;
    }

    private bool TryGetSavePreviewThumbnail(int slotIndex, out Sprite thumbnail)
    {
        if (savePreviewThumbnailCache.TryGetValue(slotIndex, out thumbnail))
        {
            return thumbnail != null;
        }

        if (!SaveRepository.TryLoadPreviewSprite(slotIndex, out thumbnail))
        {
            return false;
        }

        savePreviewThumbnailCache[slotIndex] = thumbnail;
        return thumbnail != null;
    }

    private void ClearSavePreviewThumbnailCache()
    {
        foreach (Sprite sprite in savePreviewThumbnailCache.Values)
        {
            DestroyRuntimePreviewSprite(sprite);
        }

        savePreviewThumbnailCache.Clear();
    }

    private static void DestroyRuntimePreviewSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        Texture texture = sprite.texture;
        if (Application.isPlaying)
        {
            Destroy(sprite);
            if (texture != null)
            {
                Destroy(texture);
            }
        }
        else
        {
            DestroyImmediate(sprite);
            if (texture != null)
            {
                DestroyImmediate(texture);
            }
        }
    }

    public void OnClickLoadConfirmYesButton()
    {
        RunAfterButtonFeedback(LoadSelectedSave);
    }

    private void LoadSelectedSave()
    {
        if (loadingGameScene)
        {
            return;
        }

        SetTitleRainPausedForSaveList(false);
        BeginLoadingScreen(
            SaveManager.LoadGameAsync(selectedSaveSlotIndex, gameSceneName),
            startsFromBlack: false);
    }

    public void OnClickLoadConfirmNoButton()
    {
        RunAfterButtonFeedback(HideLoadConfirmPanel);
    }

    private void HideLoadConfirmPanel()
    {
        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(false);
        }
    }

    private void RefreshSaveSlotViews()
    {
        if ((saveSlotViews == null || saveSlotViews.Length == 0) && saveListPanel != null)
        {
            saveSlotViews = saveListPanel.GetComponentsInChildren<TitleSaveSlotView>(true);
        }

        if (saveSlotViews == null)
        {
            return;
        }

        for (int i = 0; i < saveSlotViews.Length; i++)
        {
            if (saveSlotViews[i] != null)
            {
                saveSlotViews[i].Refresh(this);
            }
        }
    }

    private StageDisplayInfo FindStageDisplayInfo(string sceneName)
    {
        if (stageDisplayInfos == null || string.IsNullOrWhiteSpace(sceneName))
        {
            return default;
        }

        for (int i = 0; i < stageDisplayInfos.Length; i++)
        {
            if (stageDisplayInfos[i].Matches(sceneName))
            {
                return stageDisplayInfos[i];
            }
        }

        return default;
    }

    private void SelectSaveSlot(int slotIndex)
    {
        if (!SaveManager.HasSave(slotIndex))
        {
            return;
        }

        selectedSaveSlotIndex = slotIndex;
        if (loadConfirmPanel != null)
        {
            loadConfirmPanel.SetActive(true);
        }
    }

    private void BeginLoadingScreen(IEnumerator loadRoutine, bool startsFromBlack)
    {
        loadingGameScene = true;
        SetMainTitleButtonsInteractable(false);
        SetLoadingCursorHidden(true);

        TitleLoadingOverlay.Begin(CreateLoadingOverlaySettings(startsFromBlack), loadRoutine);
    }

    private TitleLoadingOverlay.Settings CreateLoadingOverlaySettings(bool startsFromBlack)
    {
        return new TitleLoadingOverlay.Settings
        {
            Text = string.IsNullOrWhiteSpace(loadingText) ? "Now Loading……" : loadingText,
            Font = ResolveLoadingFont(),
            FontSize = loadingFontSize,
            TextColor = loadingTextColor,
            TextAreaSize = loadingTextAreaSize,
            TextMargin = loadingTextMargin,
            InitialAlpha = startsFromBlack ? 1f : 0f,
            FadeInDuration = startsFromBlack ? 0f : continueFadeOutDuration,
            FadeOutDuration = ResolveLoadingFadeInDuration(),
            CursorHideOwner = loadingCursorHideOwner
        };
    }

    private float ResolveLoadingFadeInDuration()
    {
        if (loadingFadeOutDuration > 0f)
        {
            return loadingFadeOutDuration;
        }

        return Mathf.Max(0.1f, startRainFillDuration);
    }

    private TMP_FontAsset ResolveLoadingFont()
    {
        if (loadingFont != null)
        {
            return loadingFont;
        }

        TMP_Text sceneText = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return sceneText != null ? sceneText.font : null;
    }

    private void SetLoadingCursorHidden(bool hidden)
    {
        GameCursorController.SetCursorForcedHidden(loadingCursorHideOwner, hidden);
    }
}

[System.Serializable]
public struct StageDisplayInfo
{
    public string sceneName;
    public string displayName;
    public Sprite thumbnail;

    public bool Matches(string targetSceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) &&
               string.Equals(sceneName, targetSceneName, System.StringComparison.Ordinal);
    }
}

internal sealed class TitleLoadingOverlay : MonoBehaviour
{
    private const string RuntimeObjectName = "[TitleLoadingOverlay]";
    private const int SortingOrder = 32766;
    private const float DefaultPostLoadWaitTimeoutSeconds = 10f;

    private static TitleLoadingOverlay activeOverlay;

    private Settings settings;
    private CanvasGroup canvasGroup;
    private TMP_Text loadingLabel;

    public sealed class Settings
    {
        public string Text = "Now Loading……";
        public TMP_FontAsset Font;
        public float FontSize = 36f;
        public Color TextColor = Color.white;
        public Vector2 TextAreaSize = new Vector2(520f, 96f);
        public Vector2 TextMargin = new Vector2(72f, 56f);
        public float InitialAlpha = 1f;
        public float FadeInDuration;
        public float FadeOutDuration = 0.5f;
        public object CursorHideOwner;
    }

    public static void Begin(Settings settings, IEnumerator loadRoutine)
    {
        if (activeOverlay != null)
        {
            activeOverlay.ReleaseCursorHide();
            Destroy(activeOverlay.gameObject);
            activeOverlay = null;
        }

        GameObject overlayObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(overlayObject);

        activeOverlay = overlayObject.AddComponent<TitleLoadingOverlay>();
        activeOverlay.Configure(settings);
        activeOverlay.StartCoroutine(activeOverlay.Run(loadRoutine));
    }

    public static IEnumerator LoadSceneAsync(string sceneName)
    {
        string targetSceneName = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[TitleLoadingOverlay] Game scene name is empty.");
            yield break;
        }

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(targetSceneName);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[TitleLoadingOverlay] Failed to load scene '{targetSceneName}'. {exception}");
            yield break;
        }

        if (operation == null)
        {
            Debug.LogError($"[TitleLoadingOverlay] Failed to start loading scene '{targetSceneName}'.");
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private void Configure(Settings overlaySettings)
    {
        settings = overlaySettings ?? new Settings();
        ApplyCursorHide();
        BuildOverlay();
        canvasGroup.alpha = Mathf.Clamp01(settings.InitialAlpha);

        if (loadingLabel != null)
        {
            loadingLabel.gameObject.SetActive(settings.InitialAlpha >= 1f);
        }
    }

    private IEnumerator Run(IEnumerator loadRoutine)
    {
        BuildOverlay();

        if (canvasGroup.alpha < 1f)
        {
            yield return FadeCanvasGroup(1f, settings.FadeInDuration);
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        if (loadingLabel != null)
        {
            loadingLabel.gameObject.SetActive(true);
        }

        yield return null;

        if (loadRoutine != null)
        {
            yield return StartCoroutine(loadRoutine);
        }

        yield return WaitForSaveLoadRestore();
        yield return null;

        if (loadingLabel != null)
        {
            loadingLabel.gameObject.SetActive(false);
        }

        yield return FadeCanvasGroup(0f, settings.FadeOutDuration);
        ReleaseCursorHide();

        if (activeOverlay == this)
        {
            activeOverlay = null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ReleaseCursorHide();
    }

    private void ApplyCursorHide()
    {
        if (settings == null || settings.CursorHideOwner == null)
        {
            return;
        }

        GameCursorController.SetCursorForcedHidden(settings.CursorHideOwner, true);
    }

    private void ReleaseCursorHide()
    {
        if (settings == null || settings.CursorHideOwner == null)
        {
            return;
        }

        GameCursorController.SetCursorForcedHidden(settings.CursorHideOwner, false);
    }

    private IEnumerator WaitForSaveLoadRestore()
    {
        float elapsed = 0f;
        while (SaveManager.IsLoadInProgress && elapsed < DefaultPostLoadWaitTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha, float durationSeconds)
    {
        float startAlpha = canvasGroup.alpha;
        float duration = Mathf.Max(0f, durationSeconds);

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void BuildOverlay()
    {
        if (canvasGroup != null && loadingLabel != null)
        {
            return;
        }

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (!gameObject.TryGetComponent(out GraphicRaycaster _))
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        Image background = CreateImage("Background", transform, Color.black);
        Stretch(background.rectTransform);
        background.raycastTarget = true;

        GameObject labelObject = new GameObject("LoadingText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(1f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(1f, 0f);
        labelRect.sizeDelta = settings != null ? settings.TextAreaSize : new Vector2(520f, 96f);
        Vector2 margin = settings != null ? settings.TextMargin : new Vector2(72f, 56f);
        labelRect.anchoredPosition = new Vector2(-margin.x, margin.y);

        loadingLabel = labelObject.GetComponent<TMP_Text>();
        loadingLabel.text = settings != null && !string.IsNullOrWhiteSpace(settings.Text)
            ? settings.Text
            : "Now Loading……";
        loadingLabel.fontSize = settings != null ? settings.FontSize : 36f;
        loadingLabel.color = settings != null ? settings.TextColor : Color.white;
        loadingLabel.alignment = TextAlignmentOptions.BottomRight;
        loadingLabel.textWrappingMode = TextWrappingModes.NoWrap;
        loadingLabel.overflowMode = TextOverflowModes.Overflow;
        loadingLabel.raycastTarget = false;

        if (settings != null && settings.Font != null)
        {
            loadingLabel.font = settings.Font;
        }
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);

        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }
}
