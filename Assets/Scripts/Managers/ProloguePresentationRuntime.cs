using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProloguePresentationRuntime : MonoBehaviour
{
    private const string RuntimeObjectName = "[ProloguePresentationRuntime]";
    private const string StagePrefix = "PG_STAGE_";
    private const string ActorPrefix = "PG_ACTOR_";
    private const string MarkerPrefix = "PG_MARKER_";
    private const string IrisActorId = "iris";

    private static ProloguePresentationRuntime instance;

    private readonly Dictionary<string, AudioClip> clipByName =
        new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warnedMissingObjects =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warnedMissingClips =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [SerializeField] private bool logMissingClipWarnings;

    private AudioSource rainSource;
    private AudioSource sfxSource;
    private bool clipCacheBuilt;

    public static ProloguePresentationRuntime Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<ProloguePresentationRuntime>();
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
        EnsureAudioSources();
    }

    public IEnumerator FadeBlack(float alpha, float durationSeconds)
    {
        yield return StoryOverlayFader.Instance.FadeTo(alpha, durationSeconds, Color.black);
    }

    public void SetStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return;
        }

        string targetName = StagePrefix + stageId.Trim();
        Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tf = allTransforms[i];
            if (tf == null || !tf.name.StartsWith(StagePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            bool active = string.Equals(tf.name, targetName, StringComparison.Ordinal);
            if (tf.gameObject.activeSelf != active)
            {
                tf.gameObject.SetActive(active);
            }
        }
    }

    public void PlaySfx(string clipKey)
    {
        EnsureAudioSources();

        AudioClip clip = ResolveClip(clipKey);
        if (clip == null)
        {
            if (logMissingClipWarnings)
            {
                WarnMissingClipOnce(clipKey);
            }
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    public void StartRain(string clipKey, float volume)
    {
        EnsureAudioSources();

        AudioClip clip = ResolveClip(clipKey);
        if (clip == null)
        {
            if (logMissingClipWarnings)
            {
                WarnMissingClipOnce(clipKey);
            }
            return;
        }

        rainSource.loop = true;
        rainSource.clip = clip;
        rainSource.volume = Mathf.Clamp01(volume);

        if (!rainSource.isPlaying)
        {
            rainSource.Play();
        }
    }

    public IEnumerator StopRain(float fadeSeconds)
    {
        EnsureAudioSources();

        if (!rainSource.isPlaying)
        {
            yield break;
        }

        float startVolume = rainSource.volume;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeSeconds);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rainSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        rainSource.Stop();
        rainSource.clip = null;
        rainSource.volume = startVolume;
    }

    public void SetActorActive(string actorId, bool active)
    {
        Transform actor = ResolveActorTransform(actorId);
        if (actor != null)
        {
            actor.gameObject.SetActive(active);
        }
    }

    public void PlaceActor(string actorId, string markerId)
    {
        Transform actor = ResolveActorTransform(actorId);
        Transform marker = FindNamedTransform(MarkerPrefix, markerId);
        if (actor == null || marker == null)
        {
            return;
        }

        SetActorWorldPosition(actor, marker.position, resetVelocity: true);
    }

    public IEnumerator MoveActor(string actorId, string markerId, float durationSeconds)
    {
        Transform actor = ResolveActorTransform(actorId);
        Transform marker = FindNamedTransform(MarkerPrefix, markerId);
        if (actor == null || marker == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.01f, durationSeconds);
        Vector3 start = actor.position;
        Vector3 end = marker.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetActorWorldPosition(actor, Vector3.Lerp(start, end, t), resetVelocity: false);
            yield return null;
        }

        SetActorWorldPosition(actor, end, resetVelocity: true);
    }

    public void FaceActor(string actorId, string direction)
    {
        Transform actor = ResolveActorTransform(actorId);
        if (actor == null || string.IsNullOrWhiteSpace(direction))
        {
            return;
        }

        string dir = direction.Trim().ToLowerInvariant();
        Vector3 scale = actor.localScale;
        float absX = Mathf.Abs(scale.x);

        if (dir == "left" || dir == "l")
        {
            scale.x = -absX;
            actor.localScale = scale;
        }
        else if (dir == "right" || dir == "r")
        {
            scale.x = absX;
            actor.localScale = scale;
        }
    }

    public IEnumerator WaitRealtime(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void EnsureAudioSources()
    {
        if (rainSource == null)
        {
            rainSource = gameObject.AddComponent<AudioSource>();
            rainSource.playOnAwake = false;
            rainSource.loop = true;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }
    }

    private Transform FindNamedTransform(string prefix, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string expectedName = prefix + id.Trim();
        string expectedNameWithUnderscore = "_" + expectedName;
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform tf = transforms[i];
            if (tf != null &&
                (string.Equals(tf.name, expectedName, StringComparison.Ordinal) ||
                 string.Equals(tf.name, expectedNameWithUnderscore, StringComparison.Ordinal)))
            {
                return tf;
            }
        }

        if (warnedMissingObjects.Add(expectedName))
        {
            Debug.LogWarning($"[ProloguePresentationRuntime] Object not found: '{expectedName}'.");
        }

        return null;
    }

    private Transform ResolveActorTransform(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        string trimmedActorId = actorId.Trim();
        if (string.Equals(trimmedActorId, IrisActorId, StringComparison.OrdinalIgnoreCase))
        {
            global::PlayerController player =
                UnityEngine.Object.FindFirstObjectByType<global::PlayerController>(FindObjectsInactive.Include);

            if (player != null)
            {
                return player.transform;
            }

            string warningKey = "PlayerControllerForIris";
            if (warnedMissingObjects.Add(warningKey))
            {
                Debug.LogWarning("[ProloguePresentationRuntime] PlayerController not found for actor 'iris'.");
            }

            return null;
        }

        return FindNamedTransform(ActorPrefix, trimmedActorId);
    }

    private static void SetActorWorldPosition(Transform actor, Vector3 worldPosition, bool resetVelocity)
    {
        if (actor == null)
        {
            return;
        }

        actor.position = worldPosition;

        Rigidbody2D rigidbody2D = FindAttachedRigidbody2D(actor);
        if (rigidbody2D == null)
        {
            return;
        }

        rigidbody2D.position = new Vector2(worldPosition.x, worldPosition.y);

        if (resetVelocity)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
            rigidbody2D.Sleep();
        }
    }

    private static Rigidbody2D FindAttachedRigidbody2D(Transform actor)
    {
        if (actor == null)
        {
            return null;
        }

        Rigidbody2D rigidbody2D = actor.GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            return rigidbody2D;
        }

        return actor.GetComponentInParent<Rigidbody2D>();
    }

    private AudioClip ResolveClip(string clipKey)
    {
        if (string.IsNullOrWhiteSpace(clipKey))
        {
            return null;
        }

        EnsureClipCache();

        string key = clipKey.Trim();
        if (clipByName.TryGetValue(key, out AudioClip clip) && clip != null)
        {
            return clip;
        }

        if (key.Contains("/"))
        {
            clip = Resources.Load<AudioClip>(key);
            if (clip != null)
            {
                clipByName[key] = clip;
                return clip;
            }
        }

        return null;
    }

    private void WarnMissingClipOnce(string clipKey)
    {
        string key = string.IsNullOrWhiteSpace(clipKey) ? "(null)" : clipKey.Trim();
        if (warnedMissingClips.Add(key))
        {
            Debug.LogWarning($"[ProloguePresentationRuntime] Audio clip not found: '{key}'.");
        }
    }

    private void EnsureClipCache()
    {
        if (clipCacheBuilt)
        {
            return;
        }

        clipCacheBuilt = true;
        AudioClip[] loadedClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        for (int i = 0; i < loadedClips.Length; i++)
        {
            AudioClip clip = loadedClips[i];
            if (clip == null || string.IsNullOrWhiteSpace(clip.name))
            {
                continue;
            }

            if (!clipByName.ContainsKey(clip.name))
            {
                clipByName.Add(clip.name, clip);
            }
        }
    }
}
