using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageBgmController : MonoBehaviour
{
    private const string BgmVolumeKey = "Options.BgmVolume";
    private const string SystemVolumeKey = "Options.SystemVolume";

    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip normalStageBgm;
    [SerializeField] private AudioClip bossStageBgm;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.2f;
    [SerializeField, Min(0f)] private float crossfadeDuration = 1.5f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private Coroutine crossfadeCoroutine;
    private float sourceABaseVolume;
    private float sourceBBaseVolume;
    private PlaybackContext playbackContext = PlaybackContext.Normal;
    private readonly Dictionary<Object, AreaBgmRequest> areaBgmRequests = new Dictionary<Object, AreaBgmRequest>();
    private long nextAreaRequestOrder;

    private enum PlaybackContext
    {
        Normal,
        Boss,
        Timeline
    }

    private readonly struct AreaBgmRequest
    {
        public AreaBgmRequest(AudioClip clip, float volume, int priority, long order)
        {
            Clip = clip;
            Volume = volume;
            Priority = priority;
            Order = order;
        }

        public AudioClip Clip { get; }
        public float Volume { get; }
        public int Priority { get; }
        public long Order { get; }
    }

    public AudioClip RequestedBgmClip { get; private set; }

    private void Awake()
    {
        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
        }

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        sourceA = bgmSource;
        sourceB = gameObject.AddComponent<AudioSource>();

        ConfigureSource(sourceA);
        ConfigureSource(sourceB);
        SetSourceBaseVolume(sourceA, bgmVolume);
        SetSourceBaseVolume(sourceB, 0f);
        sourceA.volume = ResolveEffectiveVolume(bgmVolume);
        sourceB.volume = 0f;
    }

    private void OnDisable()
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }
    }

    private void Update()
    {
        if (crossfadeCoroutine != null)
        {
            return;
        }

        RefreshPlayingSourceVolumes();
    }

    private void Start()
    {
        playbackContext = PlaybackContext.Normal;
        PlayNormalOrArea(true);
    }

    public void PlayNormal()
    {
        playbackContext = PlaybackContext.Normal;
        PlayNormalOrArea(false);
    }

    public void PlayNormal(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            PlayNormal();
            return;
        }

        playbackContext = PlaybackContext.Normal;
        Play(clip, volume);
    }

    public void PlayBoss()
    {
        playbackContext = PlaybackContext.Boss;
        Play(bossStageBgm, bgmVolume);
    }

    public void PlayBoss(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            PlayBoss();
            return;
        }

        playbackContext = PlaybackContext.Boss;
        Play(clip, volume);
    }

    public void PlayNormalImmediate()
    {
        playbackContext = PlaybackContext.Normal;
        PlayNormalOrArea(true);
    }

    public void PlayBossImmediate()
    {
        playbackContext = PlaybackContext.Boss;
        PlayImmediate(bossStageBgm, bgmVolume);
    }

    public void PlayTimelineBgm(AudioClip clip, float volume)
    {
        playbackContext = PlaybackContext.Timeline;
        Play(clip, volume);
    }

    public void SetAreaBgm(Object owner, AudioClip clip, float volume, int priority = 0)
    {
        if (owner == null || clip == null)
        {
            return;
        }

        areaBgmRequests[owner] = new AreaBgmRequest(
            clip,
            Mathf.Clamp01(volume),
            priority,
            nextAreaRequestOrder++);

        if (playbackContext == PlaybackContext.Normal)
        {
            PlayNormalOrArea(false);
        }
    }

    public void ClearAreaBgm(Object owner)
    {
        if (owner == null || !areaBgmRequests.Remove(owner))
        {
            return;
        }

        if (playbackContext == PlaybackContext.Normal)
        {
            PlayNormalOrArea(false);
        }
    }

    public void StopTimelineBgm(float fadeSeconds)
    {
        // タイムライン停止と通常のBGMフェードアウトで同じ処理を使う。
        playbackContext = PlaybackContext.Normal;
        FadeOutCurrent(fadeSeconds);
    }

    public void FadeOutCurrent(float fadeSeconds)
    {
        // 現在鳴っているBGMを、別クリップへ切り替えずに音量だけ下げて停止する。
        RequestedBgmClip = null;

        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }

        AudioSource current = ResolveCurrentSource();
        if (current == null || !current.isPlaying)
        {
            return;
        }

        if (fadeSeconds <= 0f)
        {
            StopSource(sourceA);
            StopSource(sourceB);
            activeSource = null;
            return;
        }

        crossfadeCoroutine = StartCoroutine(FadeOutAll(fadeSeconds));
    }

    private void Play(AudioClip clip, float baseVolume)
    {
        if (clip == null)
        {
            return;
        }

        RequestedBgmClip = clip;

        AudioSource current = ResolveCurrentSource();
        if (current != null && current.clip == clip && current.isPlaying)
        {
            SetSourceBaseVolume(current, baseVolume);
            RefreshSourceVolume(current);
            return;
        }

        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }

        crossfadeCoroutine = StartCoroutine(CrossfadeTo(clip, baseVolume));
    }

    private void PlayImmediate(AudioClip clip, float baseVolume)
    {
        if (clip == null)
        {
            return;
        }

        RequestedBgmClip = clip;

        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }

        AudioSource to = ResolveCurrentSource();
        if (to == null)
        {
            to = sourceA != null ? sourceA : sourceB;
        }

        AudioSource from = to == sourceA ? sourceB : sourceA;
        if (from != null && from.isPlaying)
        {
            from.Stop();
            from.clip = null;
            from.volume = 0f;
        }

        if (to == null)
        {
            return;
        }

        to.clip = clip;
        SetSourceBaseVolume(to, baseVolume);
        to.volume = ResolveEffectiveVolume(baseVolume);
        if (!to.isPlaying)
        {
            to.Play();
        }

        activeSource = to;
    }

    private void ConfigureSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private void StopSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
        source.volume = 0f;
        SetSourceBaseVolume(source, 0f);
    }

    private void PlayNormalOrArea(bool immediate)
    {
        RemoveDestroyedAreaOwners();

        AudioClip clip = normalStageBgm;
        float volume = bgmVolume;
        if (TryGetHighestPriorityArea(out AreaBgmRequest areaRequest))
        {
            clip = areaRequest.Clip;
            volume = areaRequest.Volume;
        }

        if (immediate)
        {
            PlayImmediate(clip, volume);
        }
        else
        {
            Play(clip, volume);
        }
    }

    private bool TryGetHighestPriorityArea(out AreaBgmRequest selected)
    {
        selected = default;
        bool found = false;

        foreach (KeyValuePair<Object, AreaBgmRequest> pair in areaBgmRequests)
        {
            AreaBgmRequest request = pair.Value;
            if (!found ||
                request.Priority > selected.Priority ||
                (request.Priority == selected.Priority && request.Order > selected.Order))
            {
                selected = request;
                found = true;
            }
        }

        return found;
    }

    private void RemoveDestroyedAreaOwners()
    {
        if (areaBgmRequests.Count == 0)
        {
            return;
        }

        List<Object> destroyedOwners = null;
        foreach (Object owner in areaBgmRequests.Keys)
        {
            if (owner != null)
            {
                continue;
            }

            destroyedOwners ??= new List<Object>();
            destroyedOwners.Add(owner);
        }

        if (destroyedOwners == null)
        {
            return;
        }

        for (int i = 0; i < destroyedOwners.Count; i++)
        {
            areaBgmRequests.Remove(destroyedOwners[i]);
        }
    }

    private AudioSource ResolveCurrentSource()
    {
        bool aPlaying = sourceA != null && sourceA.isPlaying;
        bool bPlaying = sourceB != null && sourceB.isPlaying;

        if (aPlaying && bPlaying)
        {
            return sourceA.volume >= sourceB.volume ? sourceA : sourceB;
        }

        if (aPlaying)
        {
            return sourceA;
        }

        if (bPlaying)
        {
            return sourceB;
        }

        return activeSource;
    }

    private float ResolveEffectiveVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * ResolveOptionsBgmVolume();
    }

    private static float ResolveOptionsBgmVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(SystemVolumeKey, 1f)) *
               Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
    }

    private void SetSourceBaseVolume(AudioSource source, float baseVolume)
    {
        float clampedVolume = Mathf.Clamp01(baseVolume);

        if (source == sourceA)
        {
            sourceABaseVolume = clampedVolume;
            return;
        }

        if (source == sourceB)
        {
            sourceBBaseVolume = clampedVolume;
        }
    }

    private float GetSourceBaseVolume(AudioSource source)
    {
        if (source == sourceA)
        {
            return sourceABaseVolume;
        }

        if (source == sourceB)
        {
            return sourceBBaseVolume;
        }

        return bgmVolume;
    }

    private void RefreshPlayingSourceVolumes()
    {
        RefreshSourceVolume(sourceA);
        RefreshSourceVolume(sourceB);
    }

    private void RefreshSourceVolume(AudioSource source)
    {
        if (source == null || !source.isPlaying)
        {
            return;
        }

        source.volume = ResolveEffectiveVolume(GetSourceBaseVolume(source));
    }

    private System.Collections.IEnumerator CrossfadeTo(AudioClip nextClip, float nextBaseVolume)
    {
        AudioSource from = ResolveCurrentSource();
        AudioSource to = from == sourceA ? sourceB : sourceA;

        if (to == null)
        {
            yield break;
        }

        to.clip = nextClip;
        SetSourceBaseVolume(to, nextBaseVolume);
        to.volume = 0f;
        to.Play();

        if (from == null || !from.isPlaying || crossfadeDuration <= 0f)
        {
            to.volume = ResolveEffectiveVolume(nextBaseVolume);
            activeSource = to;
            crossfadeCoroutine = null;
            yield break;
        }

        float fromStartBaseVolume = GetSourceBaseVolume(from);
        float toBaseVolume = Mathf.Clamp01(nextBaseVolume);
        float elapsed = 0f;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / crossfadeDuration);
            float optionsVolume = ResolveOptionsBgmVolume();
            from.volume = Mathf.Lerp(fromStartBaseVolume, 0f, t) * optionsVolume;
            to.volume = Mathf.Lerp(0f, toBaseVolume, t) * optionsVolume;
            yield return null;
        }

        from.Stop();
        from.clip = null;
        SetSourceBaseVolume(from, 0f);
        from.volume = 0f;

        to.volume = ResolveEffectiveVolume(toBaseVolume);
        activeSource = to;
        crossfadeCoroutine = null;
    }

    private System.Collections.IEnumerator FadeOutAll(float fadeSeconds)
    {
        float sourceAStart = sourceA != null ? sourceA.volume : 0f;
        float sourceBStart = sourceB != null ? sourceB.volume : 0f;
        float duration = Mathf.Max(0.01f, fadeSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (sourceA != null)
            {
                sourceA.volume = Mathf.Lerp(sourceAStart, 0f, t);
            }

            if (sourceB != null)
            {
                sourceB.volume = Mathf.Lerp(sourceBStart, 0f, t);
            }

            yield return null;
        }

        StopSource(sourceA);
        StopSource(sourceB);
        activeSource = null;
        crossfadeCoroutine = null;
    }
}
