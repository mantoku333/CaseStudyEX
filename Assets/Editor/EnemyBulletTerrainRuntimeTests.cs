using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using Metroidvania.Enemy;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyBulletTerrainRuntimeTests
{
    private const string MainScene = "Assets/Scenes/FixScenes/Fix_0925_NYAA.unity";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private SimulationMode2D previousSimulationMode;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (TestContext.CurrentContext.Test.MethodName == nameof(MainScene_UnnumberedEnemy_BlocksRightShot_AndFiresLeft))
        {
            Application.logMessageReceived += ExpectKnownSceneValidationMessage;
            UnityEngine.SceneManagement.Scene scene;
            try { scene = EditorSceneManager.OpenScene(MainScene); }
            finally { Application.logMessageReceived -= ExpectKnownSceneValidationMessage; }
            // Keep saved enemy placement and actual terrain. Disable unrelated gameplay
            // and damage sources so the assertions concern this enemy's shots only.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null) behaviour.enabled = false;
                if (root == null) continue;
                foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
                    if (collider != null) collider.enabled = (TerrainMask().value & (1 << collider.gameObject.layer)) != 0;
            }
            foreach (GameObject originalPlayer in GameObject.FindGameObjectsWithTag("Player"))
                originalPlayer.SetActive(false);
            foreach (EnemyRangedAttack attack in Object.FindObjectsByType<EnemyRangedAttack>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (attack.name != "Enemy_Ranged") continue;
                // Freeze before entering Play Mode so the test starts at saved placement.
                Rigidbody2D body = attack.GetComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        previousSimulationMode = Physics2D.simulationMode;
        EnemyGameplayPause.ResetCache();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Physics2D.simulationMode = previousSimulationMode;
        Time.timeScale = 1f;
        foreach (GameObject instance in objects)
            if (instance != null) Object.DestroyImmediate(instance);
        objects.Clear();
        EnemyGameplayPause.ResetCache();
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator MainScene_UnnumberedEnemy_BlocksRightShot_AndFiresLeft()
    {
        EnemyRangedAttack attack = null;
        foreach (EnemyRangedAttack candidate in Object.FindObjectsByType<EnemyRangedAttack>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.name != "Enemy_Ranged") continue;
            Assert.That(attack, Is.Null, "Select the unnumbered instance only.");
            attack = candidate;
        }
        Assert.That(attack, Is.Not.Null);
        Assert.That(attack.transform.localPosition.x, Is.EqualTo(477.41f).Within(0.01f));
        Assert.That(attack.transform.localPosition.y, Is.EqualTo(52.88f).Within(0.01f));
        Collider2D enemyBody = attack.GetComponent<Collider2D>();
        enemyBody.enabled = true;
        Rigidbody2D enemyBodyRb = attack.GetComponent<Rigidbody2D>();
        enemyBodyRb.gravityScale = 0f;
        enemyBodyRb.constraints = RigidbodyConstraints2D.FreezeAll;

        PlayerHealth player = CreatePlayer((Vector2)attack.transform.position + Vector2.right * 6f);
        SetField(attack, "playerTransform", player.transform);
        SetField(attack, "windupDuration", 0.1f);
        SetField(attack, "fireVisualDuration", 0.05f);
        SetField(attack, "vibrationAmplitude", 0f);
        EnemyController controller = attack.GetComponent<EnemyController>();
        controller.FaceDirection(1);
        Physics2D.SyncTransforms();
        Vector2 spawn = (Vector3)Invoke(attack, "ResolveProjectileSpawnPosition");
        Vector2 source = enemyBody.bounds.center;
        RaycastHit2D boundary = Physics2D.Linecast(source, spawn, TerrainMask());
        Assert.That(boundary.collider, Is.TypeOf<CompositeCollider2D>());
        Assert.That(((CompositeCollider2D)boundary.collider).geometryType, Is.EqualTo(CompositeCollider2D.GeometryType.Outlines));
        Assert.That(Physics2D.OverlapCircle(spawn, 0.729f, TerrainMask()), Is.Null,
            "The original invalid spawn lies inside an outline and passes an overlap-only test.");
        Debug.Log($"Ranged scene reproduction: enemy={attack.transform.position}, right spawn={spawn}, wall={boundary.point}");

        int fired = 0;
        bool launchedLeft = false;
        attack.ProjectileFired += () =>
        {
            fired++;
            foreach (EnemyBullet bullet in Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None))
            {
                Track(bullet.gameObject);
                if (bullet.GetComponent<Rigidbody2D>().linearVelocity.x < 0f) launchedLeft = true;
            }
        };
        int healthBefore = player.CurrentHealth;
        attack.enabled = true;
        Time.timeScale = 4f;
        yield return WaitForPhysics(25);
        Assert.That(Time.fixedTime, Is.GreaterThan(0.4f), "Play Mode physics must actually advance.");
        Assert.That(fired, Is.Zero);
        Assert.That(Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(player.CurrentHealth, Is.EqualTo(healthBefore));
        int impactCount = 0;
        foreach (EnemyBulletEffectPlayer visual in Object.FindObjectsByType<EnemyBulletEffectPlayer>(FindObjectsSortMode.None))
        {
            if (!visual.IsFinishing || visual.GetComponent<EnemyBullet>() != null) continue;
            Track(visual.gameObject);
            impactCount++;
            Assert.That(visual.transform.position.x, Is.EqualTo(boundary.point.x).Within(0.02f));
            Assert.That(visual.GetComponent<Collider2D>(), Is.Null);
            Assert.That(visual.GetComponent<Rigidbody2D>(), Is.Null);
        }
        Assert.That(impactCount, Is.GreaterThan(0), "Cancelled right shots must show an impact at the wall.");

        player.transform.position = attack.transform.position + Vector3.left * 6f;
        Physics2D.SyncTransforms();
        Invoke(attack, "EnterIdleState");
        yield return WaitForPhysics(15);
        Assert.That(fired, Is.GreaterThan(0), "Open-space left shots must still fire.");
        Assert.That(launchedLeft, Is.True);
        attack.enabled = false;
    }

    [UnityTest]
    public IEnumerator ExpiredGuidance_CrossesOutlineDuringPhysics_WithoutDamagingPlayer()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        GameObject owner = CreateObject("Owner", Vector2.zero);
        PlayerHealth player = CreatePlayer(new Vector2(4f, 0f));
        CreateWall(new Vector2(2f, 0f), new Vector2(0.1f, 8f), outline: true);
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D body);
        Physics2D.SyncTransforms();
        bullet.Initialize(player.transform, owner.transform, 100f, 200f, 10, 10f, TerrainMask(), false, 0.01f, Vector2.right);
        Invoke(bullet, "FixedUpdate");
        Assert.That(GetField<bool>(bullet, "isGuidanceActive"), Is.False);
        int healthBefore = player.CurrentHealth;
        Physics2D.Simulate(0.02f);
        Assert.That(body.position.x, Is.GreaterThan(3f), "The physics step must actually cross the thin wall.");
        Invoke(bullet, "LateUpdate");
        AssertInactive(bullet, body);
        Assert.That(player.CurrentHealth, Is.EqualTo(healthBefore));
        // A damage callback already queued for the same physics step must remain harmless.
        Invoke(bullet, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
        Assert.That(player.CurrentHealth, Is.EqualTo(healthBefore));
        yield return null;
        Assert.That(bullet == null, Is.True);
    }

    [UnityTest]
    public IEnumerator StraightBullet_CrossesThinWallWithoutTriggerContact_IsRemovedBeforeRendering()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        CreateWall(new Vector2(2f, 0f), new Vector2(0.05f, 8f), outline: true);
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D body);
        bullet.Initialize(Vector2.right, 200f);
        Physics2D.SyncTransforms();
        Physics2D.Simulate(0.02f);
        Assert.That(body.position.x, Is.GreaterThan(3f));
        Invoke(bullet, "LateUpdate");
        AssertInactive(bullet, body);
        yield return null;
    }

    [UnityTest]
    public IEnumerator NormalTerrainContact_DisablesBulletImmediately_AndIgnoresLaterPlayerHit()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        GameObject wall = CreateWall(Vector2.right, Vector2.one);
        EnemyBullet bullet = CreateBullet(new Vector2(0.45f, 0f), out Rigidbody2D body);
        PlayerHealth player = CreatePlayer(Vector2.left * 2f);
        bullet.Initialize(Vector2.right, 10f);
        Physics2D.SyncTransforms();
        int healthBefore = player.CurrentHealth;
        Invoke(bullet, "OnTriggerEnter2D", wall.GetComponent<Collider2D>());
        AssertInactive(bullet, body);
        Invoke(bullet, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
        Assert.That(player.CurrentHealth, Is.EqualTo(healthBefore));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ExpiredGuidance_TerrainImpactAnimatesAtSweptContact_AndStaysHarmless()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        GameObject owner = CreateObject("Owner", Vector2.zero);
        PlayerHealth player = CreatePlayer(Vector2.right * 8f);
        CreateWall(Vector2.right * 2f, new Vector2(0.05f, 8f), outline: true);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectiles/bullet.prefab");
        EnemyBullet bullet = Track(Object.Instantiate(prefab, Vector3.zero, Quaternion.identity)).GetComponent<EnemyBullet>();
        Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();
        EnemyBulletEffectPlayer visual = bullet.GetComponent<EnemyBulletEffectPlayer>();
        bullet.Initialize(player.transform, owner.transform, 100f, 200f, 10, 10f, TerrainMask(), false, 0.01f, Vector2.right);
        Invoke(bullet, "FixedUpdate");
        Assert.That(GetField<bool>(bullet, "isGuidanceActive"), Is.False);
        Physics2D.SyncTransforms();
        Physics2D.Simulate(0.02f);
        Assert.That(body.position.x, Is.GreaterThan(3f));
        Invoke(bullet, "LateUpdate");

        Assert.That(visual.IsFinishing, Is.True);
        Assert.That(bullet.gameObject.activeSelf, Is.True, "Only the impact animation remains active.");
        Assert.That(bullet.enabled, Is.False);
        Assert.That(body.simulated, Is.False);
        Assert.That(bullet.GetComponent<Collider2D>().enabled, Is.False);
        Assert.That(bullet.CanJustParry(), Is.False);
        Assert.That(visual.transform.position.x, Is.EqualTo(1.975f).Within(0.02f),
            "The effect belongs at the wall surface, not the tunneled bullet endpoint.");
        Assert.That(visual.GetComponent<SpriteRenderer>().sprite,
            Is.SameAs(GetField<Sprite[]>(visual, "impactFrames")[0]));
        int healthBefore = player.CurrentHealth;
        Invoke(bullet, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
        Assert.That(player.CurrentHealth, Is.EqualTo(healthBefore));
        Time.timeScale = 4f;
        yield return WaitForImpactToFinish(visual);
        Assert.That(bullet == null, Is.True);
    }

    [UnityTest]
    public IEnumerator GuidedBullet_DynamicWallCrossing_IsBlockedUsingItsCollisionWidth()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        GameObject owner = CreateObject("Owner", Vector2.zero);
        PlayerHealth player = CreatePlayer(Vector2.right * 8f);
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D body);
        bullet.GetComponent<CircleCollider2D>().radius = 0.3f;
        Physics2D.SyncTransforms();
        bullet.Initialize(player.transform, owner.transform, 100f, 200f, 10, 10f, TerrainMask(), true, 1f, Vector2.right);
        Assert.That(GetField<bool>(bullet, "isGuidanceActive"), Is.True);
        // Insert terrain after homing chose its direction. The center line misses it.
        CreateWall(new Vector2(2f, 0.3f), new Vector2(0.1f, 0.2f));
        Physics2D.SyncTransforms();
        Assert.That(Physics2D.Linecast(Vector2.zero, Vector2.right * 4f, TerrainMask()).collider, Is.Null);
        Physics2D.Simulate(0.02f);
        Invoke(bullet, "LateUpdate");
        AssertInactive(bullet, body);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReflectedBullet_SweepsBreakableWalls_AndHonorsBothConsumptionSettings()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        foreach (bool consumeBullet in new[] { true, false })
        {
            float y = consumeBullet ? 0f : 20f;
            GameObject wallObject = CreateWall(new Vector2(2f, y), Vector2.one);
            ReflectBreakableWall wall = wallObject.AddComponent<ReflectBreakableWall>();
            SetField(wall, "destroyBulletOnBreak", consumeBullet);
            EnemyBullet bullet = CreateBullet(new Vector2(0f, y), out Rigidbody2D body);
            bullet.Initialize(Vector2.left, 200f);
            bullet.ReflectByJustParry(bullet.transform.position);
            Assert.That(bullet.IsReflectedByPlayer, Is.True);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Physics2D.SyncTransforms();
            Physics2D.Simulate(0.02f);
            Invoke(bullet, "LateUpdate");
            Assert.That(GetField<bool>(wall, "isBroken"), Is.True);
            Assert.That(bullet.gameObject.activeSelf, Is.EqualTo(!consumeBullet));
            if (!consumeBullet)
            {
                // The same sweep must still stop at ordinary terrain behind the broken wall.
                CreateWall(new Vector2(3f, y), new Vector2(0.1f, 8f));
                Physics2D.SyncTransforms();
                Invoke(bullet, "LateUpdate");
                AssertInactive(bullet, body);
            }
            yield return null;
            Assert.That(wall == null, Is.True);
        }
    }

    [UnityTest]
    public IEnumerator ReflectedBullet_SolidWallPreventsBreakingWallBehindIt()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        CreateWall(Vector2.right, new Vector2(0.1f, 8f));
        ReflectBreakableWall breakable = CreateWall(Vector2.right * 3f, Vector2.one).AddComponent<ReflectBreakableWall>();
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D body);
        bullet.Initialize(Vector2.left, 200f);
        bullet.ReflectByJustParry(Vector2.zero);
        Physics2D.SyncTransforms();
        Physics2D.Simulate(0.02f);
        Invoke(bullet, "LateUpdate");
        AssertInactive(bullet, body);
        Assert.That(GetField<bool>(breakable, "isBroken"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OpenSpace_NormalBulletDamagesPlayer_AndReflectedBulletDamagesEnemy()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        PlayerHealth player = CreatePlayer(Vector2.right);
        int playerHealthBefore = player.CurrentHealth;
        EnemyBullet normalBullet = CreateBullet(Vector2.zero, out _);
        normalBullet.Initialize(Vector2.right, 10f);
        Physics2D.SyncTransforms();
        Invoke(normalBullet, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
        Assert.That(player.CurrentHealth, Is.LessThan(playerHealthBefore));
        Assert.That(normalBullet.gameObject.activeSelf, Is.False);

        player.RestoreFullHealth();
        GameObject enemyObject = CreateObject("Enemy", Vector2.left * 2f);
        Collider2D enemyCollider = enemyObject.AddComponent<BoxCollider2D>();
        EnemyController enemy = enemyObject.AddComponent<EnemyController>();
        enemy.enabled = false;
        SetField(enemy, "currentHealth", 100);
        EnemyBullet reflected = CreateBullet(Vector2.zero, out Rigidbody2D body);
        reflected.Initialize(Vector2.right, 10f);
        reflected.ReflectByJustParry(Vector2.zero);
        Physics2D.SyncTransforms();
        Invoke(reflected, "OnTriggerEnter2D", player.GetComponent<Collider2D>());
        Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
        Assert.That(reflected.gameObject.activeSelf, Is.True);
        Assert.That(body.linearVelocity.x, Is.LessThan(0f));
        Invoke(reflected, "OnTriggerEnter2D", enemyCollider);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(85));
        AssertInactive(reflected, body);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SharedShooter_CancelsShotBeyondWall()
    {
        GameObject owner = CreateObject("Shooter", Vector2.zero);
        EnemyShooter shooter = owner.AddComponent<EnemyShooter>();
        GameObject muzzle = CreateObject("Muzzle", Vector2.right * 3f);
        SetField(shooter, "firePoint", muzzle.transform);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Projectiles/bullet.prefab");
        SetField(shooter, "bulletPrefab", prefab);
        CreateWall(Vector2.right * 1.5f, new Vector2(0.1f, 8f), outline: true);
        Physics2D.SyncTransforms();
        shooter.Fire();
        Assert.That(Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None), Is.Empty);
        EnemyBulletEffectPlayer[] visuals = Object.FindObjectsByType<EnemyBulletEffectPlayer>(FindObjectsSortMode.None);
        Assert.That(visuals, Has.Length.EqualTo(1));
        EnemyBulletEffectPlayer visual = visuals[0];
        Track(visual.gameObject);
        Assert.That(visual.IsFinishing, Is.True);
        Assert.That(visual.GetComponent<Collider2D>(), Is.Null);
        Assert.That(visual.GetComponent<Rigidbody2D>(), Is.Null);
        Assert.That(visual.transform.position.x, Is.EqualTo(1.45f).Within(0.02f));
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        Assert.That(renderer.sprite.texture,
            Is.SameAs(GetField<Texture2D>(prefab.GetComponent<EnemyBulletEffectPlayer>(), "impactSpriteSheet")));
        Assert.That(renderer.sharedMaterial, Is.SameAs(prefab.GetComponent<SpriteRenderer>().sharedMaterial));
        Assert.That(visual.transform.localScale,
            Is.EqualTo(GetField<Vector3>(prefab.GetComponent<EnemyBulletEffectPlayer>(), "impactScale")));
        Sprite firstFrame = renderer.sprite;
        double deadline = Time.realtimeSinceStartupAsDouble + 2d;
        Time.timeScale = 4f;
        while (visual != null && renderer.sprite == firstFrame && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        Assert.That(visual, Is.Not.Null);
        Assert.That(renderer.sprite, Is.Not.SameAs(firstFrame), "The impact animation must advance.");
        yield return WaitForImpactToFinish(visual);
    }

    private PlayerHealth CreatePlayer(Vector2 position)
    {
        GameObject player = CreateObject("Player", position);
        player.tag = "Player";
        player.AddComponent<BoxCollider2D>().size = new Vector2(0.4f, 1f);
        PlayerHealth health = player.AddComponent<PlayerHealth>();
        health.SetCurrentHealthFromSave(health.MaxHealth);
        return health;
    }

    private EnemyBullet CreateBullet(Vector2 position, out Rigidbody2D body)
    {
        GameObject instance = CreateObject("Bullet", position);
        body = instance.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        CircleCollider2D collider = instance.AddComponent<CircleCollider2D>();
        collider.radius = 0.1f;
        collider.isTrigger = true;
        return instance.AddComponent<EnemyBullet>();
    }

    private GameObject CreateWall(Vector2 position, Vector2 size, bool outline = false)
    {
        GameObject wall = CreateObject("Wall", position);
        wall.layer = LayerMask.NameToLayer("Ground");
        BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
        box.size = size;
        if (outline)
        {
            wall.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            CompositeCollider2D composite = wall.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
            composite.GenerateGeometry();
        }
        return wall;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject instance = Track(new GameObject(name));
        instance.transform.position = position;
        return instance;
    }

    private GameObject Track(GameObject instance)
    {
        if (!objects.Contains(instance)) objects.Add(instance);
        return instance;
    }

    private static void AssertInactive(EnemyBullet bullet, Rigidbody2D body)
    {
        Assert.That(bullet.gameObject.activeSelf, Is.False, "Blocked bullets disappear synchronously.");
        Assert.That(bullet.enabled, Is.False);
        Assert.That(body.simulated, Is.False);
        Assert.That(bullet.GetComponent<Collider2D>().enabled, Is.False);
    }

    private static LayerMask TerrainMask() => LayerMask.GetMask("Ground", "FallThroughFloor");

    private static IEnumerator WaitForPhysics(int steps)
    {
        double end = Time.fixedTimeAsDouble + steps * (double)Time.fixedDeltaTime;
        while (Time.fixedTimeAsDouble < end) yield return null;
    }

    private static IEnumerator WaitForImpactToFinish(EnemyBulletEffectPlayer visual)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 5d;
        while (visual != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Assert.That(visual == null, Is.True, "The completed impact visual must clean itself up.");
    }

    private static void SetField(object instance, string name, object value)
    {
        instance.GetType().GetField(name, PrivateInstance).SetValue(instance, value);
    }

    private static T GetField<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, PrivateInstance).GetValue(instance);
    }

    private static object Invoke(object instance, string name, params object[] args)
    {
        return instance.GetType().GetMethod(name, PrivateInstance).Invoke(instance, args);
    }

    private static void ExpectKnownSceneValidationMessage(string message, string stack, LogType type)
    {
        if (type == LogType.Error && message.StartsWith("SendMessage cannot be called during Awake, CheckConsistency, or OnValidate") &&
            (message.Contains("ItemEffectVisual:") || message.Contains("ReflectBreakableWall:")))
            LogAssert.Expect(type, message);
    }
}
