using Metroidvania.Player;
using Player;
using UnityEngine;

public class RespawnOnFall : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [SerializeField] private Transform respawnPoint;
    [SerializeField, Min(0)] private int respawnDamage = 1;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryResolvePlayer(other, out PlayerHealth playerHealth, out PlayerDamageFlash playerDamageFlash))
        {
            return;
        }

        if (respawnPoint == null)
        {
            return;
        }

        Vector3 respawnPosition = respawnPoint.position;
        Rigidbody2D rb = other.attachedRigidbody != null
            ? other.attachedRigidbody
            : other.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = respawnPosition;
        }

        other.transform.position = respawnPosition;
        TryApplyRespawnDamage(playerHealth, playerDamageFlash);
    }

    private static bool TryResolvePlayer(
        Collider2D other,
        out PlayerHealth playerHealth,
        out PlayerDamageFlash playerDamageFlash)
    {
        playerHealth = null;
        playerDamageFlash = null;

        if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                other,
                out playerHealth,
                out Collider2D playerCollider))
        {
            return false;
        }

        if (!MatchesPlayerTag(playerCollider.gameObject, playerHealth))
        {
            return false;
        }

        playerDamageFlash = ResolvePlayerDamageFlash(playerCollider, playerHealth);
        return true;
    }

    private void TryApplyRespawnDamage(PlayerHealth playerHealth, PlayerDamageFlash playerDamageFlash)
    {
        if (playerHealth == null || respawnDamage <= 0)
        {
            return;
        }

        if (playerHealth.TryTakeDamage(respawnDamage))
        {
            playerDamageFlash?.PlayFlashForced();
        }
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

    private static bool MatchesPlayerTag(GameObject hitObject, PlayerHealth playerHealth)
    {
        if (hitObject != null && hitObject.CompareTag(PlayerTag))
        {
            return true;
        }

        if (playerHealth != null && playerHealth.CompareTag(PlayerTag))
        {
            return true;
        }

        return playerHealth != null &&
               playerHealth.transform.root != null &&
               playerHealth.transform.root.CompareTag(PlayerTag);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        respawnDamage = Mathf.Max(0, respawnDamage);
    }
#endif
}
