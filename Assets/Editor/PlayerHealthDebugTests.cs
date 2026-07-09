using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerHealthDebugTests
{
    private GameObject playerObject;

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null)
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void DebugInvincible_BlocksDamageUntilDisabled()
    {
        PlayerHealth playerHealth = CreatePlayerHealth();

        playerHealth.DebugInvincible = true;
        bool didDamageWhileInvincible = playerHealth.TryTakeDamage(1, 0f);

        Assert.That(didDamageWhileInvincible, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth));

        playerHealth.DebugInvincible = false;
        bool didDamageAfterDisable = playerHealth.TryTakeDamage(1, 0f);

        Assert.That(didDamageAfterDisable, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    private PlayerHealth CreatePlayerHealth()
    {
        playerObject = new GameObject("Player");
        playerObject.tag = "Player";

        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        playerHealth.RestoreFullHealth();
        return playerHealth;
    }
}
