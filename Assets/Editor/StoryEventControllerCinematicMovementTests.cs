using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class StoryEventControllerCinematicMovementTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateInstanceAndPublic =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private GameObject playerObject;
    private PlayerController player;
    private GameObject eventObject;
    private StoryEventController storyEvent;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Player");
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        playerObject.AddComponent<BoxCollider2D>();
        player = playerObject.AddComponent<PlayerController>();

        eventObject = new GameObject("StoryEvent");
        storyEvent = eventObject.AddComponent<StoryEventController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (eventObject != null)
        {
            Object.DestroyImmediate(eventObject);
        }

        if (playerObject != null)
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void RestoreCinematicState_ClearsMovementStartedByTheEvent()
    {
        SetPrivateField("restoreExternalMovementStateOnExit", true);
        Invoke("CaptureCinematicState");
        Invoke("ApplyCinematicState");

        player.StartExternalMoveToX(player.transform.position.x + 10f);
        Assert.That(player.IsExternalMovementActive, Is.True);

        Invoke("RestoreCinematicState");

        Assert.That(player.IsExternalControlLocked, Is.False);
        Assert.That(player.IsExternalMovementActive, Is.False,
            "A timeline move must not keep overriding input after the cinematic exits.");
    }

    [Test]
    public void RestoreCinematicState_WithoutMovementRestore_PreservesExistingEventBehaviour()
    {
        Invoke("CaptureCinematicState");
        Invoke("ApplyCinematicState");

        player.StartExternalMoveToX(player.transform.position.x + 10f);
        Invoke("RestoreCinematicState");

        Assert.That(player.IsExternalMovementActive, Is.True,
            "Events that have not opted in must retain their existing scripted-movement behaviour.");
    }

    [Test]
    public void RestoreCinematicState_WithMovementRestore_RestoresMovementThatPrecededTheEvent()
    {
        const float originalTargetX = 4f;
        const float eventTargetX = 10f;
        player.StartExternalMoveToX(originalTargetX);
        SetPrivateField("restoreExternalMovementStateOnExit", true);

        Invoke("CaptureCinematicState");
        Invoke("ApplyCinematicState");
        player.StartExternalMoveToX(eventTargetX);
        Invoke("RestoreCinematicState");

        Assert.That(player.IsExternalMovementActive, Is.True);
        Assert.That(GetPlayerPrivateField<float>("externalMovementTargetX"), Is.EqualTo(originalTargetX));
    }

    [Test]
    public void EnsurePlayerControlOnEventExit_ReleasesCinematicControlAndMovement()
    {
        SetPrivateField("ensurePlayerControlOnExit", true);
        player.SetExternalControlLocked(true);
        player.SetExternalMovementSuppressed(true);
        player.StartExternalMoveToX(player.transform.position.x + 10f);

        Invoke("EnsurePlayerControlOnEventExit");

        Assert.That(player.enabled, Is.True);
        Assert.That(player.IsExternalControlLocked, Is.False);
        Assert.That(player.IsExternalMovementSuppressed, Is.False);
        Assert.That(player.IsExternalMovementActive, Is.False);
    }

    private void Invoke(string methodName)
    {
        MethodInfo method = typeof(StoryEventController).GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found.");
        method.Invoke(storyEvent, null);
    }

    private void SetPrivateField(string fieldName, object value)
    {
        FieldInfo field = typeof(StoryEventController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
        field.SetValue(storyEvent, value);
    }

    private T GetPlayerPrivateField<T>(string fieldName)
    {
        FieldInfo field = typeof(PlayerController).GetField(fieldName, PrivateInstanceAndPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
        return (T)field.GetValue(player);
    }
}
