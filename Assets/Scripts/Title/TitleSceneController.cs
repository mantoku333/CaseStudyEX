using System;
using System.Collections;
using System.Collections.Generic;
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

    private int selectedSaveSlotIndex = SaveManager.DefaultSlotIndex;
    private Coroutine delayedButtonActionRoutine;
    private readonly Dictionary<int, Sprite> savePreviewThumbnailCache = new Dictionary<int, Sprite>();
    private bool titleRainAudioWasPlaying;
    private bool startingNewGame;
    private bool titleLogoPanelDefaultActive = true;

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
        if (startingNewGame)
        {
            return;
        }

        BeginStartNewGameRainTransition();
    }

    private void BeginStartNewGameRainTransition()
    {
        if (startingNewGame)
        {
            return;
        }

        startingNewGame = true;
        SetMainTitleButtonsInteractable(false);
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
        SaveManager.DeleteSave();
        SaveManager.ClearAllFlags();
        SaveManager.ClearAllItems();
        CurrentLocationService.ClearCurrentLocation();
        ElegantPointWallet.Clear();

        SceneManager.LoadScene(gameSceneName);
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
            return;
        }

        GameObject logoOverlayCanvas = GameObject.Find("LogoOverlayCanvas");
        if (logoOverlayCanvas != null)
        {
            titleLogoPanel = logoOverlayCanvas;
            return;
        }

        titleLogoPanel = GameObject.Find("rogo");
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

        titleLogoPanel.SetActive(titleLogoPanelDefaultActive && !overlayOpen);
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
        if (!SaveManager.TryLoadGame(selectedSaveSlotIndex, gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
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
