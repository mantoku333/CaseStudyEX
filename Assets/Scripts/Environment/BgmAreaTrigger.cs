using Metroidvania.Player;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Audio/BGM Area Trigger 2D")]
public sealed class BgmAreaTrigger : MonoBehaviour
{
    [SerializeField] private StageBgmController stageBgm;
    [SerializeField] private AudioClip bgmClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.2f;
    [SerializeField] private int priority;

    private bool playerInside;
    private Collider2D triggerCollider;
    private Collider2D playerBodyCollider;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ResolveStageBgm();
    }

    private void OnDisable()
    {
        ClearRequest();
    }

    private void FixedUpdate()
    {
        // ワープや無効化で OnTriggerExit2D が来ない場合も、エリア曲を残さない。
        if (playerInside &&
            (playerBodyCollider == null ||
             !playerBodyCollider.enabled ||
             triggerCollider == null ||
             !triggerCollider.IsTouching(playerBodyCollider)))
        {
            ClearRequest();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerInside || !PlayerBodyColliderUtility.IsPlayerBodyCollider(other))
        {
            return;
        }

        playerInside = true;
        playerBodyCollider = other;
        ResolveStageBgm();
        stageBgm?.SetAreaBgm(this, bgmClip, volume, priority);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerInside || (other != playerBodyCollider && !PlayerBodyColliderUtility.IsPlayerBodyCollider(other)))
        {
            return;
        }

        ClearRequest();
    }

    private void ClearRequest()
    {
        if (stageBgm != null)
        {
            stageBgm.ClearAreaBgm(this);
        }

        playerInside = false;
        playerBodyCollider = null;
    }

    private void ResolveStageBgm()
    {
        if (stageBgm == null)
        {
            stageBgm = FindFirstObjectByType<StageBgmController>(FindObjectsInactive.Include);
        }
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
        EnsureTriggerCollider();
    }
#endif
}
