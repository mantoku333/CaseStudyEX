using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageBgmController : MonoBehaviour
{
    private const string BgmVolumeKey = "MantokuStoryOptions.BgmVolume";

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
        PlayImmediate(normalStageBgm, bgmVolume);
    }

    public void PlayNormal()
    {
        Play(normalStageBgm, bgmVolume);
    }

    public void PlayNormal(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            PlayNormal();
            return;
        }

        Play(clip, volume);
    }

    public void PlayBoss()
    {
        Play(bossStageBgm, bgmVolume);
    }

    public void PlayBoss(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            PlayBoss();
            return;
        }

        Play(clip, volume);
    }

    public void PlayNormalImmediate()
    {
        PlayImmediate(normalStageBgm, bgmVolume);
    }

    public void PlayBossImmediate()
    {
        PlayImmediate(bossStageBgm, bgmVolume);
    }

    private void Play(AudioClip clip, float baseVolume)
    {
        if (clip == null)
        {
            return;
        }

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
        return Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
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
}
