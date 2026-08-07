using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("CaseStudy/Story/Story Timeline Runtime")]
public sealed class StoryTimelineRuntime : MonoBehaviour
{
    private const string RuntimeObjectName = "[StoryTimelineRuntime]";
    private const string BgmVolumeKey = "Options.BgmVolume";
    private const string SeVolumeKey = "Options.SeVolume";
    private const string SystemVolumeKey = "Options.SystemVolume";

    private static StoryTimelineRuntime instance;

    private AudioSource bgmSource;
    private AudioSource seSource;
    private AudioSource ambienceSource;
    private Coroutine bgmFadeRoutine;
    private Coroutine ambienceFadeRoutine;
    private float bgmBaseVolume = 1f;
    private float ambienceBaseVolume = 1f;

    public static StoryTimelineRuntime Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    public static bool UsesBgmVolume(AudioSource source)
    {
        return source != null && instance != null && source == instance.bgmSource;
    }

    public static bool UsesSeVolume(AudioSource source)
    {
        return source != null &&
               instance != null &&
               (source == instance.seSource || source == instance.ambienceSource);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<StoryTimelineRuntime>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildSources();
    }

    private void Update()
    {
        if (bgmFadeRoutine == null && bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.volume = ResolveBgmVolume(bgmBaseVolume);
        }

        if (ambienceFadeRoutine == null && ambienceSource != null && ambienceSource.isPlaying)
        {
            ambienceSource.volume = ResolveSeVolume(ambienceBaseVolume);
        }
    }

    public void PlayBgm(AudioClip clip, float volume, bool loop, float fadeSeconds)
    {
        if (clip == null)
        {
            return;
        }

        StageBgmController stageBgm = FindFirstObjectByType<StageBgmController>(FindObjectsInactive.Include);
        if (stageBgm != null)
        {
            stageBgm.PlayTimelineBgm(clip, volume);
            return;
        }

        BuildSources();

        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }

        bgmBaseVolume = Mathf.Clamp01(volume);
        bgmSource.loop = loop;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            bgmFadeRoutine = StartCoroutine(FadeBgmTo(bgmBaseVolume, fadeSeconds));
            return;
        }

        bgmSource.clip = clip;
        bgmSource.volume = fadeSeconds > 0f ? 0f : ResolveBgmVolume(bgmBaseVolume);
        bgmSource.Play();

        if (fadeSeconds > 0f)
        {
            bgmFadeRoutine = StartCoroutine(FadeBgmTo(bgmBaseVolume, fadeSeconds));
        }
    }

    public void StopBgm(float fadeSeconds)
    {
        StageBgmController stageBgm = FindFirstObjectByType<StageBgmController>(FindObjectsInactive.Include);
        if (stageBgm != null)
        {
            stageBgm.StopTimelineBgm(fadeSeconds);
            return;
        }

        BuildSources();

        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }

        if (bgmSource == null || !bgmSource.isPlaying)
        {
            return;
        }

        if (fadeSeconds <= 0f)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = 0f;
            return;
        }

        bgmFadeRoutine = StartCoroutine(FadeBgmOut(fadeSeconds));
    }

    public void PlayAmbience(AudioClip clip, float volume, bool loop, float fadeSeconds)
    {
        if (clip == null)
        {
            return;
        }

        BuildSources();

        if (ambienceFadeRoutine != null)
        {
            StopCoroutine(ambienceFadeRoutine);
            ambienceFadeRoutine = null;
        }

        ambienceBaseVolume = Mathf.Clamp01(volume);
        ambienceSource.loop = loop;

        if (ambienceSource.clip == clip && ambienceSource.isPlaying)
        {
            ambienceFadeRoutine = StartCoroutine(FadeAmbienceTo(ambienceBaseVolume, fadeSeconds));
            return;
        }

        ambienceSource.clip = clip;
        ambienceSource.volume = fadeSeconds > 0f ? 0f : ResolveSeVolume(ambienceBaseVolume);
        ambienceSource.Play();

        if (fadeSeconds > 0f)
        {
            ambienceFadeRoutine = StartCoroutine(FadeAmbienceTo(ambienceBaseVolume, fadeSeconds));
        }
    }

    public void StopAmbience(float fadeSeconds)
    {
        BuildSources();

        if (ambienceFadeRoutine != null)
        {
            StopCoroutine(ambienceFadeRoutine);
            ambienceFadeRoutine = null;
        }

        if (ambienceSource == null || !ambienceSource.isPlaying)
        {
            return;
        }

        if (fadeSeconds <= 0f)
        {
            ambienceSource.Stop();
            ambienceSource.clip = null;
            ambienceSource.volume = 0f;
            return;
        }

        ambienceFadeRoutine = StartCoroutine(FadeAmbienceOut(fadeSeconds));
    }

    public void PlayTimelineAudio(
        StoryTimelineAudioKind audioKind,
        StoryTimelineAudioAction action,
        AudioClip clip,
        float volume,
        bool loop,
        float fadeSeconds)
    {
        if (audioKind == StoryTimelineAudioKind.Bgm)
        {
            if (action == StoryTimelineAudioAction.Stop)
            {
                StopBgm(fadeSeconds);
                return;
            }

            PlayBgm(clip, volume, loop, fadeSeconds);
            return;
        }

        if (audioKind == StoryTimelineAudioKind.Ambience)
        {
            if (action == StoryTimelineAudioAction.Stop)
            {
                StopAmbience(fadeSeconds);
                return;
            }

            PlayAmbience(clip, volume, loop, fadeSeconds);
            return;
        }

        if (action == StoryTimelineAudioAction.Play)
        {
            PlaySe(clip, volume);
        }
    }

    public void PlaySe(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        BuildSources();
        seSource.PlayOneShot(clip, Mathf.Clamp01(volume) * ResolveSeVolume());
    }

    private IEnumerator FadeBgmTo(float targetBaseVolume, float fadeSeconds)
    {
        float start = bgmSource != null ? bgmSource.volume : 0f;
        float target = ResolveBgmVolume(targetBaseVolume);
        float duration = Mathf.Max(0.01f, fadeSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (bgmSource != null)
            {
                bgmSource.volume = Mathf.Lerp(start, target, t);
            }

            yield return null;
        }

        if (bgmSource != null)
        {
            bgmSource.volume = target;
        }

        bgmFadeRoutine = null;
    }

    private IEnumerator FadeBgmOut(float fadeSeconds)
    {
        float start = bgmSource != null ? bgmSource.volume : 0f;
        float duration = Mathf.Max(0.01f, fadeSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (bgmSource != null)
            {
                bgmSource.volume = Mathf.Lerp(start, 0f, t);
            }

            yield return null;
        }

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = 0f;
        }

        bgmFadeRoutine = null;
    }

    private IEnumerator FadeAmbienceTo(float targetBaseVolume, float fadeSeconds)
    {
        float start = ambienceSource != null ? ambienceSource.volume : 0f;
        float target = ResolveSeVolume(targetBaseVolume);
        float duration = Mathf.Max(0.01f, fadeSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (ambienceSource != null)
            {
                ambienceSource.volume = Mathf.Lerp(start, target, t);
            }

            yield return null;
        }

        if (ambienceSource != null)
        {
            ambienceSource.volume = target;
        }

        ambienceFadeRoutine = null;
    }

    private IEnumerator FadeAmbienceOut(float fadeSeconds)
    {
        float start = ambienceSource != null ? ambienceSource.volume : 0f;
        float duration = Mathf.Max(0.01f, fadeSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (ambienceSource != null)
            {
                ambienceSource.volume = Mathf.Lerp(start, 0f, t);
            }

            yield return null;
        }

        if (ambienceSource != null)
        {
            ambienceSource.Stop();
            ambienceSource.clip = null;
            ambienceSource.volume = 0f;
        }

        ambienceFadeRoutine = null;
    }

    private void BuildSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;
        }

        if (seSource == null)
        {
            seSource = gameObject.AddComponent<AudioSource>();
            seSource.playOnAwake = false;
            seSource.loop = false;
            seSource.spatialBlend = 0f;
        }

        if (ambienceSource == null)
        {
            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
        }
    }

    private static float ResolveBgmVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) *
               ResolveMasterVolume() *
               Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
    }

    private static float ResolveSeVolume()
    {
        return ResolveMasterVolume() * Mathf.Clamp01(PlayerPrefs.GetFloat(SeVolumeKey, 1f));
    }

    private static float ResolveSeVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * ResolveSeVolume();
    }

    private static float ResolveMasterVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(SystemVolumeKey, 1f));
    }
}
