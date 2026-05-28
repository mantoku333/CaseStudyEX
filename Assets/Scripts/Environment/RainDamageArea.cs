using Metroidvania.Player;
using Player;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Environment/Rain Damage Area 2D")]
public sealed class RainDamageArea : MonoBehaviour
{
    private const int HitCapacity = 32;

    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0f)] private float damageIntervalSeconds = 1f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.55f, 1f, 0.25f);

    private readonly Collider2D[] overlapHits = new Collider2D[HitCapacity];
    private ContactFilter2D overlapFilter;
    private BoxCollider2D areaCollider;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        BuildOverlapFilter();
    }

    private void FixedUpdate()
    {
        DamagePlayersInArea();
    }

    private void DamagePlayersInArea()
    {
        if (areaCollider == null || !areaCollider.enabled)
        {
            return;
        }

        int hitCount = areaCollider.Overlap(overlapFilter, overlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapHits[i];
            overlapHits[i] = null;
            TryDamagePlayer(hit);
        }
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (!TryResolvePlayer(
                other,
                out PlayerHealth playerHealth,
                out PlayerDamageFlash playerDamageFlash))
        {
            return;
        }

        if (playerHealth.TryTakeDamage(damage, damageIntervalSeconds))
        {
            playerDamageFlash?.PlayFlashForced();
        }
    }

    private bool TryResolvePlayer(
        Collider2D candidate,
        out PlayerHealth playerHealth,
        out PlayerDamageFlash playerDamageFlash)
    {
        playerHealth = null;
        playerDamageFlash = null;

        if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(candidate, out playerHealth, out Collider2D bodyCollider))
        {
            return false;
        }

        if (!MatchesPlayerTag(bodyCollider.gameObject, playerHealth))
        {
            return false;
        }

        playerDamageFlash = ResolvePlayerDamageFlash(bodyCollider, playerHealth);
        return true;
    }

    private static PlayerDamageFlash ResolvePlayerDamageFlash(Collider2D playerCollider, PlayerHealth playerHealth)
    {
        PlayerDamageFlash damageFlash = playerCollider.GetComponent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerCollider.GetComponentInParent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerHealth.GetComponent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerHealth.GetComponentInParent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        return playerHealth.GetComponentInChildren<PlayerDamageFlash>(true);
    }

    private bool MatchesPlayerTag(GameObject hitObject, PlayerHealth playerHealth)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return true;
        }

        if (hitObject != null && hitObject.CompareTag(playerTag))
        {
            return true;
        }

        if (playerHealth.CompareTag(playerTag))
        {
            return true;
        }

        return playerHealth.transform.root != null && playerHealth.transform.root.CompareTag(playerTag);
    }

    private void EnsureTriggerCollider()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<BoxCollider2D>();
        }

        if (areaCollider != null)
        {
            areaCollider.isTrigger = true;
        }
    }

    private void BuildOverlapFilter()
    {
        overlapFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        overlapFilter.SetLayerMask(Physics2D.AllLayers);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        damage = Mathf.Max(1, damage);
        damageIntervalSeconds = Mathf.Max(0f, damageIntervalSeconds);
        EnsureTriggerCollider();
        BuildOverlapFilter();
    }

    private void OnDrawGizmosSelected()
    {
        EnsureTriggerCollider();
        if (areaCollider == null)
        {
            return;
        }

        Bounds bounds = areaCollider.bounds;
        Color previousColor = Gizmos.color;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Gizmos.color = previousColor;
    }
#endif
}
