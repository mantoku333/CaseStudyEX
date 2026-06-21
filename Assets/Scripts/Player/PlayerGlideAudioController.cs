using Player;
using UnityEngine;

/// <summary>
/// 滑空が一定時間続いたときだけ、専用の AudioSource で風音をループ再生する。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerGlideAudioController : MonoBehaviour
{
    [Header("滑空風音")]
    [SerializeField] private AudioClip glideLoopClip;
    [SerializeField, Min(0f)] private float playDelay = 0.2f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private IPlayerViewStateProvider stateProvider;
    private AudioSource glideAudioSource;
    private float glidingDuration;

    private void Awake()
    {
        stateProvider = GetComponent<IPlayerViewStateProvider>();
        EnsureAudioSource();
    }

    private void OnEnable()
    {
        glidingDuration = 0f;
    }

    private void Update()
    {
        if (stateProvider == null)
        {
            stateProvider = GetComponent<IPlayerViewStateProvider>();
        }

        if (stateProvider == null || !stateProvider.IsGliding)
        {
            glidingDuration = 0f;
            FadeOutGlideAudio();
            return;
        }

        glidingDuration += Time.deltaTime;
        if (glidingDuration < playDelay)
        {
            // 着地直後などに再び滑空しても、残響を急に元の音量へ戻さない。
            FadeOutGlideAudio();
            return;
        }

        PlayGlideAudio();
    }

    private void OnDisable()
    {
        StopGlideAudioImmediately();
    }

    private void PlayGlideAudio()
    {
        if (glideLoopClip == null)
        {
            return;
        }

        EnsureAudioSource();
        if (glideAudioSource.isPlaying)
        {
            glideAudioSource.volume = volume;
            return;
        }

        glideAudioSource.clip = glideLoopClip;
        glideAudioSource.volume = volume;
        glideAudioSource.Play();
    }

    private void FadeOutGlideAudio()
    {
        if (glideAudioSource == null || !glideAudioSource.isPlaying)
        {
            return;
        }

        if (fadeOutDuration <= 0f)
        {
            StopGlideAudioImmediately();
            return;
        }

        glideAudioSource.volume = Mathf.MoveTowards(
            glideAudioSource.volume,
            0f,
            volume * Time.deltaTime / fadeOutDuration);

        if (glideAudioSource.volume <= 0f)
        {
            glideAudioSource.Stop();
        }
    }

    private void StopGlideAudioImmediately()
    {
        glidingDuration = 0f;

        if (glideAudioSource != null)
        {
            glideAudioSource.Stop();
            glideAudioSource.volume = volume;
        }
    }

    private void EnsureAudioSource()
    {
        if (glideAudioSource != null)
        {
            return;
        }

        glideAudioSource = gameObject.AddComponent<AudioSource>();
        glideAudioSource.playOnAwake = false;
        glideAudioSource.loop = true;
        glideAudioSource.spatialBlend = 0f;
    }
}
