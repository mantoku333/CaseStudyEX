using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitStopController : MonoBehaviour
{
    private const float DefaultDuration = 0.15f;
    private const float PlayerToEnemyDelay = 0.05f;

    private static HitStopController instance;
    private static int externalPauseDepth;

    [SerializeField, Min(0f)] private float normalParryDuration = 0.2f;
    [SerializeField, Min(0f)] private float justParryDuration = 0.35f;

    private Coroutine activeRoutine;
    private Coroutine delayedRoutine;
    private bool hitStopActive;
    private float restoreTimeScale = 1f;
    private float stopUntilUnscaledTime;
    private float delayedStopStartUnscaledTime;
    private float delayedStopDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void RequestPlayerToEnemy()
    {
        HitStopController controller = EnsureInstance();
        controller.RequestInternal(DefaultDuration, PlayerToEnemyDelay);
    }

    public static void RequestEnemyToPlayer()
    {
        RequestDefault();
    }

    public static void RequestParry()
    {
        RequestDefault();
    }

    public static void RequestNormalParry()
    {
        HitStopController controller = EnsureInstance();
        controller.RequestInternal(controller.normalParryDuration, 0f);
    }

    public static void RequestJustParry()
    {
        HitStopController controller = EnsureInstance();
        controller.RequestInternal(controller.justParryDuration, 0f);
    }

    public static void Request(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        EnsureInstance().RequestInternal(duration, 0f);
    }

    public static void BeginExternalPause()
    {
        HitStopController controller = EnsureInstance();
        controller.CancelActiveHitStop();
        externalPauseDepth++;
    }

    public static void EndExternalPause()
    {
        if (externalPauseDepth > 0)
        {
            externalPauseDepth--;
        }
    }

    private static void RequestDefault()
    {
        HitStopController controller = EnsureInstance();
        controller.RequestInternal(DefaultDuration, 0f);
    }

    private static HitStopController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        HitStopController existing = FindFirstObjectByType<HitStopController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            if (instance.transform.parent != null)
            {
                instance.transform.SetParent(null, true);
            }

            MakePersistentIfPlaying(instance.gameObject);
            return instance;
        }

        GameObject gameObject = new GameObject(nameof(HitStopController));
        MakePersistentIfPlaying(gameObject);
        instance = gameObject.AddComponent<HitStopController>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        MakePersistentIfPlaying(gameObject);
    }

    private static void MakePersistentIfPlaying(GameObject target)
    {
        if (!Application.isPlaying || target == null)
        {
            return;
        }

        DontDestroyOnLoad(target);
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        RestoreTimeScaleIfOwned();
        instance = null;
    }

    private void RequestInternal(float duration, float delay)
    {
        if (!Application.isPlaying || duration <= 0f || externalPauseDepth > 0)
        {
            return;
        }

        if (delay > 0f && !hitStopActive)
        {
            QueueDelayedHitStop(duration, delay);
            return;
        }

        if (!hitStopActive && Mathf.Approximately(Time.timeScale, 0f))
        {
            return;
        }

        stopUntilUnscaledTime = Mathf.Max(stopUntilUnscaledTime, Time.unscaledTime + duration);
        if (hitStopActive)
        {
            return;
        }

        restoreTimeScale = Time.timeScale;
        hitStopActive = true;
        Time.timeScale = 0f;
        activeRoutine = StartCoroutine(HitStopRoutine());
    }

    private void QueueDelayedHitStop(float duration, float delay)
    {
        float requestedStartTime = Time.unscaledTime + delay;
        delayedStopDuration = Mathf.Max(delayedStopDuration, duration);

        if (delayedRoutine != null)
        {
            delayedStopStartUnscaledTime = Mathf.Min(delayedStopStartUnscaledTime, requestedStartTime);
            return;
        }

        delayedStopStartUnscaledTime = requestedStartTime;
        delayedRoutine = StartCoroutine(DelayedHitStopRoutine());
    }

    private IEnumerator DelayedHitStopRoutine()
    {
        while (Time.unscaledTime < delayedStopStartUnscaledTime)
        {
            yield return null;
        }

        float duration = delayedStopDuration;
        delayedRoutine = null;
        delayedStopStartUnscaledTime = 0f;
        delayedStopDuration = 0f;
        RequestInternal(duration, 0f);
    }

    private IEnumerator HitStopRoutine()
    {
        while (Time.unscaledTime < stopUntilUnscaledTime)
        {
            yield return null;
        }

        activeRoutine = null;
        RestoreTimeScaleIfOwned();
    }

    private void CancelActiveHitStop()
    {
        if (delayedRoutine != null)
        {
            StopCoroutine(delayedRoutine);
            delayedRoutine = null;
            delayedStopStartUnscaledTime = 0f;
            delayedStopDuration = 0f;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        RestoreTimeScaleIfOwned();
    }

    private void RestoreTimeScaleIfOwned()
    {
        if (!hitStopActive)
        {
            return;
        }

        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = restoreTimeScale;
        }

        hitStopActive = false;
        stopUntilUnscaledTime = 0f;
    }
}
