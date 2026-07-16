using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class FallThroughGroundSeamTests
{
    private const string ScenePath = "Assets/Scenes/FixScenes/Fix_CAPCOM.unity";
    private const string PrefabPath = "Assets/Prefabs/Environment/FallThruGroundSeam.prefab";
    private const string SeamPrefix = "FallThruGroundSeam_";
    private readonly List<Object> objectsToDestroy = new List<Object>();
    private SimulationMode2D previousSimulationMode;

    [SetUp]
    public void SetUp()
    {
        previousSimulationMode = Physics2D.simulationMode;
    }

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
        Physics2D.simulationMode = previousSimulationMode;
    }

    [Test]
    public void SeamPrefab_UsesOnlyStaticOneWayPhysicsComponents()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.layer, Is.EqualTo(LayerMask.NameToLayer("FallThroughFloor")));
        Assert.That(prefab.GetComponent<Rigidbody2D>(), Is.Null);
        Assert.That(prefab.GetComponents<MonoBehaviour>(), Is.Empty);

        EdgeCollider2D edge = prefab.GetComponent<EdgeCollider2D>();
        PlatformEffector2D effector = prefab.GetComponent<PlatformEffector2D>();
        Assert.That(edge, Is.Not.Null);
        Assert.That(edge.usedByEffector, Is.True);
        Assert.That(edge.points.Length, Is.EqualTo(3));
        Assert.That(effector, Is.Not.Null);
        Assert.That(effector.useOneWay, Is.True);
        Assert.That(effector.useOneWayGrouping, Is.True);
        Assert.That(effector.useSideFriction, Is.False);
    }

    [Test]
    public void FixCapcom_HasTenLocalizedScriptFreeSeamHelpers()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        GameObject[] helpers = transforms
            .Where(transform => transform.name.StartsWith(SeamPrefix))
            .Select(transform => transform.gameObject)
            .ToArray();

        Assert.That(helpers.Length, Is.EqualTo(10));
        for (int i = 0; i < helpers.Length; i++)
        {
            GameObject helper = helpers[i];
            EdgeCollider2D edge = helper.GetComponent<EdgeCollider2D>();
            PlatformEffector2D effector = helper.GetComponent<PlatformEffector2D>();

            Assert.That(helper.layer, Is.EqualTo(LayerMask.NameToLayer("FallThroughFloor")), helper.name);
            Assert.That(helper.GetComponent<Rigidbody2D>(), Is.Null, helper.name);
            Assert.That(helper.GetComponents<MonoBehaviour>(), Is.Empty, helper.name);
            Assert.That(edge, Is.Not.Null, helper.name);
            Assert.That(edge.usedByEffector, Is.True, helper.name);
            Assert.That(edge.points.Length, Is.EqualTo(3), helper.name);
            Assert.That(edge.points[1].y, Is.GreaterThan(edge.points[0].y + 0.04f), helper.name);
            Assert.That(edge.points[1].y, Is.GreaterThan(edge.points[2].y + 0.04f), helper.name);
            Assert.That(effector, Is.Not.Null, helper.name);
            Assert.That(effector.useOneWay, Is.True, helper.name);
        }
    }

    [TestCase(true, true)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(false, false)]
    public void SeamHelper_AllowsCrossingWithoutFalling(
        bool groundIsLeft,
        bool startsOnGround)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Physics2D.simulationMode = SimulationMode2D.Script;

        const float groundSurfaceY = 0.13f;
        const float platformSurfaceY = 0f;
        CreateGroundAndPlatform(groundIsLeft, groundSurfaceY, platformSurfaceY);
        CreateSeamHelper(groundIsLeft, groundSurfaceY, platformSurfaceY);

        bool startsOnLeft = startsOnGround == groundIsLeft;
        float startX = startsOnLeft ? -1.4f : 1.4f;
        float targetX = startsOnLeft ? 1.2f : -1.2f;
        float startSurfaceY = startsOnGround ? groundSurfaceY : platformSurfaceY;
        PlayerCollisionMover2D mover = CreatePlayer(
            new Vector2(startX, startSurfaceY + 1.47f),
            out Rigidbody2D rigidbody2d);

        Physics2D.SyncTransforms();
        SimulateSettling(rigidbody2d, 20);

        float horizontalSpeed = startsOnLeft ? 2f : -2f;
        float lowestFeetY = float.PositiveInfinity;
        bool reachedTarget = false;
        for (int i = 0; i < 180; i++)
        {
            float projectedSpeed = mover.ProjectHorizontalVelocityForNextFixedStep(horizontalSpeed);
            Vector2 velocity = rigidbody2d.linearVelocity;
            velocity.x = projectedSpeed;
            rigidbody2d.linearVelocity = velocity;
            Physics2D.Simulate(0.02f);

            lowestFeetY = Mathf.Min(lowestFeetY, rigidbody2d.position.y - 1.47f);
            reachedTarget = startsOnLeft
                ? rigidbody2d.position.x >= targetX
                : rigidbody2d.position.x <= targetX;
            if (reachedTarget)
            {
                break;
            }
        }

        Assert.That(reachedTarget, Is.True,
            $"Player failed to cross. groundIsLeft={groundIsLeft}, startsOnGround={startsOnGround}");
        Assert.That(lowestFeetY, Is.GreaterThan(-0.08f),
            $"Player fell through the transition. Lowest feet Y: {lowestFeetY}");
    }

    private void CreateGroundAndPlatform(bool groundIsLeft, float groundY, float platformY)
    {
        float groundX = groundIsLeft ? -2f : 2f;
        GameObject ground = CreateObject("Ground", new Vector2(groundX, groundY - 0.5f));
        ground.layer = LayerMask.NameToLayer("Ground");
        BoxCollider2D groundCollider = ground.AddComponent<BoxCollider2D>();
        groundCollider.size = new Vector2(4f, 1f);

        float platformX = groundIsLeft ? 2f : -2f;
        GameObject platform = CreateObject("FallThroughFloor", new Vector2(platformX, platformY - 0.25f));
        platform.layer = LayerMask.NameToLayer("FallThroughFloor");
        BoxCollider2D platformCollider = platform.AddComponent<BoxCollider2D>();
        platformCollider.size = new Vector2(4f, 0.5f);
        platformCollider.usedByEffector = true;
        ConfigureEffector(platform.AddComponent<PlatformEffector2D>());
    }

    private void CreateSeamHelper(bool groundIsLeft, float groundY, float platformY)
    {
        GameObject helper = CreateObject("SeamHelper", Vector2.zero);
        helper.layer = LayerMask.NameToLayer("FallThroughFloor");
        EdgeCollider2D edge = helper.AddComponent<EdgeCollider2D>();
        float peakY = Mathf.Max(groundY, platformY) + 0.05f;
        edge.points = groundIsLeft
            ? new[] { new Vector2(-1f, groundY), new Vector2(0f, peakY), new Vector2(1f, platformY) }
            : new[] { new Vector2(-1f, platformY), new Vector2(0f, peakY), new Vector2(1f, groundY) };
        edge.usedByEffector = true;
        ConfigureEffector(helper.AddComponent<PlatformEffector2D>());
    }

    private PlayerCollisionMover2D CreatePlayer(Vector2 position, out Rigidbody2D rigidbody2d)
    {
        GameObject player = CreateObject("Player", position);
        player.layer = LayerMask.NameToLayer("Player");
        rigidbody2d = player.AddComponent<Rigidbody2D>();
        rigidbody2d.gravityScale = 2f;
        rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rigidbody2d.constraints = RigidbodyConstraints2D.FreezeRotation;

        CapsuleCollider2D capsule = player.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(1.35f, 2.94f);
        capsule.direction = CapsuleDirection2D.Vertical;
        PhysicsMaterial2D material = new PhysicsMaterial2D("SeamTestNoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };
        objectsToDestroy.Add(material);
        capsule.sharedMaterial = material;

        PlayerCollisionMover2D mover = player.AddComponent<PlayerCollisionMover2D>();
        mover.SetSolidLayerMask(1 << LayerMask.NameToLayer("Ground"));
        mover.SetSkinWidth(0.03f);
        return mover;
    }

    private static void ConfigureEffector(PlatformEffector2D effector)
    {
        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        effector.surfaceArc = 165f;
        effector.useSideFriction = false;
        effector.useSideBounce = false;
    }

    private static void SimulateSettling(Rigidbody2D rigidbody2d, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            Vector2 velocity = rigidbody2d.linearVelocity;
            velocity.x = 0f;
            rigidbody2d.linearVelocity = velocity;
            Physics2D.Simulate(0.02f);
        }
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }
}
