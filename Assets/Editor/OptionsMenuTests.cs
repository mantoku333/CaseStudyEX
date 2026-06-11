using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class OptionsMenuTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (objectsToDestroy[i] != null)
            {
                Object.DestroyImmediate(objectsToDestroy[i]);
            }
        }

        objectsToDestroy.Clear();
    }

    [Test]
    public void CaptureAndRestorePausedPlayerVelocity_PreservesPlayerRigidbodyVelocity()
    {
        OptionsMenu menu = CreateMenu();
        Rigidbody2D playerRigidbody = CreateTaggedPlayer();
        Vector2 launchVelocity = new Vector2(3.25f, 12.5f);
        float angularVelocity = 22.5f;
        playerRigidbody.linearVelocity = launchVelocity;
        playerRigidbody.angularVelocity = angularVelocity;

        InvokePrivate(menu, "CapturePausedPlayerVelocity", playerRigidbody.gameObject);

        Assert.That(playerRigidbody.linearVelocity.x, Is.EqualTo(launchVelocity.x).Within(0.001f));
        Assert.That(playerRigidbody.linearVelocity.y, Is.EqualTo(launchVelocity.y).Within(0.001f));
        Assert.That(playerRigidbody.angularVelocity, Is.EqualTo(angularVelocity).Within(0.001f));

        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;

        InvokePrivate(menu, "RestorePausedPlayerVelocity");

        Assert.That(playerRigidbody.linearVelocity.x, Is.EqualTo(launchVelocity.x).Within(0.001f));
        Assert.That(playerRigidbody.linearVelocity.y, Is.EqualTo(launchVelocity.y).Within(0.001f));
        Assert.That(playerRigidbody.angularVelocity, Is.EqualTo(angularVelocity).Within(0.001f));
    }

    private OptionsMenu CreateMenu()
    {
        GameObject menuObject = new GameObject("OptionsMenu");
        objectsToDestroy.Add(menuObject);
        return menuObject.AddComponent<OptionsMenu>();
    }

    private Rigidbody2D CreateTaggedPlayer()
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.tag = "Player";
        objectsToDestroy.Add(playerObject);

        Rigidbody2D rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        rigidbody2D.gravityScale = 0f;
        return rigidbody2D;
    }

    private static void InvokePrivate(OptionsMenu menu, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(OptionsMenu).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        method.Invoke(menu, arguments);
    }
}
