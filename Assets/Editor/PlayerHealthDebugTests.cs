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
        PvModeState.SetActive(false);

        if (playerObject != null)
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void PvMode_BlocksDamageUntilDisabled()
    {
        PlayerHealth playerHealth = CreatePlayerHealth();

        PvModeState.SetActive(true);
        bool didDamageInPvMode = playerHealth.TryTakeDamage(1, 0f);

        Assert.That(didDamageInPvMode, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth));

        PvModeState.SetActive(false);
        bool didDamageAfterDisable = playerHealth.TryTakeDamage(1, 0f);

        Assert.That(didDamageAfterDisable, Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    [Test]
    public void DisablingPvMode_DoesNotDisableDebugInvincibility()
    {
        PlayerHealth playerHealth = CreatePlayerHealth();
        playerHealth.DebugInvincible = true;

        PvModeState.SetActive(true);
        PvModeState.SetActive(false);

        Assert.That(playerHealth.DebugInvincible, Is.True);
        Assert.That(playerHealth.TryTakeDamage(1, 0f), Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth));
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

    [Test]
    public void TryTakeDamage_WhenDiveAttacking_BlocksDamage()
    {
        PlayerHealth playerHealth = CreatePlayerHealth();
        playerObject.AddComponent<Rigidbody2D>();
        PlayerDiveAttackController diveAttack = playerObject.AddComponent<PlayerDiveAttackController>();

        Assert.That(diveAttack.TryStartDiveAttack(), Is.True);

        bool didDamage = playerHealth.TryTakeDamage(1, 0f);

        Assert.That(didDamage, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth));
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
