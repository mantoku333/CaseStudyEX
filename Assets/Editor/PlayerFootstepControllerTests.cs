using System.Collections.Generic;
using System.Reflection;
using GameName.Audio;
using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("Gameplay")]
public sealed class PlayerFootstepControllerTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

    [TearDown]
    public void TearDown()
    {
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
    public void ResolveCurrentSurfaceProfile_UsesHighestPriorityFilteredZone()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int zoneLayer = LayerMask.NameToLayer("FallThroughFloor");
        Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0), "Ground layer must exist for footstep surface tests.");
        Assert.That(zoneLayer, Is.GreaterThanOrEqualTo(0), "FallThroughFloor layer must exist for footstep surface tests.");

        SurfaceAudioProfile lowPriorityProfile = CreateProfile("LowPrioritySurface");
        SurfaceAudioProfile highPriorityProfile = CreateProfile("HighPrioritySurface");
        SurfaceAudioProfile ignoredProfile = CreateProfile("IgnoredUnmaskedSurface");
        CreateGround(groundLayer);
        CreateZone("LowPriorityZone", zoneLayer, lowPriorityProfile, priority: 1);
        CreateZone("HighPriorityZone", zoneLayer, highPriorityProfile, priority: 10);
        CreateZone("IgnoredDefaultLayerZone", 0, ignoredProfile, priority: 100);

        PlayerFootstepController footstepController = CreateFootstepController(groundLayer, zoneLayer);

        SurfaceAudioProfile resolvedProfile =
            InvokePrivate<SurfaceAudioProfile>(footstepController, "ResolveCurrentSurfaceProfile");

        Assert.That(resolvedProfile, Is.SameAs(highPriorityProfile));
    }

    private PlayerFootstepController CreateFootstepController(int groundLayer, int zoneLayer)
    {
        GameObject playerObject = CreateObject("Player", new Vector2(0f, 1f));
        playerObject.AddComponent<Rigidbody2D>();
        PlayerFootstepController footstepController = playerObject.AddComponent<PlayerFootstepController>();
        SetPrivateField(footstepController, "surfaceLayerMask", (LayerMask)(1 << groundLayer));
        SetPrivateField(footstepController, "surfaceZoneLayerMask", (LayerMask)(1 << zoneLayer));
        SetPrivateField(footstepController, "surfaceProbeDistance", 2f);
        InvokePrivate(footstepController, "Awake");
        return footstepController;
    }

    private void CreateGround(int layer)
    {
        GameObject groundObject = CreateObject("Ground", new Vector2(0f, -0.1f));
        groundObject.layer = layer;
        BoxCollider2D groundCollider = groundObject.AddComponent<BoxCollider2D>();
        groundCollider.size = new Vector2(4f, 0.2f);
    }

    private void CreateZone(string name, int layer, SurfaceAudioProfile profile, int priority)
    {
        GameObject zoneObject = CreateObject(name, Vector2.zero);
        zoneObject.layer = layer;
        BoxCollider2D zoneCollider = zoneObject.AddComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
        zoneCollider.size = new Vector2(4f, 4f);

        SurfaceAudioZone zone = zoneObject.AddComponent<SurfaceAudioZone>();
        SetPrivateField(zone, "profile", profile);
        SetPrivateField(zone, "priority", priority);
    }

    private SurfaceAudioProfile CreateProfile(string name)
    {
        SurfaceAudioProfile profile = ScriptableObject.CreateInstance<SurfaceAudioProfile>();
        profile.name = name;
        objectsToDestroy.Add(profile);
        return profile;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' must exist.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"Private method '{methodName}' must exist.");
        method.Invoke(target, null);
    }

    private static T InvokePrivate<T>(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"Private method '{methodName}' must exist.");
        return (T)method.Invoke(target, null);
    }
}
