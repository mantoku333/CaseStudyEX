using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Registers UI buttons in every scene and plays a shared click sound.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIButtonSfxPlayer : MonoBehaviour
{
    private const string SettingsResourcePath = "UI/UIButtonSfxSettings";
    private const string SystemVolumeKey = "Options.SystemVolume";

    private static UIButtonSfxPlayer instance;

    private UIButtonSfxSettings settings;
    private AudioSource audioSource;
    private Coroutine registrationRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        UIButtonSfxSettings loadedSettings = Resources.Load<UIButtonSfxSettings>(SettingsResourcePath);
        if (loadedSettings == null || loadedSettings.ClickClip == null)
        {
            Debug.LogWarning($"[UIButtonSfxPlayer] Missing settings or click clip at Resources/{SettingsResourcePath}.");
            return;
        }

        GameObject playerObject = new GameObject(nameof(UIButtonSfxPlayer));
        DontDestroyOnLoad(playerObject);

        instance = playerObject.AddComponent<UIButtonSfxPlayer>();
        instance.Initialize(loadedSettings);
    }

    /// <summary>
    /// Registers a button created after its scene was loaded, such as a dialogue option.
    /// </summary>
    public static void Register(Button button)
    {
        if (instance != null)
        {
            instance.RegisterButton(button);
        }
    }

    /// <summary>
    /// Registers pointer hover sound for a button. Used by the title UI.
    /// </summary>
    public static void RegisterHover(Button button)
    {
        if (instance != null)
        {
            instance.RegisterHoverButton(button);
        }
    }

    /// <summary>
    /// Plays the shared UI click for keyboard/controller actions that bypass Button.onClick.
    /// </summary>
    public static void PlayClick()
    {
        if (instance != null)
        {
            instance.Play();
        }
    }

    /// <summary>
    /// Plays the configured panel-open sound for UI presented without a Button click.
    /// </summary>
    public static void PlayPanelOpen()
    {
        if (instance != null)
        {
            instance.PlayPanelOpenInternal();
        }
    }

    /// <summary>
    /// Plays the configured diary-open sound after the pickup diary UI is shown.
    /// </summary>
    public static void PlayDiaryOpen()
    {
        if (instance != null)
        {
            instance.PlayDiaryOpenInternal();
        }
    }

    private void Initialize(UIButtonSfxSettings loadedSettings)
    {
        settings = loadedSettings;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        audioSource.volume = settings.Volume * PlayerPrefs.GetFloat(SystemVolumeKey, 1f);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (registrationRoutine != null)
        {
            StopCoroutine(registrationRoutine);
        }

        registrationRoutine = StartCoroutine(RegisterSceneButtons());
    }

    private IEnumerator RegisterSceneButtons()
    {
        // Awake-created buttons are available immediately. Waiting one frame also catches Start-created UI.
        RegisterAllSceneButtons();
        yield return null;
        RegisterAllSceneButtons();
        registrationRoutine = null;
    }

    private void RegisterAllSceneButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            RegisterButton(buttons[i]);
        }
    }

    private void RegisterButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        UIButtonSfxRelay relay = button.GetComponent<UIButtonSfxRelay>();
        if (relay == null)
        {
            relay = button.gameObject.AddComponent<UIButtonSfxRelay>();
        }

        relay.Initialize(this, button);
    }

    private void RegisterHoverButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        UIButtonHoverSfxRelay relay = button.GetComponent<UIButtonHoverSfxRelay>();
        if (relay == null)
        {
            relay = button.gameObject.AddComponent<UIButtonHoverSfxRelay>();
        }

        relay.Initialize(this, button);
    }

    internal void Play()
    {
        Play(settings != null ? settings.ClickClip : null);
    }

    internal void PlayHover()
    {
        Play(settings != null ? settings.HoverClip : null);
    }

    private void PlayPanelOpenInternal()
    {
        Play(settings != null ? settings.PanelOpenClip : null);
    }

    private void PlayDiaryOpenInternal()
    {
        Play(settings != null ? settings.DiaryOpenClip : null);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        // Title has no OptionsMenu to apply the saved master volume, so refresh it here too.
        audioSource.volume = settings.Volume * PlayerPrefs.GetFloat(SystemVolumeKey, 1f);
        audioSource.PlayOneShot(clip);
    }
}

[DisallowMultipleComponent]
internal sealed class UIButtonHoverSfxRelay : MonoBehaviour, IPointerEnterHandler
{
    private UIButtonSfxPlayer player;
    private Button button;

    internal void Initialize(UIButtonSfxPlayer owner, Button targetButton)
    {
        player = owner;
        button = targetButton;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (player != null && button != null && button.IsInteractable())
        {
            player.PlayHover();
        }
    }
}

[DisallowMultipleComponent]
internal sealed class UIButtonSfxRelay : MonoBehaviour
{
    private UIButtonSfxPlayer player;
    private Button button;

    internal void Initialize(UIButtonSfxPlayer owner, Button targetButton)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Play);
        }

        player = owner;
        button = targetButton;
        button.onClick.RemoveListener(Play);
        button.onClick.AddListener(Play);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Play);
        }
    }

    private void Play()
    {
        if (player != null)
        {
            player.Play();
        }
    }
}
