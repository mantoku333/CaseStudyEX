using System.Collections;
using System.Reflection;
using Metroidvania.Player;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerDamageFlashTests
{
    private static readonly Color OriginalColor = new Color(0.25f, 0.5f, 0.75f, 1f);

    private GameObject playerObject;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

        if (playerObject != null)
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void PlayFlashForced_WhenRestarted_ReplaysAndRestoresOriginalColor()
    {
        PlayerDamageFlash damageFlash = CreateDamageFlash(out SpriteRenderer playerRenderer);
        SetPrivateField(damageFlash, "flashDuration", 0.04f);
        SetPrivateField(damageFlash, "flashRepeatCount", 1);
        SetPrivateField(damageFlash, "normalDuration", 0f);

        damageFlash.PlayFlashForced();
        Coroutine firstFlashCoroutine = GetPrivateField<Coroutine>(damageFlash, "flashCoroutine");
        Assert.That(playerRenderer.color, Is.EqualTo(Color.red));

        damageFlash.PlayFlashForced();
        Coroutine restartedFlashCoroutine = GetPrivateField<Coroutine>(damageFlash, "flashCoroutine");
        Assert.That(playerRenderer.color, Is.EqualTo(Color.red));
        Assert.That(restartedFlashCoroutine, Is.Not.SameAs(firstFlashCoroutine));

        InvokeLifecycleMethod(damageFlash, "OnDisable");
        Assert.That(playerRenderer.color, Is.EqualTo(OriginalColor));

        IEnumerator flashRoutine = InvokeCoroutineMethod(damageFlash, "FlashCoroutine");
        Assert.That(flashRoutine.MoveNext(), Is.True);
        Assert.That(playerRenderer.color, Is.EqualTo(Color.red));
        Assert.That(flashRoutine.MoveNext(), Is.False);
        Assert.That(playerRenderer.color, Is.EqualTo(OriginalColor));
    }

    [Test]
    public void OnDisable_DuringFlash_RestoresColorAndAllowsFlashAfterReenable()
    {
        PlayerDamageFlash damageFlash = CreateDamageFlash(out SpriteRenderer playerRenderer);
        SetPrivateField(damageFlash, "flashDuration", 0.02f);
        SetPrivateField(damageFlash, "flashRepeatCount", 1);
        SetPrivateField(damageFlash, "normalDuration", 0f);

        damageFlash.PlayFlashForced();
        Assert.That(playerRenderer.color, Is.EqualTo(Color.red));

        damageFlash.enabled = false;
        InvokeLifecycleMethod(damageFlash, "OnDisable");
        Assert.That(playerRenderer.color, Is.EqualTo(OriginalColor));
        Assert.That(GetPrivateField<Coroutine>(damageFlash, "flashCoroutine"), Is.Null);
        Assert.That(GetPrivateField<float>(damageFlash, "nextFlashTime"), Is.EqualTo(0f));

        damageFlash.enabled = true;
        damageFlash.PlayFlash();
        Assert.That(playerRenderer.color, Is.EqualTo(Color.red));

        InvokeLifecycleMethod(damageFlash, "OnDisable");
        Assert.That(playerRenderer.color, Is.EqualTo(OriginalColor));
    }

    private PlayerDamageFlash CreateDamageFlash(out SpriteRenderer playerRenderer)
    {
        playerObject = new GameObject("Player");
        playerRenderer = playerObject.AddComponent<SpriteRenderer>();
        playerRenderer.color = OriginalColor;

        PlayerDamageFlash damageFlash = playerObject.AddComponent<PlayerDamageFlash>();
        InvokeLifecycleMethod(damageFlash, "Awake");
        return damageFlash;
    }

    private static void InvokeLifecycleMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private static IEnumerator InvokeCoroutineMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return (IEnumerator)method.Invoke(target, null);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }
}
