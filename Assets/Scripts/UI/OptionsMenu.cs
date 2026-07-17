using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OptionsMenu : MonoBehaviour
{
    [Header("Option Header")]
    [SerializeField] private Vector2 optionHeaderSelectionMarkerOffset = new Vector2(0f, -23f);
    [SerializeField, Min(0f)] private float optionHeaderSelectionMarkerSlideDuration = 0.12f;

    private const string PlayerActionMapName = "Player";
    private const string KeyboardMouseGroup = "Keyboard&Mouse";
    private const string TitleSceneName = "Title";
    private const string BgmVolumeKey = "Options.BgmVolume";
    private const string SeVolumeKey = "Options.SeVolume";
    private const string SystemVolumeKey = "Options.SystemVolume";
    private const string MoveRightDefaultPath = "<Keyboard>/d";
    private const string MoveLeftDefaultPath = "<Keyboard>/a";
    private const string JumpDefaultPath = "<Keyboard>/space";
    private const string AttackDefaultPath = "<Mouse>/leftButton";
    private const string GlideDefaultPath = "<Mouse>/rightButton";
    private const string RecoilDefaultPath = "";
    private const string DodgeDefaultPath = "<Keyboard>/leftShift";
    private const float AudioRefreshInterval = 0.35f;
    private const float HandlePadding = 10f;
    private const float KeyboardScrollPixelsPerWheelTick = 56f;
    private const float KeyboardScrollbarMinHandleHeight = 44f;
    private const float ButtonActionDelay = 0.32f;
    private const int TopSortingOrder = 10000;
    private const int FullMapSortingOrder = TopSortingOrder + 10;

    private static readonly string[] PlayerControlBehaviourNames =
    {
        "PlayerController",
        "PlayerController_ozono",
        "PlayerPlatformerMockController",
        "DodgeController",
        "PlayerShooter",
        "GunController",
        "UmbrellaController",
        "UmbrellaAttackController",
        "UmbrellaParryController"
    };

    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private readonly Dictionary<int, float> capturedAudioBaseVolumes = new Dictionary<int, float>();
    private readonly Dictionary<int, float> lastAppliedAudioVolumes = new Dictionary<int, float>();
    private readonly HashSet<int> audioSourcesMutedByOptions = new HashSet<int>();
    private GameObject menuRoot;
    private GameObject optionPanel;
    private GameObject mainMenuPanel;
    private GameObject alternateMainMenuPanel;
    private GameObject optionDetailPanel;
    private GameObject finishPromptPanel;
    private GameObject soundContentPanel;
    private GameObject keyboardContentPanel;
    private GameObject decorationContentPanel;
    private GameObject optionHeaderCloseButtonObject;
    private GameObject optionHeaderPreviousButtonObject;
    private GameObject optionHeaderNextButtonObject;
    private GameObject optionHeaderBack;
    private GameObject optionHeaderBackground;
    private GameObject optionHeaderSelectionMarker;
    private GameObject mapTabNormal;
    private GameObject mapTabSelected;
    private GameObject decorationTabNormal;
    private GameObject decorationTabSelected;
    private GameObject noteTabNormal;
    private GameObject noteTabSelected;
    private GameObject settingsTabNormal;
    private GameObject settingsTabSelected;

    private Canvas rootCanvas;
    private GraphicRaycaster graphicRaycaster;

    private Button resumeButton;
    private Button saveButton;
    private Button mapButton;
    private Button optionButton;
    private Button titleButton;
    private Button soundTabButton;
    private Button keyboardTabButton;
    private Button soundTabButtonBase;
    private Button keyboardTabButtonBase;
    private Button backButton;
    private Button optionHeaderCloseButton;
    private Button optionHeaderPreviousButton;
    private Button optionHeaderNextButton;
    private Button mapTabNormalButton;
    private Button mapTabSelectedButton;
    private Button decorationTabNormalButton;
    private Button decorationTabSelectedButton;
    private Button noteTabNormalButton;
    private Button noteTabSelectedButton;
    private Button settingsTabNormalButton;
    private Button settingsTabSelectedButton;
    private Button rightMoveButton;
    private Button leftMoveButton;
    private Button jumpButton;
    private Button attackButton;
    private Button glideButton;
    private Button recoilButton;
    private Button parryButton;
    private Button dodgeButton;

    private RectTransform bgmBar;
    private RectTransform seBar;
    private RectTransform systemBar;
    private RectTransform bgmFill;
    private RectTransform seFill;
    private RectTransform systemFill;
    private RectTransform bgmHandle;
    private RectTransform seHandle;
    private RectTransform systemHandle;
    private RectTransform keyboardScrollViewport;
    private RectTransform keyboardScrollContent;
    private RectTransform keyboardScrollbarTrack;
    private RectTransform keyboardScrollbarHandle;

    private TextMeshProUGUI statusText;
    private TextMeshProUGUI keyboardStatusText;
    private TextMeshProUGUI bgmVolumeText;
    private TextMeshProUGUI seVolumeText;
    private TextMeshProUGUI systemVolumeText;
    private TextMeshProUGUI rightMoveValueText;
    private TextMeshProUGUI leftMoveValueText;
    private TextMeshProUGUI jumpValueText;
    private TextMeshProUGUI attackValueText;
    private TextMeshProUGUI glideValueText;
    private TextMeshProUGUI recoilValueText;
    private TextMeshProUGUI parryValueText;
    private TextMeshProUGUI dodgeValueText;
    private OptionsMainMenuSkin alternateMainMenuSkin;
    private OptionsFinishPromptSkin finishPromptSkin;

    private PlayerInput pausedPlayerInput;
    private MinimapManager cachedMinimapManager;
    private MinimapView cachedMinimapView;
    private InputActionRebindingExtensions.RebindingOperation activeRebindOperation;
    private InputAction activeRebindAction;
    private Button activeRebindButton;
    private bool previousPlayerInputEnabled;
    private bool previousMinimapManagerEnabled;
    private bool previousMinimapVisible;
    private bool previousFullMapVisible;
    private bool fullMapOpenedFromMenu;
    private bool minimapVisibleBeforeMenuMap;
    private bool hasMinimapVisibleBeforeMenuMap;
    private bool gameplayPaused;
    private bool isOpen;
    private bool cursorMenuModeActive;
    private bool listenersRegistered;
    private bool referencesResolved;
    private bool isRebinding;
    private bool isDraggingKeyboardScrollbar;
    private bool activeRebindAllowsMouse;
    private bool openFullMapAfterClose;
    private bool optionPageHeaderAvailable;
    private Coroutine delayedButtonActionRoutine;
    private float previousTimeScale = 1f;
    private Rigidbody2D pausedPlayerRigidbody;
    private Vector2 pausedPlayerLinearVelocity;
    private float pausedPlayerAngularVelocity;
    private bool hasPausedPlayerVelocity;
    private float nextAudioRefreshTime;
    private float statusHideAt;
    private float keyboardScrollNormalized;
    private int lastMapKeyboardRequestFrame = -1;
    private ScrollRect decorationScrollRect;
    private DecorationPage decorationPage;
    private string activeRebindPreviousOverridePath = string.Empty;
    private int activeRebindBindingIndex = -1;
    private ActiveVolumeBar activeVolumeBar;

    private enum AudioChannel
    {
        Bgm,
        Se,
        System
    }

    private enum ActiveVolumeBar
    {
        None,
        Bgm,
        Se,
        System
    }

    private enum OptionPage
    {
        Map,
        Decoration,
        Note,
        Settings
    }

    private OptionPage currentOptionPage = OptionPage.Settings;
    private bool optionHeaderSelectionMarkerAnimating;
    private Vector2 optionHeaderSelectionMarkerAnimationStart;
    private Vector2 optionHeaderSelectionMarkerAnimationTarget;
    private float optionHeaderSelectionMarkerAnimationElapsed;

    private void Awake()
    {
        ResolveReferences();
        RegisterListeners();
        InitializeViewState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RegisterListeners();
        InitializeViewState();
        ApplyAudioSettingsToScene(forceRefreshAll: true);
    }

    private void OnDisable()
    {
        StopDelayedButtonAction();
        SetCursorMenuModeActive(false);
        DisposeActiveRebindOperation();
        activeVolumeBar = ActiveVolumeBar.None;
        isDraggingKeyboardScrollbar = false;
        RestoreGameplayState();
    }

    private void OnDestroy()
    {
        StopDelayedButtonAction();
        SetCursorMenuModeActive(false);
        DisposeActiveRebindOperation();
        UnregisterListeners();
        RestoreGameplayState();
    }

    private void OnValidate()
    {
        if (optionHeaderSelectionMarker != null && optionHeaderSelectionMarker.activeSelf)
        {
            UpdateOptionHeaderSelectionMarker(true);
        }
    }

    private void Update()
    {
        if (!referencesResolved)
        {
            return;
        }

        if (!isRebinding && ShouldToggleMenu())
        {
            HandleToggleRequest();
            UIButtonSfxPlayer.PlayClick();
        }

        if (!isRebinding)
        {
            HandleOptionPageKeyboardInput();
        }

        if (statusHideAt > 0f && Time.unscaledTime >= statusHideAt && statusText != null)
        {
            statusHideAt = 0f;
            statusText.text = string.Empty;
        }

        HandleVolumePointerInteraction();
        HandleKeyboardScrollInteraction();

        if (Time.unscaledTime >= nextAudioRefreshTime)
        {
            ApplyAudioSettingsToScene(forceRefreshAll: false);
            nextAudioRefreshTime = Time.unscaledTime + AudioRefreshInterval;
        }
    }

    private void LateUpdate()
    {
        if (!referencesResolved)
        {
            return;
        }

        RefreshOptionHeaderSelectionMarkerPosition();

        // 0設定中は、そのフレームに新規生成・再設定されたAudioSourceも音声出力前にmuteする。
        if (ReadVolume(SeVolumeKey, 1f) <= 0.0001f ||
            ReadVolume(SystemVolumeKey, 1f) <= 0.0001f)
        {
            ApplyAudioSettingsToScene(forceRefreshAll: false);
        }
    }

    private void ResolveReferences()
    {
        if (referencesResolved)
        {
            return;
        }

        rootCanvas = GetComponent<Canvas>();
        graphicRaycaster = GetComponent<GraphicRaycaster>();
        EnsureTopSortingOrder();

        menuRoot = FindChildObject("MenuRoot");
        optionPanel = FindChildObject("MenuRoot/OptionPanel");
        mainMenuPanel = FindChildObject("MenuRoot/OptionPanel/MainMenuPanel");
        alternateMainMenuPanel = FindChildObject("MenuRoot/AlternateMainMenuPanel");
        optionDetailPanel = FindChildObject("MenuRoot/OptionPanel/OptionDetailPanel");
        finishPromptPanel = FindChildObject("MenuRoot/FinishPrompt");
        soundContentPanel = FindChildObject("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel");
        keyboardContentPanel = FindChildObject("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel");
        decorationContentPanel = FindChildObject("MenuRoot/OptionPanel/Decoration");
        decorationScrollRect = FindChildComponent<ScrollRect>("MenuRoot/OptionPanel/Decoration/Item back ground");
        decorationPage = decorationContentPanel != null ? decorationContentPanel.GetComponent<DecorationPage>() : null;
        optionHeaderCloseButtonObject = FindChildObject("MenuRoot/OptionPanel/Back Button");
        optionHeaderPreviousButtonObject = FindChildObject("MenuRoot/OptionPanel/Q");
        optionHeaderNextButtonObject = FindChildObject("MenuRoot/OptionPanel/E");
        optionHeaderBack = FindChildObject("MenuRoot/OptionPanel/back");
        optionHeaderBackground = FindChildObject("MenuRoot/OptionPanel/Back ground 1_0");
        optionHeaderSelectionMarker = FindChildObject("MenuRoot/OptionPanel/Select 2_0");
        mapTabNormal = FindChildObject("MenuRoot/OptionPanel/Map No Select 1_0");
        mapTabSelected = FindChildObject("MenuRoot/OptionPanel/Map Select 1_0");
        decorationTabNormal = FindChildObject("MenuRoot/OptionPanel/Decoration No Select 1_0");
        decorationTabSelected = FindChildObject("MenuRoot/OptionPanel/Decoration Select 1_0");
        noteTabNormal = FindChildObject("MenuRoot/OptionPanel/Note No Select 1_0");
        noteTabSelected = FindChildObject("MenuRoot/OptionPanel/Note Select 1_0");
        settingsTabNormal = FindChildObject("MenuRoot/OptionPanel/Option text No select 1_0");
        settingsTabSelected = FindChildObject("MenuRoot/OptionPanel/Option text select 1_0");
        alternateMainMenuSkin = alternateMainMenuPanel != null
            ? alternateMainMenuPanel.GetComponent<OptionsMainMenuSkin>()
            : null;
        finishPromptSkin = finishPromptPanel != null
            ? finishPromptPanel.GetComponent<OptionsFinishPromptSkin>()
            : null;

        resumeButton = FindChildComponent<Button>("MenuRoot/OptionPanel/MainMenuPanel/ResumeButton");
        saveButton = FindChildComponent<Button>("MenuRoot/OptionPanel/MainMenuPanel/SaveButton");
        mapButton = FindChildComponent<Button>("MenuRoot/OptionPanel/MainMenuPanel/MapButton");
        optionButton = FindChildComponent<Button>("MenuRoot/OptionPanel/MainMenuPanel/OptionButton");
        titleButton = FindChildComponent<Button>("MenuRoot/OptionPanel/MainMenuPanel/TitleButton");
        soundTabButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/SoundTabButton");
        keyboardTabButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/KeyboardTabButton");
        soundTabButtonBase = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/SoundTabButton (1)");
        keyboardTabButtonBase = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/KeyboardTabButton (1)");
        backButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/BackButton");
        optionHeaderCloseButton = EnsureRuntimeButton(optionHeaderCloseButtonObject, "Back Button");
        OptionsCanvasButtonUtility.ConfigureIllustrationOnly(optionHeaderPreviousButtonObject);
        OptionsCanvasButtonUtility.ConfigureIllustrationOnly(optionHeaderNextButtonObject);
        optionHeaderPreviousButton = null;
        optionHeaderNextButton = null;
        mapTabNormalButton = EnsureRuntimeButton(mapTabNormal, "Map", useDimHover: false);
        mapTabSelectedButton = EnsureRuntimeButton(mapTabSelected, "Map", useDimHover: false);
        decorationTabNormalButton = EnsureRuntimeButton(decorationTabNormal, "Decoration", useDimHover: false);
        decorationTabSelectedButton = EnsureRuntimeButton(decorationTabSelected, "Decoration", useDimHover: false);
        noteTabNormalButton = EnsureRuntimeButton(noteTabNormal, "Note", useDimHover: false);
        noteTabSelectedButton = EnsureRuntimeButton(noteTabSelected, "Note", useDimHover: false);
        settingsTabNormalButton = EnsureRuntimeButton(settingsTabNormal, "Option text", useDimHover: false);
        settingsTabSelectedButton = EnsureRuntimeButton(settingsTabSelected, "Option text", useDimHover: false);
        rightMoveButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/RightMoveRow/ValueButton");
        leftMoveButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/LeftMoveRow/ValueButton");
        jumpButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/JumpRow/ValueButton");
        attackButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/AttackRow/ValueButton");
        glideButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/GlideRow/ValueButton");
        recoilButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/RecoilRow/ValueButton");
        parryButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/ParryRow/ValueButton");
        dodgeButton = FindChildComponent<Button>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/DodgeRow/ValueButton");

        bgmBar = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/BgmBar");
        seBar = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SeBar");
        systemBar = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SystemBar");
        bgmFill = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/BgmBar/Fill");
        seFill = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SeBar/Fill");
        systemFill = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SystemBar/Fill");
        bgmHandle = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/BgmBar/Handle");
        seHandle = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SeBar/Handle");
        systemHandle = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SystemBar/Handle");
        keyboardScrollViewport = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport");
        keyboardScrollContent = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content");
        keyboardScrollbarTrack = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollbarTrack");
        keyboardScrollbarHandle = FindChildComponent<RectTransform>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollbarTrack/Handle");
        HideKeyboardScrollbarGraphics();

        statusText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/MainMenuPanel/StatusText");
        bgmVolumeText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/BgmVolumeText");
        seVolumeText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SeVolumeText");
        systemVolumeText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/SoundContentPanel/SystemVolumeText");
        keyboardStatusText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/KeyboardStatusText");
        rightMoveValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/RightMoveRow/ValueButton/Label");
        leftMoveValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/LeftMoveRow/ValueButton/Label");
        jumpValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/JumpRow/ValueButton/Label");
        attackValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/AttackRow/ValueButton/Label");
        glideValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/GlideRow/ValueButton/Label");
        recoilValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/RecoilRow/ValueButton/Label");
        parryValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/ParryRow/ValueButton/Label");
        dodgeValueText = FindChildComponent<TextMeshProUGUI>("MenuRoot/OptionPanel/OptionDetailPanel/ContentFrame/KeyboardContentPanel/ScrollViewport/Content/DodgeRow/ValueButton/Label");
        optionPageHeaderAvailable =
            optionHeaderPreviousButtonObject != null &&
            optionHeaderNextButtonObject != null &&
            optionHeaderCloseButtonObject != null &&
            optionHeaderBack != null &&
            optionHeaderBackground != null &&
            optionHeaderSelectionMarker != null &&
            mapTabNormal != null &&
            mapTabSelected != null &&
            decorationTabNormal != null &&
            decorationTabSelected != null &&
            noteTabNormal != null &&
            noteTabSelected != null &&
            settingsTabNormal != null &&
            settingsTabSelected != null;

        if (!optionPageHeaderAvailable)
        {
            Debug.LogWarning("[OptionsMenu] Option page header unavailable. Missing: " + BuildMissingHeaderSummary());
        }

        bool hasLegacyMainMenu =
            mainMenuPanel != null &&
            resumeButton != null &&
            saveButton != null &&
            mapButton != null &&
            optionButton != null &&
            titleButton != null;
        bool hasMainMenu = alternateMainMenuPanel != null || hasLegacyMainMenu;

        referencesResolved =
            menuRoot != null &&
            optionPanel != null &&
            hasMainMenu &&
            optionDetailPanel != null &&
            soundContentPanel != null &&
            keyboardContentPanel != null &&
            soundTabButton != null &&
            keyboardTabButton != null &&
            backButton != null &&
            rightMoveButton != null &&
            leftMoveButton != null &&
            jumpButton != null &&
            attackButton != null &&
            glideButton != null &&
            dodgeButton != null &&
            bgmBar != null &&
            seBar != null &&
            systemBar != null &&
            bgmHandle != null &&
            seHandle != null &&
            systemHandle != null &&
            keyboardScrollViewport != null &&
            keyboardScrollContent != null &&
            keyboardScrollbarTrack != null &&
            keyboardStatusText != null &&
            rightMoveValueText != null &&
            leftMoveValueText != null &&
            jumpValueText != null &&
            attackValueText != null &&
            glideValueText != null &&
            dodgeValueText != null;

        if (!referencesResolved)
        {
            Debug.LogWarning("[OptionsMenu] UI references are incomplete. Missing: " + BuildMissingReferencesSummary());
        }
    }

    private void RegisterListeners()
    {
        if (!referencesResolved || listenersRegistered)
        {
            return;
        }

        MinimapManager.MapKeyRequested += OpenMapFromKeyboard;
        BindButton(resumeButton, RequestCloseMenu);
        BindButton(saveButton, SaveCurrentGame);
        BindButton(mapButton, RequestOpenMap);
        BindButton(optionButton, RequestShowOptionDetail);
        BindButton(titleButton, RequestShowFinishPrompt);
        BindButton(soundTabButton, ShowSoundTab, useDimHover: false);
        BindButton(keyboardTabButton, ShowKeyboardTab, useDimHover: false);
        BindButton(soundTabButtonBase, ShowSoundTab, useDimHover: false);
        BindButton(keyboardTabButtonBase, ShowKeyboardTab, useDimHover: false);
        BindButton(backButton, DoNothing);
        BindButton(optionHeaderCloseButton, RequestShowMainMenu);
        BindButton(mapTabNormalButton, ShowMapOptionPage, useDimHover: false);
        BindButton(mapTabSelectedButton, ShowMapOptionPage, useDimHover: false);
        BindButton(decorationTabNormalButton, ShowDecorationOptionPage, useDimHover: false);
        BindButton(decorationTabSelectedButton, ShowDecorationOptionPage, useDimHover: false);
        BindButton(noteTabNormalButton, ShowNoteOptionPage, useDimHover: false);
        BindButton(noteTabSelectedButton, ShowNoteOptionPage, useDimHover: false);
        BindButton(settingsTabNormalButton, ShowSettingsOptionPage, useDimHover: false);
        BindButton(settingsTabSelectedButton, ShowSettingsOptionPage, useDimHover: false);
        BindButton(rightMoveButton, StartRebindRightMove);
        BindButton(leftMoveButton, StartRebindLeftMove);
        BindButton(jumpButton, StartRebindJump);
        BindButton(attackButton, StartRebindAttack);
        BindButton(glideButton, StartRebindGlide);
        BindButton(dodgeButton, StartRebindDodge);

        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        MinimapManager.MapKeyRequested -= OpenMapFromKeyboard;
        UnbindButton(resumeButton, RequestCloseMenu);
        UnbindButton(saveButton, SaveCurrentGame);
        UnbindButton(mapButton, RequestOpenMap);
        UnbindButton(optionButton, RequestShowOptionDetail);
        UnbindButton(titleButton, RequestShowFinishPrompt);
        UnbindButton(soundTabButton, ShowSoundTab);
        UnbindButton(keyboardTabButton, ShowKeyboardTab);
        UnbindButton(soundTabButtonBase, ShowSoundTab);
        UnbindButton(keyboardTabButtonBase, ShowKeyboardTab);
        UnbindButton(backButton, DoNothing);
        UnbindButton(optionHeaderCloseButton, RequestShowMainMenu);
        UnbindButton(optionHeaderPreviousButton, ShowPreviousOptionPage);
        UnbindButton(optionHeaderNextButton, ShowNextOptionPage);
        UnbindButton(mapTabNormalButton, ShowMapOptionPage);
        UnbindButton(mapTabSelectedButton, ShowMapOptionPage);
        UnbindButton(decorationTabNormalButton, ShowDecorationOptionPage);
        UnbindButton(decorationTabSelectedButton, ShowDecorationOptionPage);
        UnbindButton(noteTabNormalButton, ShowNoteOptionPage);
        UnbindButton(noteTabSelectedButton, ShowNoteOptionPage);
        UnbindButton(settingsTabNormalButton, ShowSettingsOptionPage);
        UnbindButton(settingsTabSelectedButton, ShowSettingsOptionPage);
        UnbindButton(rightMoveButton, StartRebindRightMove);
        UnbindButton(leftMoveButton, StartRebindLeftMove);
        UnbindButton(jumpButton, StartRebindJump);
        UnbindButton(attackButton, StartRebindAttack);
        UnbindButton(glideButton, StartRebindGlide);
        UnbindButton(dodgeButton, StartRebindDodge);

        listenersRegistered = false;
    }

    private void InitializeViewState()
    {
        if (!referencesResolved)
        {
            return;
        }

        ApplySavedVolumeValues();
        RefreshVolumeVisuals();
        RefreshKeyboardBindings();
        ResetKeyboardScroll();
        SetRebindButtonsInteractable(true);
        ClearKeyboardStatus();
        ShowMainMenu();
        SetMenuVisible(false);
        SetFinishPromptVisible(false);
        if (statusText != null)
        {
            statusText.text = string.Empty;
        }
    }

    public void ShowOptionDetail()
    {
        if (isRebinding)
        {
            return;
        }

        ResolveReferences();
        if (optionPanel == null || optionDetailPanel == null)
        {
            Debug.LogWarning("[OptionsMenu] Cannot show option detail because OptionPanel or OptionDetailPanel is missing.");
            return;
        }

        if (!gameplayPaused)
        {
            PauseGameplay();
        }

        SetMenuVisible(true);
        SetCursorMenuModeActive(true);
        isOpen = true;
        SetFinishPromptVisible(false);
        SetOptionPanelVisible(true);
        optionPanel.transform.SetAsLastSibling();
        SetMainMenuPanelVisible(false);
        SetAlternateMainMenuVisible(false);
        SetOptionPageHeaderVisible(true);
        ShowOptionPage(OptionPage.Settings);
    }

    public void RequestShowOptionDetail()
    {
        if (!referencesResolved || isRebinding)
        {
            return;
        }

        RunAfterButtonFeedback(ShowOptionDetail);
    }

    public void ShowMainMenu()
    {
        if (isRebinding)
        {
            return;
        }

        ResolveReferences();
        if (optionPanel == null && alternateMainMenuPanel == null)
        {
            Debug.LogWarning("[OptionsMenu] Cannot show main menu because no menu panel is available.");
            return;
        }

        bool useAlternateMainMenu = alternateMainMenuPanel != null;
        SetFinishPromptVisible(false);
        SetOptionPanelVisible(!useAlternateMainMenu);
        SetMainMenuPanelVisible(!useAlternateMainMenu);
        SetAlternateMainMenuVisible(useAlternateMainMenu);
        SetOptionPageHeaderVisible(false);
        SetActiveIfChanged(optionDetailPanel, false);
        SetActiveIfChanged(decorationContentPanel, false);
        HideMapPanelsWhileMenuIsOpen();

        if (alternateMainMenuSkin != null)
        {
            if (isOpen)
            {
                alternateMainMenuSkin.SelectDefaultButton();
            }
            else
            {
                alternateMainMenuSkin.SelectOptionButton();
            }

            return;
        }

        SelectButton(isOpen ? resumeButton : optionButton);
    }

    public void RequestShowMainMenu()
    {
        if (!referencesResolved || isRebinding)
        {
            return;
        }

        RunAfterButtonFeedback(ShowMainMenu);
    }

    private void ShowMapOptionPage()
    {
        ShowOptionPage(OptionPage.Map);
    }

    private void ShowDecorationOptionPage()
    {
        ShowOptionPage(OptionPage.Decoration);
    }

    private void ShowNoteOptionPage()
    {
        ShowOptionPage(OptionPage.Note);
    }

    private void ShowSettingsOptionPage()
    {
        ShowOptionPage(OptionPage.Settings);
    }

    private void ShowPreviousOptionPage()
    {
        MoveOptionPage(-1);
    }

    private void ShowNextOptionPage()
    {
        MoveOptionPage(1);
    }

    private void MoveOptionPage(int direction)
    {
        if (!referencesResolved || !isOpen || !IsOptionDetailVisible())
        {
            return;
        }

        int pageCount = Enum.GetValues(typeof(OptionPage)).Length;
        int nextIndex = ((int)currentOptionPage + direction + pageCount) % pageCount;
        ShowOptionPage((OptionPage)nextIndex);
    }

    private void ShowOptionPage(OptionPage page)
    {
        ResolveReferences();
        if (optionPanel == null)
        {
            return;
        }

        bool animateSelectionMarker =
            optionPageHeaderAvailable &&
            isOpen &&
            IsOptionDetailVisible() &&
            optionHeaderSelectionMarker != null &&
            optionHeaderSelectionMarker.activeSelf &&
            currentOptionPage != page;

        currentOptionPage = page;
        SetOptionPanelVisible(true);
        optionPanel.transform.SetAsLastSibling();
        SetMainMenuPanelVisible(false);
        SetAlternateMainMenuVisible(false);
        SetOptionPageHeaderVisible(true, updateMarker: !animateSelectionMarker);
        SetOptionTabPair(mapTabNormal, mapTabSelected, page == OptionPage.Map);
        SetOptionTabPair(decorationTabNormal, decorationTabSelected, page == OptionPage.Decoration);
        SetOptionTabPair(noteTabNormal, noteTabSelected, page == OptionPage.Note);
        SetOptionTabPair(settingsTabNormal, settingsTabSelected, page == OptionPage.Settings);
        UpdateOptionHeaderSelectionMarker(true, animateSelectionMarker);

        bool showMapPage = page == OptionPage.Map;
        bool showSettingsPage = page == OptionPage.Settings;
        bool showDecorationPage = page == OptionPage.Decoration;
        if (cachedMinimapView != null)
        {
            if (showMapPage)
            {
                cachedMinimapView.SetFullMapSortingOrder(FullMapSortingOrder);
            }
            else
            {
                cachedMinimapView.RestoreFullMapSortingOrder();
            }

            cachedMinimapView.SetFullMapVisible(showMapPage);
        }
        SetActiveIfChanged(optionDetailPanel, showSettingsPage);
        SetActiveIfChanged(decorationContentPanel, showDecorationPage);
        if (showSettingsPage)
        {
            ShowSoundTab();
        }
        else if (showDecorationPage)
        {
            activeVolumeBar = ActiveVolumeBar.None;
            isDraggingKeyboardScrollbar = false;
            ClearKeyboardStatus();
            decorationPage?.Refresh();
            if (decorationScrollRect != null)
            {
                decorationScrollRect.verticalNormalizedPosition = 1f;
            }
        }
        else
        {
            activeVolumeBar = ActiveVolumeBar.None;
            isDraggingKeyboardScrollbar = false;
            ClearKeyboardStatus();
        }
    }

    private void HandleOptionPageKeyboardInput()
    {
        if (!referencesResolved || !isOpen || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (HandleMapKeyboardRequest())
            {
                UIButtonSfxPlayer.PlayClick();
            }
            return;
        }

        if (!IsOptionDetailVisible())
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ShowPreviousOptionPage();
            UIButtonSfxPlayer.PlayClick();
        }
        else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            ShowNextOptionPage();
            UIButtonSfxPlayer.PlayClick();
        }
    }

    private bool IsOptionDetailVisible()
    {
        return optionPanel != null &&
               optionPanel.activeInHierarchy &&
               !IsMainMenuPanelVisible() &&
               (optionPageHeaderAvailable || optionDetailPanel.activeSelf);
    }

    private bool IsMainMenuPanelVisible()
    {
        return mainMenuPanel != null && mainMenuPanel.activeSelf;
    }

    private void SetOptionPageHeaderVisible(bool visible, bool updateMarker = true)
    {
        if (!optionPageHeaderAvailable)
        {
            return;
        }

        SetActiveIfChanged(mapTabNormal, visible && currentOptionPage != OptionPage.Map);
        SetActiveIfChanged(mapTabSelected, visible && currentOptionPage == OptionPage.Map);
        SetActiveIfChanged(decorationTabNormal, visible && currentOptionPage != OptionPage.Decoration);
        SetActiveIfChanged(decorationTabSelected, visible && currentOptionPage == OptionPage.Decoration);
        SetActiveIfChanged(noteTabNormal, visible && currentOptionPage != OptionPage.Note);
        SetActiveIfChanged(noteTabSelected, visible && currentOptionPage == OptionPage.Note);
        SetActiveIfChanged(settingsTabNormal, visible && currentOptionPage != OptionPage.Settings);
        SetActiveIfChanged(settingsTabSelected, visible && currentOptionPage == OptionPage.Settings);

        SetActiveIfChanged(optionHeaderCloseButtonObject, visible);
        SetActiveIfChanged(optionHeaderPreviousButtonObject, visible);
        SetActiveIfChanged(optionHeaderNextButtonObject, visible);
        SetActiveIfChanged(optionHeaderBack, visible);
        SetActiveIfChanged(optionHeaderBackground, visible);
        if (updateMarker)
        {
            UpdateOptionHeaderSelectionMarker(visible);
        }

        if (visible)
        {
            RefreshOptionHeaderSiblingOrder();
            RefreshOptionHeaderSpriteSorting();
        }
    }

    private void HideMapPanelsWhileMenuIsOpen()
    {
        if (cachedMinimapView != null)
        {
            cachedMinimapView.SetPanelVisibility(false, false);
            cachedMinimapView.RestoreFullMapSortingOrder();
        }
    }

    private void RefreshOptionHeaderSelectionMarkerPosition()
    {
        if (isOpen && IsOptionDetailVisible() && optionHeaderSelectionMarker != null && optionHeaderSelectionMarker.activeSelf)
        {
            if (optionHeaderSelectionMarkerAnimating)
            {
                AdvanceOptionHeaderSelectionMarkerAnimation();
            }
            else
            {
                UpdateOptionHeaderSelectionMarker(true);
            }
        }
    }

    private void SetOptionTabPair(GameObject normalState, GameObject selectedState, bool selected)
    {
        SetActiveIfChanged(normalState, !selected);
        SetActiveIfChanged(selectedState, selected);
    }

    private void UpdateOptionHeaderSelectionMarker(bool visible, bool animate = false)
    {
        if (optionHeaderSelectionMarker == null)
        {
            return;
        }

        SetActiveIfChanged(optionHeaderSelectionMarker, visible);
        if (!visible)
        {
            optionHeaderSelectionMarkerAnimating = false;
            return;
        }

        Transform markerTransform = optionHeaderSelectionMarker.transform;
        Transform targetTransform = GetSelectedOptionHeaderTarget();
        if (markerTransform == null || targetTransform == null)
        {
            return;
        }

        RectTransform markerRectTransform = markerTransform as RectTransform;
        RectTransform targetRectTransform = targetTransform as RectTransform;
        if (markerRectTransform != null && targetRectTransform != null)
        {
            Vector2 markerTargetPosition = targetRectTransform.anchoredPosition + optionHeaderSelectionMarkerOffset;
            if (animate && optionHeaderSelectionMarkerSlideDuration > 0f)
            {
                StartOptionHeaderSelectionMarkerAnimation(markerRectTransform, markerTargetPosition);
            }
            else
            {
                optionHeaderSelectionMarkerAnimating = false;
                markerRectTransform.anchoredPosition = markerTargetPosition;
            }
            return;
        }

        Vector3 fallbackTargetPosition = targetTransform.localPosition;
        optionHeaderSelectionMarkerAnimating = false;
        markerTransform.localPosition = new Vector3(
            fallbackTargetPosition.x + optionHeaderSelectionMarkerOffset.x,
            fallbackTargetPosition.y + optionHeaderSelectionMarkerOffset.y,
            markerTransform.localPosition.z);
    }

    private void StartOptionHeaderSelectionMarkerAnimation(RectTransform marker, Vector2 targetPosition)
    {
        if ((marker.anchoredPosition - targetPosition).sqrMagnitude <= 0.01f)
        {
            optionHeaderSelectionMarkerAnimating = false;
            marker.anchoredPosition = targetPosition;
            return;
        }

        optionHeaderSelectionMarkerAnimationStart = marker.anchoredPosition;
        optionHeaderSelectionMarkerAnimationTarget = targetPosition;
        optionHeaderSelectionMarkerAnimationElapsed = 0f;
        optionHeaderSelectionMarkerAnimating = true;
    }

    private void AdvanceOptionHeaderSelectionMarkerAnimation()
    {
        if (optionHeaderSelectionMarker == null)
        {
            optionHeaderSelectionMarkerAnimating = false;
            return;
        }

        RectTransform marker = optionHeaderSelectionMarker.transform as RectTransform;
        if (marker == null)
        {
            optionHeaderSelectionMarkerAnimating = false;
            return;
        }

        optionHeaderSelectionMarkerAnimationElapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.0001f, optionHeaderSelectionMarkerSlideDuration);
        float t = Mathf.Clamp01(optionHeaderSelectionMarkerAnimationElapsed / duration);
        float eased = t * t * (3f - (2f * t));
        marker.anchoredPosition = Vector2.LerpUnclamped(
            optionHeaderSelectionMarkerAnimationStart,
            optionHeaderSelectionMarkerAnimationTarget,
            eased);

        if (t >= 1f)
        {
            marker.anchoredPosition = optionHeaderSelectionMarkerAnimationTarget;
            optionHeaderSelectionMarkerAnimating = false;
        }
    }

    private Transform GetSelectedOptionHeaderTarget()
    {
        switch (currentOptionPage)
        {
            case OptionPage.Map:
                return mapTabSelected != null ? mapTabSelected.transform : mapTabNormal != null ? mapTabNormal.transform : null;
            case OptionPage.Decoration:
                return decorationTabSelected != null ? decorationTabSelected.transform : decorationTabNormal != null ? decorationTabNormal.transform : null;
            case OptionPage.Note:
                return noteTabSelected != null ? noteTabSelected.transform : noteTabNormal != null ? noteTabNormal.transform : null;
            case OptionPage.Settings:
                return settingsTabSelected != null ? settingsTabSelected.transform : settingsTabNormal != null ? settingsTabNormal.transform : null;
            default:
                return null;
        }
    }

    private static void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void RefreshOptionHeaderSiblingOrder()
    {
        SetAsFirstSibling(optionHeaderBack);
        SetAsFirstSibling(optionHeaderBackground);
        SetAsLastSibling(optionDetailPanel);
        SetAsLastSibling(optionHeaderSelectionMarker);
        SetAsLastSibling(mapTabNormal);
        SetAsLastSibling(mapTabSelected);
        SetAsLastSibling(decorationTabNormal);
        SetAsLastSibling(decorationTabSelected);
        SetAsLastSibling(noteTabNormal);
        SetAsLastSibling(noteTabSelected);
        SetAsLastSibling(settingsTabNormal);
        SetAsLastSibling(settingsTabSelected);
        SetAsLastSibling(optionHeaderPreviousButtonObject);
        SetAsLastSibling(optionHeaderNextButtonObject);
        SetAsLastSibling(optionHeaderCloseButtonObject);
    }

    private static void SetAsFirstSibling(GameObject target)
    {
        if (target != null)
        {
            target.transform.SetAsFirstSibling();
        }
    }

    private static void SetAsLastSibling(GameObject target)
    {
        if (target != null)
        {
            target.transform.SetAsLastSibling();
        }
    }

    private void RefreshOptionHeaderSpriteSorting()
    {
        SetSpriteSortingOrder(optionHeaderBackground, -30);
        SetSpriteSortingOrder(optionHeaderBack, -20);
        SetSpriteSortingOrder(optionHeaderSelectionMarker, 45);
        SetSpriteSortingOrder(mapTabNormal, 50);
        SetSpriteSortingOrder(mapTabSelected, 50);
        SetSpriteSortingOrder(decorationTabNormal, 50);
        SetSpriteSortingOrder(decorationTabSelected, 50);
        SetSpriteSortingOrder(noteTabNormal, 50);
        SetSpriteSortingOrder(noteTabSelected, 50);
        SetSpriteSortingOrder(settingsTabNormal, 50);
        SetSpriteSortingOrder(settingsTabSelected, 50);
        SetSpriteSortingOrder(optionHeaderPreviousButtonObject, 60);
        SetSpriteSortingOrder(optionHeaderNextButtonObject, 60);
        SetSpriteSortingOrder(optionHeaderCloseButtonObject, 70);
    }

    private static void SetSpriteSortingOrder(GameObject target, int sortingOrder)
    {
        if (target == null)
        {
            return;
        }

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].sortingOrder = sortingOrder;
            }
        }
    }

    private void ShowSoundTab()
    {
        if (isRebinding || soundContentPanel == null || keyboardContentPanel == null)
        {
            return;
        }

        soundContentPanel.SetActive(true);
        soundContentPanel.transform.SetAsLastSibling();
        keyboardContentPanel.SetActive(false);
        isDraggingKeyboardScrollbar = false;
        ClearKeyboardStatus();
        SetTabVisualState(soundTabButton, true);
        SetTabVisualState(keyboardTabButton, false);
        RefreshVolumeVisuals();
        SelectButton(soundTabButton);
    }

    private void ShowKeyboardTab()
    {
        if (isRebinding || soundContentPanel == null || keyboardContentPanel == null)
        {
            return;
        }

        soundContentPanel.SetActive(false);
        keyboardContentPanel.SetActive(true);
        keyboardContentPanel.transform.SetAsLastSibling();
        SetTabVisualState(soundTabButton, false);
        SetTabVisualState(keyboardTabButton, true);
        RefreshKeyboardBindings();
        ResetKeyboardScroll();
        ClearKeyboardStatus();
        SelectButton(rightMoveButton);
    }

    private void OpenMenu()
    {
        if (isOpen || !referencesResolved)
        {
            return;
        }

        PauseGameplay();
        RefreshKeyboardBindings();
        RefreshVolumeVisuals();
        ResetKeyboardScroll();
        ClearKeyboardStatus();
        ShowMainMenu();
        SetCursorMenuModeActive(true);
        SetMenuVisible(true);
        isOpen = true;

        if (alternateMainMenuSkin != null)
        {
            alternateMainMenuSkin.SelectDefaultButton();
        }
        else
        {
            SelectButton(resumeButton);
        }
    }

    public void CloseMenu()
    {
        if (!isOpen || !referencesResolved || isRebinding)
        {
            return;
        }

        SetMenuVisible(false);
        SetCursorMenuModeActive(false);
        isOpen = false;
        activeVolumeBar = ActiveVolumeBar.None;
        isDraggingKeyboardScrollbar = false;
        statusHideAt = 0f;
        ClearKeyboardStatus();
        SetMainMenuPanelVisible(false);
        SetOptionPanelVisible(false);
        SetAlternateMainMenuVisible(false);
        SetOptionPageHeaderVisible(false);
        optionDetailPanel.SetActive(false);
        if (statusText != null)
        {
            statusText.text = string.Empty;
        }

        SetFinishPromptVisible(false);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        RestoreGameplayState();
    }

    public void RequestCloseMenu()
    {
        if (!isOpen || !referencesResolved || isRebinding)
        {
            return;
        }

        RunAfterButtonFeedback(CloseMenu);
    }

    private void HandleToggleRequest()
    {
        if (!isOpen)
        {
            OpenMenu();
            return;
        }

        if (finishPromptPanel != null && finishPromptPanel.activeSelf)
        {
            HideFinishPrompt();
            return;
        }

        if (optionDetailPanel != null && optionDetailPanel.activeSelf)
        {
            ShowMainMenu();
            return;
        }

        CloseMenu();
    }

    private void PauseGameplay()
    {
        if (gameplayPaused)
        {
            return;
        }

        gameplayPaused = true;
        HitStopController.BeginExternalPause();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        openFullMapAfterClose = false;

        cachedMinimapManager = MinimapManager.Instance;
        cachedMinimapView = cachedMinimapManager != null ? cachedMinimapManager.GetComponent<MinimapView>() : null;
        previousMinimapVisible = cachedMinimapView != null && cachedMinimapView.IsMiniMapVisible;
        previousFullMapVisible = cachedMinimapView != null && cachedMinimapView.IsFullMapVisible;
        if (cachedMinimapView != null)
        {
            cachedMinimapView.SetPanelVisibility(false, false);
        }

        if (cachedMinimapManager != null)
        {
            previousMinimapManagerEnabled = cachedMinimapManager.enabled;
            cachedMinimapManager.enabled = false;
        }

        pausedPlayerInput = FindFirstObjectByType<PlayerInput>();
        if (pausedPlayerInput != null)
        {
            previousPlayerInputEnabled = pausedPlayerInput.enabled;
            pausedPlayerInput.enabled = false;
        }

        pausedBehaviours.Clear();
        GameObject playerObject = ResolvePlayerObject();
        if (playerObject == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = playerObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled)
            {
                continue;
            }

            if (!ShouldPauseBehaviour(behaviour.GetType().Name))
            {
                continue;
            }

            behaviour.enabled = false;
            pausedBehaviours.Add(behaviour);
        }

        CapturePausedPlayerVelocity(playerObject);
    }

    private void RestoreGameplayState()
    {
        if (!gameplayPaused)
        {
            return;
        }

        gameplayPaused = false;
        Time.timeScale = previousTimeScale;
        HitStopController.EndExternalPause();

        if (cachedMinimapManager != null)
        {
            cachedMinimapManager.enabled = previousMinimapManagerEnabled;
            cachedMinimapManager = null;
        }

        if (cachedMinimapView != null)
        {
            cachedMinimapView.RestoreFullMapSortingOrder();
            cachedMinimapView.SetPanelVisibility(previousMinimapVisible, previousFullMapVisible);
            cachedMinimapView = null;
        }

        openFullMapAfterClose = false;

        if (pausedPlayerInput != null)
        {
            pausedPlayerInput.enabled = previousPlayerInputEnabled;
            pausedPlayerInput = null;
        }

        RestorePausedPlayerVelocity();

        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            if (pausedBehaviours[i] != null)
            {
                pausedBehaviours[i].enabled = true;
            }
        }

        pausedBehaviours.Clear();
    }

    public void SaveCurrentGame()
    {
        bool saved = SaveManager.TrySaveCurrentGame();
        ShowStatus(saved
            ? "\u30BB\u30FC\u30D6\u3057\u307E\u3057\u305F"
            : "\u30BB\u30FC\u30D6\u306B\u5931\u6557\u3057\u307E\u3057\u305F");
    }

    private void CapturePausedPlayerVelocity(GameObject playerObject)
    {
        pausedPlayerRigidbody = null;
        pausedPlayerLinearVelocity = Vector2.zero;
        pausedPlayerAngularVelocity = 0f;
        hasPausedPlayerVelocity = false;

        if (playerObject == null)
        {
            return;
        }

        Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            return;
        }

        pausedPlayerRigidbody = body;
        pausedPlayerLinearVelocity = body.linearVelocity;
        pausedPlayerAngularVelocity = body.angularVelocity;
        hasPausedPlayerVelocity = true;
    }

    private void RestorePausedPlayerVelocity()
    {
        if (hasPausedPlayerVelocity && pausedPlayerRigidbody != null)
        {
            pausedPlayerRigidbody.linearVelocity = pausedPlayerLinearVelocity;
            pausedPlayerRigidbody.angularVelocity = pausedPlayerAngularVelocity;
        }

        pausedPlayerRigidbody = null;
        pausedPlayerLinearVelocity = Vector2.zero;
        pausedPlayerAngularVelocity = 0f;
        hasPausedPlayerVelocity = false;
    }

    private void OpenMapFromKeyboard()
    {
        if (HandleMapKeyboardRequest())
        {
            UIButtonSfxPlayer.PlayClick();
        }
    }

    public void OpenMap()
    {
        ShowMapOptionPage();
    }

    public void RequestOpenMap()
    {
        if (!referencesResolved || isRebinding)
        {
            return;
        }

        RunAfterButtonFeedback(OpenMap);
    }

    private bool HandleMapKeyboardRequest()
    {
        if (isRebinding || lastMapKeyboardRequestFrame == Time.frameCount)
        {
            return false;
        }

        lastMapKeyboardRequestFrame = Time.frameCount;
        ResolveReferences();
        if (!referencesResolved)
        {
            return false;
        }

        if (isOpen && currentOptionPage == OptionPage.Map && IsOptionDetailVisible())
        {
            CloseMenu();
            return true;
        }

        if (!isOpen)
        {
            OpenMenu();
        }

        SetFinishPromptVisible(false);
        ShowMapOptionPage();
        return true;
    }

    public void ShowFinishPrompt()
    {
        if (isRebinding)
        {
            return;
        }

        if (!referencesResolved)
        {
            return;
        }

        SetMainMenuPanelVisible(false);
        SetOptionPanelVisible(false);
        SetAlternateMainMenuVisible(false);
        SetOptionPageHeaderVisible(false);
        optionDetailPanel.SetActive(false);
        SetFinishPromptVisible(true);
        if (finishPromptSkin != null)
        {
            finishPromptSkin.SelectDefaultButton();
        }
    }

    public void RequestShowFinishPrompt()
    {
        if (!referencesResolved || isRebinding)
        {
            return;
        }

        RunAfterButtonFeedback(ShowFinishPrompt);
    }

    public void HideFinishPrompt()
    {
        if (!referencesResolved)
        {
            return;
        }

        SetFinishPromptVisible(false);
        ShowMainMenu();
    }

    public void RequestHideFinishPrompt()
    {
        if (!referencesResolved)
        {
            return;
        }

        RunAfterButtonFeedback(HideFinishPrompt);
    }

    public void ReturnToTitle()
    {
        if (isRebinding)
        {
            return;
        }

        CloseMenu();
        SceneManager.LoadScene(TitleSceneName);
    }

    public void RequestReturnToTitle()
    {
        if (isRebinding)
        {
            return;
        }

        RunAfterButtonFeedback(ReturnToTitle);
    }

    public void DoNothing()
    {
    }

    public void OpenSkillList()
    {
        if (isRebinding)
        {
            return;
        }

        ResolveReferences();
        if (!referencesResolved)
        {
            return;
        }

        if (!gameplayPaused)
        {
            PauseGameplay();
        }

        SetMenuVisible(true);
        SetCursorMenuModeActive(true);
        isOpen = true;
        SetFinishPromptVisible(false);
        ShowDecorationOptionPage();
    }

    private void HandleVolumePointerInteraction()
    {
        if (isRebinding || !isOpen || soundContentPanel == null || !soundContentPanel.activeSelf || Mouse.current == null)
        {
            activeVolumeBar = ActiveVolumeBar.None;
            return;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            activeVolumeBar = FindBarAtScreenPoint(pointerPosition);
            if (activeVolumeBar != ActiveVolumeBar.None)
            {
                UpdateVolumeFromPointer(activeVolumeBar, pointerPosition);
            }
        }
        else if (Mouse.current.leftButton.isPressed && activeVolumeBar != ActiveVolumeBar.None)
        {
            UpdateVolumeFromPointer(activeVolumeBar, pointerPosition);
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            activeVolumeBar = ActiveVolumeBar.None;
        }
    }

    private void HandleKeyboardScrollInteraction()
    {
        if (isRebinding || !isOpen || keyboardContentPanel == null || !keyboardContentPanel.activeSelf || Mouse.current == null)
        {
            isDraggingKeyboardScrollbar = false;
            return;
        }

        RefreshKeyboardScrollVisuals();
        float scrollableHeight = GetKeyboardScrollableHeight();
        if (scrollableHeight <= 0.01f)
        {
            isDraggingKeyboardScrollbar = false;
            return;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        if (ContainsScreenPoint(keyboardScrollViewport, pointerPosition))
        {
            float wheelDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheelDelta) > 0.01f)
            {
                AdjustKeyboardScroll(-Mathf.Sign(wheelDelta) * KeyboardScrollPixelsPerWheelTick);
            }
        }
    }

    private ActiveVolumeBar FindBarAtScreenPoint(Vector2 screenPoint)
    {
        if (ContainsScreenPoint(bgmBar, screenPoint))
        {
            return ActiveVolumeBar.Bgm;
        }

        if (ContainsScreenPoint(seBar, screenPoint))
        {
            return ActiveVolumeBar.Se;
        }

        if (ContainsScreenPoint(systemBar, screenPoint))
        {
            return ActiveVolumeBar.System;
        }

        return ActiveVolumeBar.None;
    }

    private void UpdateVolumeFromPointer(ActiveVolumeBar bar, Vector2 screenPoint)
    {
        RectTransform track = GetBarRect(bar);
        if (track == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, screenPoint, null, out Vector2 localPoint))
        {
            return;
        }

        float width = Mathf.Max(1f, track.rect.width - (HandlePadding * 2f));
        float normalized = Mathf.Clamp01((localPoint.x + (width * 0.5f)) / width);
        SaveVolume(GetVolumeKey(bar), normalized);
    }

    private void SaveVolume(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        RefreshVolumeVisuals();
        ApplyAudioSettingsToScene(forceRefreshAll: true);
    }

    private void ApplySavedVolumeValues()
    {
        PlayerPrefs.GetFloat(BgmVolumeKey, 1f);
        PlayerPrefs.GetFloat(SeVolumeKey, 1f);
        PlayerPrefs.GetFloat(SystemVolumeKey, 1f);
    }

    private void RefreshVolumeVisuals()
    {
        float bgmVolume = ReadVolume(BgmVolumeKey, 1f);
        float seVolume = ReadVolume(SeVolumeKey, 1f);
        float systemVolume = ReadVolume(SystemVolumeKey, 1f);

        UpdateBarFill(bgmBar, bgmFill, bgmVolume);
        UpdateBarFill(seBar, seFill, seVolume);
        UpdateBarFill(systemBar, systemFill, systemVolume);
        UpdateHandlePosition(bgmBar, bgmHandle, bgmVolume);
        UpdateHandlePosition(seBar, seHandle, seVolume);
        UpdateHandlePosition(systemBar, systemHandle, systemVolume);
        UpdateVolumeText(bgmVolumeText, bgmVolume);
        UpdateVolumeText(seVolumeText, seVolume);
        UpdateVolumeText(systemVolumeText, systemVolume);
    }

    private static void UpdateBarFill(RectTransform track, RectTransform fill, float value)
    {
        if (track == null || fill == null)
        {
            return;
        }

        float normalized = Mathf.Clamp01(value);
        fill.gameObject.SetActive(normalized > 0.0001f);

        float trackWidth = Mathf.Max(1f, track.rect.width);
        fill.anchorMin = new Vector2(0.5f, 0.5f);
        fill.anchorMax = new Vector2(0.5f, 0.5f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = new Vector2(-trackWidth * 0.5f, 0f);
        fill.sizeDelta = new Vector2(trackWidth * normalized, track.rect.height);
        fill.localScale = Vector3.one;

        Image fillImage = fill.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Simple;
            fillImage.fillAmount = 1f;
        }

        fill.SetSiblingIndex(Mathf.Min(1, fill.parent.childCount - 1));
    }

    private static void UpdateVolumeText(TextMeshProUGUI text, float value)
    {
        if (text == null)
        {
            return;
        }

        text.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString();
    }

    private void UpdateHandlePosition(RectTransform track, RectTransform handle, float value)
    {
        if (track == null || handle == null)
        {
            return;
        }

        float width = Mathf.Max(1f, track.rect.width - (HandlePadding * 2f));
        float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, Mathf.Clamp01(value));
        Vector2 anchored = handle.anchoredPosition;
        anchored.x = x;
        handle.anchoredPosition = anchored;
        handle.SetAsLastSibling();
    }

    private void ApplyAudioSettingsToScene(bool forceRefreshAll)
    {
        AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        if (audioSources == null || audioSources.Length == 0)
        {
            return;
        }

        float bgmVolume = ReadVolume(BgmVolumeKey, 1f);
        float seVolume = ReadVolume(SeVolumeKey, 1f);
        float systemVolume = ReadVolume(SystemVolumeKey, 1f);

        for (int i = 0; i < audioSources.Length; i++)
        {
            AudioSource source = audioSources[i];
            if (source == null)
            {
                continue;
            }

            AudioChannel channel = ClassifyAudioSource(source);
            float multiplier = seVolume;
            switch (channel)
            {
                case AudioChannel.Bgm:
                    multiplier = bgmVolume;
                    break;

                case AudioChannel.System:
                    multiplier = systemVolume;
                    break;
            }

            int id = source.GetInstanceID();
            float currentVolume = source.volume;

            // volume を再生直前に上書きするSE（足音・滑空音など）も、0設定では確実に消音する。
            bool shouldMute = multiplier <= 0.0001f;
            if (shouldMute)
            {
                audioSourcesMutedByOptions.Add(id);
                source.mute = true;
            }
            else if (audioSourcesMutedByOptions.Remove(id))
            {
                source.mute = false;
            }

            if (!capturedAudioBaseVolumes.TryGetValue(id, out float baseVolume))
            {
                baseVolume = multiplier > 0.0001f ? currentVolume / multiplier : currentVolume;
                capturedAudioBaseVolumes[id] = baseVolume;
            }
            else if (!forceRefreshAll &&
                     lastAppliedAudioVolumes.TryGetValue(id, out float lastAppliedVolume) &&
                     Mathf.Abs(currentVolume - lastAppliedVolume) > 0.0001f)
            {
                baseVolume = multiplier > 0.0001f ? currentVolume / multiplier : currentVolume;
                capturedAudioBaseVolumes[id] = baseVolume;
            }

            float appliedVolume = Mathf.Clamp01(baseVolume * multiplier);
            source.volume = appliedVolume;
            lastAppliedAudioVolumes[id] = appliedVolume;
        }
    }

    private void RefreshKeyboardBindings()
    {
        InputActionAsset actions = ResolvePlayerActions();
        if (actions == null)
        {
            SetKeyboardValueTexts("-", "-", "-", "-", "-", "-", "-");
            return;
        }

        SetKeyboardValueTexts(
            GetConfiguredBindingLabel(actions, "Move", "right", MoveRightDefaultPath),
            GetConfiguredBindingLabel(actions, "Move", "left", MoveLeftDefaultPath),
            GetConfiguredBindingLabel(actions, "Jump", null, JumpDefaultPath),
            GetConfiguredBindingLabel(actions, "Attack", null, AttackDefaultPath),
            GetConfiguredBindingLabel(actions, "UmbrellaToggle", null, GlideDefaultPath),
            GetConfiguredBindingLabel(actions, "RecoilJump", null, RecoilDefaultPath),
            GetConfiguredBindingLabel(actions, "Dodge", null, DodgeDefaultPath));
    }

    private void SetKeyboardValueTexts(
        string rightMove,
        string leftMove,
        string jump,
        string attack,
        string glide,
        string recoil,
        string dodge)
    {
        SetKeyboardValueText(rightMoveValueText, rightMove);
        SetKeyboardValueText(leftMoveValueText, leftMove);
        SetKeyboardValueText(jumpValueText, jump);
        SetKeyboardValueText(attackValueText, attack);
        SetKeyboardValueText(glideValueText, glide);
        SetKeyboardValueText(recoilValueText, recoil);
        SetKeyboardValueText(parryValueText, "-");
        SetKeyboardValueText(dodgeValueText, dodge);
    }

    private static void SetKeyboardValueText(TextMeshProUGUI text, string value)
    {
        if (text == null)
        {
            return;
        }

        text.text = IsMouseButtonLabel(value) ? string.Empty : value;
    }

    private static bool IsMouseButtonLabel(string value)
    {
        return value == "\u5DE6\u30AF\u30EA\u30C3\u30AF" ||
               value == "\u53F3\u30AF\u30EA\u30C3\u30AF" ||
               value == "\u30DB\u30A4\u30FC\u30EB\u30AF\u30EA\u30C3\u30AF";
    }

    private void StartRebindRightMove()
    {
        BeginInteractiveRebind("Move", "right", MoveRightDefaultPath, allowMouse: false, rightMoveButton);
    }

    private void StartRebindLeftMove()
    {
        BeginInteractiveRebind("Move", "left", MoveLeftDefaultPath, allowMouse: false, leftMoveButton);
    }

    private void StartRebindJump()
    {
        BeginInteractiveRebind("Jump", null, JumpDefaultPath, allowMouse: false, jumpButton);
    }

    private void StartRebindAttack()
    {
        BeginInteractiveRebind("Attack", null, AttackDefaultPath, allowMouse: true, attackButton);
    }

    private void StartRebindGlide()
    {
        BeginInteractiveRebind("UmbrellaToggle", null, GlideDefaultPath, allowMouse: true, glideButton);
    }

    private void StartRebindRecoil()
    {
        BeginInteractiveRebind("RecoilJump", null, RecoilDefaultPath, allowMouse: false, recoilButton);
    }

    private void StartRebindDodge()
    {
        BeginInteractiveRebind("Dodge", null, DodgeDefaultPath, allowMouse: false, dodgeButton);
    }

    private void BeginInteractiveRebind(
        string actionName,
        string compositePartName,
        string defaultPath,
        bool allowMouse,
        Button button)
    {
        if (!referencesResolved || !isOpen || isRebinding)
        {
            return;
        }

        InputActionAsset actions = ResolvePlayerActions();
        if (actions == null)
        {
            ShowKeyboardStatus("\u5165\u529B\u8A2D\u5B9A\u304C\u898B\u3064\u304B\u308A\u307E\u305B\u3093");
            return;
        }

        if (!TryFindBinding(actions, actionName, compositePartName, defaultPath, out InputAction action, out int bindingIndex))
        {
            ShowKeyboardStatus("\u5909\u66F4\u3067\u304D\u308B\u30AD\u30FC\u304C\u898B\u3064\u304B\u308A\u307E\u305B\u3093");
            return;
        }

        DisposeActiveRebindOperation();

        isRebinding = true;
        activeRebindAction = action;
        activeRebindBindingIndex = bindingIndex;
        activeRebindAllowsMouse = allowMouse;
        activeRebindButton = button;
        activeRebindPreviousOverridePath = action.bindings[bindingIndex].overridePath;
        activeVolumeBar = ActiveVolumeBar.None;
        isDraggingKeyboardScrollbar = false;

        SetRebindButtonsInteractable(false);
        if (button != null)
        {
            button.interactable = true;
        }

        SetTemporarilyRebindInputEnabled(false);
        if (activeRebindAction != null && activeRebindAction.enabled)
        {
            activeRebindAction.Disable();
        }

        UpdateRebindButtonLabel(button, "\u5165\u529B\u5F85\u3061...");
        ShowKeyboardStatus("\u5272\u308A\u5F53\u3066\u305F\u3044\u30AD\u30FC\u307E\u305F\u306F\u30DE\u30A6\u30B9\u30DC\u30BF\u30F3\u3092\u5165\u529B\u3057\u3066\u304F\u3060\u3055\u3044 (Esc\u3067\u30AD\u30E3\u30F3\u30BB\u30EB)");

        activeRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .OnCancel(operation =>
            {
                if (activeRebindOperation != operation)
                {
                    return;
                }

                FinishInteractiveRebind("\u30AD\u30FC\u5909\u66F4\u3092\u30AD\u30E3\u30F3\u30BB\u30EB\u3057\u307E\u3057\u305F", saveOverrides: false);
            })
            .OnComplete(operation =>
            {
                if (activeRebindOperation != operation)
                {
                    return;
                }

                string path = action.bindings[bindingIndex].effectivePath ??
                              action.bindings[bindingIndex].overridePath ??
                              action.bindings[bindingIndex].path;

                if (!IsAllowedRebindPath(path, allowMouse))
                {
                    RestoreBindingOverride(action, bindingIndex, activeRebindPreviousOverridePath);
                    FinishInteractiveRebind("\u5BFE\u5FDC\u3057\u3066\u3044\u308B\u5165\u529B\u3092\u9078\u3093\u3067\u304F\u3060\u3055\u3044", saveOverrides: false);
                    return;
                }

                FinishInteractiveRebind("\u30AD\u30FC\u5272\u308A\u5F53\u3066\u3092\u5909\u66F4\u3057\u307E\u3057\u305F", saveOverrides: true);
            });

        activeRebindOperation.Start();
    }

    private void FinishInteractiveRebind(string message, bool saveOverrides)
    {
        InputActionRebindingExtensions.RebindingOperation operation = activeRebindOperation;
        activeRebindOperation = null;

        if (saveOverrides)
        {
            InputActionAsset actions = ResolvePlayerActions();
            if (actions != null)
            {
                PlayerInputBindingOverrides.Save(actions);
            }
        }

        if (operation != null)
        {
            operation.Dispose();
        }

        isRebinding = false;
        activeRebindAction = null;
        activeRebindBindingIndex = -1;
        activeRebindPreviousOverridePath = string.Empty;
        activeRebindAllowsMouse = false;
        SetTemporarilyRebindInputEnabled(false);
        SetRebindButtonsInteractable(true);
        RefreshKeyboardBindings();
        ShowKeyboardStatus(message);
        if (activeRebindButton != null)
        {
            SelectButton(activeRebindButton);
            activeRebindButton = null;
        }
    }

    private void DisposeActiveRebindOperation()
    {
        if (activeRebindOperation != null)
        {
            activeRebindOperation.Dispose();
            activeRebindOperation = null;
        }

        isRebinding = false;
        activeRebindAction = null;
        activeRebindBindingIndex = -1;
        activeRebindPreviousOverridePath = string.Empty;
        activeRebindAllowsMouse = false;
        activeRebindButton = null;
        SetTemporarilyRebindInputEnabled(false);
        SetRebindButtonsInteractable(true);
        if (referencesResolved)
        {
            RefreshKeyboardBindings();
        }
    }

    private void SetTemporarilyRebindInputEnabled(bool enabled)
    {
        if (!gameplayPaused || pausedPlayerInput == null || !previousPlayerInputEnabled)
        {
            return;
        }

        pausedPlayerInput.enabled = enabled;
    }

    private void SetRebindButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(rightMoveButton, interactable);
        SetButtonInteractable(leftMoveButton, interactable);
        SetButtonInteractable(jumpButton, interactable);
        SetButtonInteractable(attackButton, interactable);
        SetButtonInteractable(glideButton, interactable);
        SetButtonInteractable(recoilButton, false);
        SetButtonInteractable(parryButton, interactable);
        SetButtonInteractable(dodgeButton, interactable);
    }

    private void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void UpdateRebindButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

    private string GetConfiguredBindingLabel(
        InputActionAsset actions,
        string actionName,
        string compositePartName,
        string defaultPath)
    {
        if (!TryFindBinding(actions, actionName, compositePartName, defaultPath, out InputAction action, out int bindingIndex))
        {
            return "-";
        }

        string path = action.bindings[bindingIndex].effectivePath ??
                      action.bindings[bindingIndex].overridePath ??
                      action.bindings[bindingIndex].path;
        return HumanizeBinding(path);
    }

    private bool TryFindBinding(
        InputActionAsset actions,
        string actionName,
        string compositePartName,
        string defaultPath,
        out InputAction action,
        out int bindingIndex)
    {
        action = null;
        bindingIndex = -1;
        if (actions == null || string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(defaultPath))
        {
            return false;
        }

        InputActionMap actionMap = actions.FindActionMap(PlayerActionMapName, false);
        action = actionMap != null ? actionMap.FindAction(actionName, false) : null;
        if (action == null)
        {
            return false;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!BindingContainsGroup(binding.groups, KeyboardMouseGroup))
            {
                continue;
            }

            if (!string.Equals(binding.path, defaultPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(compositePartName))
            {
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }
            }
            else
            {
                if (!binding.isPartOfComposite ||
                    !string.Equals(binding.name, compositePartName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            bindingIndex = i;
            return true;
        }

        return false;
    }

    private static bool IsAllowedRebindPath(string path, bool allowMouse)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (allowMouse &&
            (path.StartsWith("<Mouse>/leftButton", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("<Mouse>/rightButton", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("<Mouse>/middleButton", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static void RestoreBindingOverride(InputAction action, int bindingIndex, string overridePath)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return;
        }

        if (string.IsNullOrEmpty(overridePath))
        {
            action.RemoveBindingOverride(bindingIndex);
            return;
        }

        action.ApplyBindingOverride(bindingIndex, overridePath);
    }

    private InputActionAsset ResolvePlayerActions()
    {
        PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput == null)
        {
            return null;
        }

        InputActionAsset actions = playerInput.actions;
        if (actions != null)
        {
            PlayerInputBindingOverrides.EnsureOverridesLoaded(actions);
        }

        return actions;
    }

    private void ResetKeyboardScroll()
    {
        keyboardScrollNormalized = 0f;
        RefreshKeyboardScrollVisuals();
    }

    private void AdjustKeyboardScroll(float deltaPixels)
    {
        float scrollableHeight = GetKeyboardScrollableHeight();
        if (scrollableHeight <= 0.01f)
        {
            return;
        }

        SetKeyboardScrollNormalized(keyboardScrollNormalized + (deltaPixels / scrollableHeight));
    }

    private void SetKeyboardScrollNormalized(float normalized)
    {
        keyboardScrollNormalized = Mathf.Clamp01(normalized);
        RefreshKeyboardScrollVisuals();
    }

    private float GetKeyboardScrollableHeight()
    {
        if (keyboardScrollViewport == null || keyboardScrollContent == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, keyboardScrollContent.rect.height - keyboardScrollViewport.rect.height);
    }

    private void RefreshKeyboardScrollVisuals()
    {
        if (keyboardScrollViewport == null || keyboardScrollContent == null || keyboardScrollbarTrack == null)
        {
            return;
        }

        float viewportHeight = keyboardScrollViewport.rect.height;
        float contentHeight = keyboardScrollContent.rect.height;
        float scrollableHeight = Mathf.Max(0f, contentHeight - viewportHeight);

        Vector2 contentPosition = keyboardScrollContent.anchoredPosition;
        contentPosition.y = keyboardScrollNormalized * scrollableHeight;
        keyboardScrollContent.anchoredPosition = contentPosition;

        bool needsScrollbar = scrollableHeight > 0.01f;
        if (keyboardScrollbarTrack.gameObject.activeSelf != needsScrollbar)
        {
            keyboardScrollbarTrack.gameObject.SetActive(needsScrollbar);
        }

        if (!needsScrollbar || keyboardScrollbarHandle == null)
        {
            return;
        }

        float trackHeight = keyboardScrollbarTrack.rect.height;
        float handleHeight = Mathf.Clamp(
            trackHeight * (viewportHeight / Mathf.Max(viewportHeight, contentHeight)),
            KeyboardScrollbarMinHandleHeight,
            trackHeight);

        Vector2 handleSize = keyboardScrollbarHandle.sizeDelta;
        handleSize.y = handleHeight;
        keyboardScrollbarHandle.sizeDelta = handleSize;

        Vector2 handlePosition = keyboardScrollbarHandle.anchoredPosition;
        handlePosition.y = -(trackHeight - handleHeight) * keyboardScrollNormalized;
        keyboardScrollbarHandle.anchoredPosition = handlePosition;
        HideKeyboardScrollbarGraphics();
    }

    private void HideKeyboardScrollbarGraphics()
    {
        SetGraphicTransparent(keyboardScrollbarTrack);
        SetGraphicTransparent(keyboardScrollbarHandle);
    }

    private static void SetGraphicTransparent(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        Graphic graphic = target.GetComponent<Graphic>();
        if (graphic != null)
        {
            graphic.color = new Color(1f, 1f, 1f, 0.001f);
        }
    }

    private void UpdateKeyboardScrollFromPointer(Vector2 screenPoint)
    {
        if (keyboardScrollbarTrack == null || keyboardScrollbarHandle == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                keyboardScrollbarTrack,
                screenPoint,
                null,
                out Vector2 localPoint))
        {
            return;
        }

        float trackHeight = keyboardScrollbarTrack.rect.height;
        float handleHeight = keyboardScrollbarHandle.rect.height;
        float travel = Mathf.Max(1f, trackHeight - handleHeight);
        float normalized = Mathf.Clamp01((-localPoint.y - (handleHeight * 0.5f)) / travel);
        SetKeyboardScrollNormalized(normalized);
    }

    private void ShowKeyboardStatus(string message)
    {
        if (keyboardStatusText != null)
        {
            keyboardStatusText.text = message;
        }
    }

    private void ClearKeyboardStatus()
    {
        if (keyboardStatusText != null)
        {
            keyboardStatusText.text = string.Empty;
        }
    }

    private static bool BindingContainsGroup(string bindingGroups, string targetGroup)
    {
        if (string.IsNullOrEmpty(bindingGroups) || string.IsNullOrEmpty(targetGroup))
        {
            return false;
        }

        string[] groups = bindingGroups.Split(';');
        for (int i = 0; i < groups.Length; i++)
        {
            if (string.Equals(groups[i], targetGroup, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string HumanizeBinding(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "-";
        }

        switch (path)
        {
            case "<Keyboard>/space":
                return "Space";
            case "<Keyboard>/leftShift":
            case "<Keyboard>/rightShift":
                return "Shift";
            case "<Keyboard>/leftCtrl":
            case "<Keyboard>/rightCtrl":
                return "Ctrl";
            case "<Keyboard>/upArrow":
                return "\u2191";
            case "<Keyboard>/downArrow":
                return "\u2193";
            case "<Keyboard>/leftArrow":
                return "\u2190";
            case "<Keyboard>/rightArrow":
                return "\u2192";
            case "<Mouse>/leftButton":
                return "\u5DE6\u30AF\u30EA\u30C3\u30AF";
            case "<Mouse>/rightButton":
                return "\u53F3\u30AF\u30EA\u30C3\u30AF";
            case "<Mouse>/middleButton":
                return "\u30DB\u30A4\u30FC\u30EB\u30AF\u30EA\u30C3\u30AF";
        }

        string readable = InputControlPath.ToHumanReadableString(
            path,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        if (string.IsNullOrWhiteSpace(readable))
        {
            return "-";
        }

        return readable
            .Replace("Left Button", "\u5DE6\u30AF\u30EA\u30C3\u30AF")
            .Replace("Right Button", "\u53F3\u30AF\u30EA\u30C3\u30AF")
            .Replace("Middle Button", "\u30DB\u30A4\u30FC\u30EB\u30AF\u30EA\u30C3\u30AF")
            .Replace("Left Shift", "Shift")
            .Replace("Right Shift", "Shift");
    }

    private void SetTabVisualState(Button button, bool active)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(active);

        Button baseButton = button == soundTabButton ? soundTabButtonBase : keyboardTabButtonBase;
        if (baseButton != null)
        {
            baseButton.gameObject.SetActive(!active);
        }
    }

    private AudioChannel ClassifyAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return AudioChannel.Se;
        }

        if (StoryTimelineRuntime.UsesBgmVolume(source))
        {
            return AudioChannel.Bgm;
        }

        if (StoryTimelineRuntime.UsesSeVolume(source))
        {
            return AudioChannel.Se;
        }

        // 滑空風音はループ素材だが、BGMではなくプレイヤーSEとして扱う。
        if (source.GetComponent<PlayerGlideAudioController>() != null)
        {
            return AudioChannel.Se;
        }

        if (source.loop)
        {
            return AudioChannel.Bgm;
        }

        if (source.GetComponentInParent<Canvas>() != null)
        {
            return AudioChannel.System;
        }

        string objectName = source.gameObject.name;
        if (!string.IsNullOrEmpty(objectName) &&
            (objectName.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
             objectName.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
             objectName.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
             objectName.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return AudioChannel.System;
        }

        return AudioChannel.Se;
    }

    private bool ShouldToggleMenu()
    {
        return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
               (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
    }

    private float ReadVolume(string key, float defaultValue)
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, defaultValue));
    }

    private string GetVolumeKey(ActiveVolumeBar bar)
    {
        switch (bar)
        {
            case ActiveVolumeBar.Bgm:
                return BgmVolumeKey;
            case ActiveVolumeBar.System:
                return SystemVolumeKey;
            default:
                return SeVolumeKey;
        }
    }

    private RectTransform GetBarRect(ActiveVolumeBar bar)
    {
        switch (bar)
        {
            case ActiveVolumeBar.Bgm:
                return bgmBar;
            case ActiveVolumeBar.System:
                return systemBar;
            case ActiveVolumeBar.Se:
                return seBar;
            default:
                return null;
        }
    }

    private void SetMenuVisible(bool visible)
    {
        SetCanvasRenderingEnabled(visible);

        if (menuRoot != null && menuRoot.activeSelf != visible)
        {
            menuRoot.SetActive(visible);
        }
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

    private void SetCursorMenuModeActive(bool active)
    {
        if (cursorMenuModeActive == active)
        {
            return;
        }

        cursorMenuModeActive = active;
        GameCursorController.SetMenuCursorModeActive(this, active);
    }

    private void SetCanvasRenderingEnabled(bool visible)
    {
        if (rootCanvas != null)
        {
            EnsureTopSortingOrder();
            rootCanvas.enabled = visible;
        }

        if (graphicRaycaster != null)
        {
            graphicRaycaster.enabled = visible;
        }
    }

    private void EnsureTopSortingOrder()
    {
        if (rootCanvas == null)
        {
            return;
        }

        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = TopSortingOrder;
    }

    private void SetAlternateMainMenuVisible(bool visible)
    {
        if (alternateMainMenuPanel != null && alternateMainMenuPanel.activeSelf != visible)
        {
            alternateMainMenuPanel.SetActive(visible);
        }
    }

    private void SetMainMenuPanelVisible(bool visible)
    {
        if (mainMenuPanel != null && mainMenuPanel.activeSelf != visible)
        {
            mainMenuPanel.SetActive(visible);
        }
    }

    private void SetOptionPanelVisible(bool visible)
    {
        if (optionPanel != null && optionPanel.activeSelf != visible)
        {
            optionPanel.SetActive(visible);
        }
    }

    private void SetFinishPromptVisible(bool visible)
    {
        if (finishPromptPanel != null && finishPromptPanel.activeSelf != visible)
        {
            finishPromptPanel.SetActive(visible);
        }
    }

    private void ShowStatus(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusHideAt = Time.unscaledTime + 1.8f;
    }

    private void SelectButton(Button button)
    {
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private Button EnsureRuntimeButton(string path)
    {
        Transform child = transform.Find(path);
        return EnsureRuntimeButton(child != null ? child.gameObject : null, path);
    }

    private Button EnsureRuntimeButton(GameObject target, string hitKey)
    {
        return EnsureRuntimeButton(target, hitKey, useDimHover: true);
    }

    private Button EnsureRuntimeButton(GameObject target, string hitKey, bool useDimHover)
    {
        return OptionsCanvasButtonUtility.ConfigureExistingGraphicButton(target, useDimHover);
    }

    private static bool ShouldPauseBehaviour(string typeName)
    {
        for (int i = 0; i < PlayerControlBehaviourNames.Length; i++)
        {
            if (PlayerControlBehaviourNames[i] == typeName)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject ResolvePlayerObject()
    {
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            return taggedPlayer;
        }

        global::PlayerController playerController = FindFirstObjectByType<global::PlayerController>();
        if (playerController != null)
        {
            return playerController.gameObject;
        }

        return pausedPlayerInput != null ? pausedPlayerInput.gameObject : null;
    }

    private bool ContainsScreenPoint(RectTransform rectTransform, Vector2 screenPoint)
    {
        return rectTransform != null &&
               RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, null);
    }

    private string BuildMissingReferencesSummary()
    {
        var missing = new System.Text.StringBuilder();
        if (menuRoot == null) missing.Append("MenuRoot ");
        if (optionPanel == null) missing.Append("OptionPanel ");
        if (optionDetailPanel == null) missing.Append("OptionDetailPanel ");
        if (soundContentPanel == null) missing.Append("SoundContentPanel ");
        if (keyboardContentPanel == null) missing.Append("KeyboardContentPanel ");
        if (soundTabButton == null) missing.Append("SoundTabButton ");
        if (keyboardTabButton == null) missing.Append("KeyboardTabButton ");
        if (backButton == null) missing.Append("BackButton ");
        if (rightMoveButton == null) missing.Append("RightMoveButton ");
        if (leftMoveButton == null) missing.Append("LeftMoveButton ");
        if (jumpButton == null) missing.Append("JumpButton ");
        if (attackButton == null) missing.Append("AttackButton ");
        if (glideButton == null) missing.Append("GlideButton ");
        if (dodgeButton == null) missing.Append("DodgeButton ");
        if (bgmBar == null) missing.Append("BgmBar ");
        if (seBar == null) missing.Append("SeBar ");
        if (systemBar == null) missing.Append("SystemBar ");
        return missing.Length > 0 ? missing.ToString() : "(unknown)";
    }

    private string BuildMissingHeaderSummary()
    {
        var missing = new System.Text.StringBuilder();
        if (optionHeaderPreviousButtonObject == null) missing.Append("Q ");
        if (optionHeaderNextButtonObject == null) missing.Append("E ");
        if (optionHeaderCloseButtonObject == null) missing.Append("'Back Button' ");
        if (optionHeaderBack == null) missing.Append("back ");
        if (optionHeaderBackground == null) missing.Append("'Back ground 1_0' ");
        if (optionHeaderSelectionMarker == null) missing.Append("'Select 2_0' ");
        if (mapTabNormal == null) missing.Append("'Map No Select 1_0' ");
        if (mapTabSelected == null) missing.Append("'Map Select 1_0' ");
        if (decorationTabNormal == null) missing.Append("'Decoration No Select 1_0' ");
        if (decorationTabSelected == null) missing.Append("'Decoration Select 1_0' ");
        if (noteTabNormal == null) missing.Append("'Note No Select 1_0' ");
        if (noteTabSelected == null) missing.Append("'Note Select 1_0' ");
        if (settingsTabNormal == null) missing.Append("'Option text No select 1_0' ");
        if (settingsTabSelected == null) missing.Append("'Option text select 1_0' ");
        return missing.Length > 0 ? missing.ToString() : "(unknown)";
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        BindButton(button, action, useDimHover: true);
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action, bool useDimHover)
    {
        if (button == null)
        {
            return;
        }

        OptionsCanvasButtonUtility.ConfigureSingleIllustrationButton(button, useDimHover);
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
        UIButtonSfxPlayer.Register(button);
    }

    private void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
    }

    private GameObject FindChildObject(string path)
    {
        Transform child = transform.Find(path);
        return child != null ? child.gameObject : null;
    }

    private T FindChildComponent<T>(string path) where T : Component
    {
        Transform child = transform.Find(path);
        return child != null ? child.GetComponent<T>() : null;
    }
}
