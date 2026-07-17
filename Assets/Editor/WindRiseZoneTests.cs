using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WindRiseZoneTests
{
    private GameObject windObject;
    private GameObject playerObject;

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(windObject);
        Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void TryBuildTarget_AcceptsPlayerBodyPolygon()
    {
        WindRiseZone windRise = CreateWindRise();
        PolygonCollider2D bodyPolygon = CreatePlayer();

        Assert.That(TryBuildTarget(windRise, bodyPolygon), Is.True);
    }

    [Test]
    public void TryBuildTarget_RejectsPlayerChildDamageCollider()
    {
        WindRiseZone windRise = CreateWindRise();
        CreatePlayer();
        GameObject damageObject = new GameObject("DamageCollider");
        damageObject.transform.SetParent(playerObject.transform);
        BoxCollider2D damageCollider = damageObject.AddComponent<BoxCollider2D>();

        Assert.That(TryBuildTarget(windRise, damageCollider), Is.False);
    }

    private WindRiseZone CreateWindRise()
    {
        windObject = new GameObject("WindRise");
        windObject.AddComponent<BoxCollider2D>().isTrigger = true;
        return windObject.AddComponent<WindRiseZone>();
    }

    private PolygonCollider2D CreatePlayer()
    {
        playerObject = new GameObject("Player");
        playerObject.tag = "Player";
        playerObject.AddComponent<Rigidbody2D>();
        PolygonCollider2D polygon = playerObject.AddComponent<PolygonCollider2D>();
        polygon.points = new[]
        {
            new Vector2(-0.5f, 1.0f),
            new Vector2(0.5f, 1.0f),
            new Vector2(0.5f, -0.25f),
            new Vector2(0.25f, -0.5f),
            new Vector2(-0.25f, -0.5f),
            new Vector2(-0.5f, -0.25f)
        };
        return polygon;
    }

    private static bool TryBuildTarget(WindRiseZone windRise, Collider2D collider)
    {
        MethodInfo method = typeof(WindRiseZone).GetMethod(
            "TryBuildTarget",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        object[] arguments = { collider, null };
        return (bool)method.Invoke(windRise, arguments);
    }
}
