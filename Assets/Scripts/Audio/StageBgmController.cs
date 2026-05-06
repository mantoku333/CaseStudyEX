using UnityEngine;

[DisallowMultipleComponent]
public sealed class StageBgmController : MonoBehaviour
{
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip normalStageBgm;
    [SerializeField] private AudioClip bossStageBgm;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.2f;
    [SerializeField, Min(0f)] private float crossfadeDuration = 1.5f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private Coroutine crossfadeCoroutine;

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
        sourceA.volume = bgmVolume;
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

    private void Start()
    {
        PlayImmediate(normalStageBgm);
    }

    public void PlayNormal()
    {
        Play(normalStageBgm);
    }

    public void PlayBoss()
    {
        Play(bossStageBgm);
    }

    public void PlayNormalImmediate()
    {
        PlayImmediate(normalStageBgm);
    }

    public void PlayBossImmediate()
    {
        PlayImmediate(bossStageBgm);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource current = ResolveCurrentSource();
        if (current != null && current.clip == clip && current.isPlaying)
        {
            return;
        }

        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }

        crossfadeCoroutine = StartCoroutine(CrossfadeTo(clip));
    }

    private void PlayImmediate(AudioClip clip)
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
        to.volume = bgmVolume;
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

    private System.Collections.IEnumerator CrossfadeTo(AudioClip nextClip)
    {
        AudioSource from = ResolveCurrentSource();
        AudioSource to = from == sourceA ? sourceB : sourceA;

        if (to == null)
        {
            yield break;
        }

        to.clip = nextClip;
        to.volume = 0f;
        to.Play();

        if (from == null || !from.isPlaying || crossfadeDuration <= 0f)
        {
            to.volume = bgmVolume;
            activeSource = to;
            crossfadeCoroutine = null;
            yield break;
        }

        float fromStartVolume = from.volume;
        float elapsed = 0f;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / crossfadeDuration);
            from.volume = Mathf.Lerp(fromStartVolume, 0f, t);
            to.volume = Mathf.Lerp(0f, bgmVolume, t);
            yield return null;
        }

        from.Stop();
        from.clip = null;
        from.volume = 0f;

        to.volume = bgmVolume;
        activeSource = to;
        crossfadeCoroutine = null;
    }
}
