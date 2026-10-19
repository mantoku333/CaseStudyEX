using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WindRiseZoneRuntimeTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (TestContext.CurrentContext.Test.MethodName == nameof(MainScene_StackedWindCarriesPlayerToUpperHover))
        {
            // Open the actual scene without saving it. Isolate wind from enemies,
            // story/save managers and terrain, leaving both prefab instances intact.
            // These existing scene validators log errors when changing renderer
            // bounds during OnValidate. Expect only those unrelated load messages.
            Application.logMessageReceived += ExpectKnownSceneValidationMessage;
            UnityEngine.SceneManagement.Scene scene;
            try { scene = EditorSceneManager.OpenScene("Assets/Scenes/FixScenes/Fix_0925_NYAA.unity"); }
            finally { Application.logMessageReceived -= ExpectKnownSceneValidationMessage; }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    if (behaviour is WindRiseZone && IsSceneWind(behaviour.gameObject))
                    {
                        Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(behaviour),
                            Is.EqualTo("Assets/Prefabs/Environment/WindRise.prefab"));
                        var overrides = new List<string>();
                        foreach (PropertyModification modification in PrefabUtility.GetPropertyModifications(behaviour))
                        {
                            overrides.Add($"{modification.propertyPath}={modification.value}");
                            Assert.That(modification.target is Transform || modification.propertyPath == "m_Name",
                                Is.True, "These instances have only transform/name overrides.");
                        }
                        Debug.Log($"{behaviour.name} prefab overrides: {string.Join(", ", overrides)}");
                    }
                    behaviour.enabled = IsSceneWind(behaviour.gameObject) && behaviour is WindRiseZone;
                }
                if (root == null) continue;
                foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
                    if (collider != null) collider.enabled = IsSceneWind(collider.gameObject);
            }
        }
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        Time.timeScale = 10f;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (GameObject instance in objects)
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }
        objects.Clear();
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator StackedZones_CarryOpenUmbrellaToUpperHover_InEitherCreationOrder()
    {
        // Match the scene prefab's vertical geometry: height 8.5594, overlap 1.5794.
        const float height = 9.142526f * 0.9362207f;
        var players = new Rigidbody2D[2];
        for (int order = 0; order < 2; order++)
        {
            float x = order * 20f;
            for (int i = 0; i < 2; i++)
            {
                int level = order == 0 ? i : 1 - i;
                CreateZone(new Vector2(x, level * 6.979994f), new Vector2(4f, height));
            }
            players[order] = CreatePlayer(new Vector2(x, -2f));
        }

        yield return WaitForPhysics(600);

        float upperHover = 6.979994f + height * 0.5f - 0.01f;
        foreach (Rigidbody2D player in players)
        {
            Assert.That(player.GetComponent<Collider2D>().bounds.center.y,
                Is.EqualTo(upperHover).Within(0.75f),
                "An open umbrella must pass the lower spring and settle at the upper hover without input.");
        }
        Assert.That(players[0].position.y, Is.EqualTo(players[1].position.y).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator ThreeTouchingZones_UseWorldBoundsDespiteMisleadingPivots()
    {
        WindRiseZone[] zones = new WindRiseZone[3];
        foreach (int level in new[] { 2, 0, 1 })
        {
            zones[level] = CreateZone(new Vector2(0f, level * 8f), new Vector2(4f, 8f));
            // A large lower hover offset requires connected-zone lookahead before
            // the player actually overlaps the next zone.
            SetField(zones[level], "hoverOffsetFromTop", level == 2 ? 0.01f : 3f);
            BoxCollider2D collider = zones[level].GetComponent<BoxCollider2D>();
            float pivotShift = (1 - level) * 100f;
            zones[level].transform.position += Vector3.up * pivotShift;
            zones[level].transform.localScale = new Vector3(2f, 0.5f, 1f);
            collider.size = new Vector2(2f, 16f);
            collider.offset = new Vector2(0f, -pivotShift / 0.5f);
        }
        Rigidbody2D player = CreatePlayer(Vector2.zero);

        yield return WaitForPhysics(700);

        AssertHoveringAt(player, zones[2]);
    }

    [UnityTest]
    public IEnumerator VelocityResolution_IsOncePerStep_RegardlessOfCallbackOrder()
    {
        WindRiseZone lower = CreateZone(Vector2.zero, new Vector2(6f, 12f));
        WindRiseZone upper = CreateZone(new Vector2(0f, 3f), new Vector2(6f, 12f));
        Rigidbody2D player = CreatePlayer(Vector2.zero);
        player.gravityScale = 0f;
        player.linearVelocity = new Vector2(0.2f, 0f);
        yield return WaitForPhysics(5);

        Vector2 resolvedVelocity = player.linearVelocity;
        // Both callbacks have already run this physics step. Extra/reordered calls
        // must never accumulate another acceleration or lower-zone spring.
        Invoke(upper, "FixedUpdate");
        Invoke(lower, "FixedUpdate");
        Invoke(lower, "FixedUpdate");
        Invoke(upper, "FixedUpdate");
        Assert.That(player.linearVelocity, Is.EqualTo(resolvedVelocity));
        Assert.That(player.linearVelocity.x, Is.EqualTo(0.2f).Within(0.0001f));
    }

    [UnityTest]
    public IEnumerator SingleZone_HoversAndHonorsUmbrellaWithoutChangingHorizontalVelocity()
    {
        WindRiseZone zone = CreateZone(Vector2.zero, new Vector2(12f, 12f));
        Rigidbody2D player = CreatePlayer(Vector2.zero);
        player.linearVelocity = new Vector2(0.2f, 0f);
        yield return WaitForPhysics(500);
        AssertHoveringAt(player, zone);
        Assert.That(player.linearVelocity.x, Is.EqualTo(0.2f).Within(0.0001f));

        player.gravityScale = 0f;
        player.GetComponent<UmbrellaController>().CloseUmbrella();
        player.linearVelocity = Vector2.zero;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero));

        player.GetComponent<UmbrellaController>().OpenUmbrella();
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator ExitDisableDestroyAndReentry_ReleaseAndReacquireWind()
    {
        WindRiseZone lower = CreateZone(Vector2.zero, new Vector2(6f, 8f));
        WindRiseZone upper = CreateZone(new Vector2(0f, 7f), new Vector2(6f, 8f));
        Rigidbody2D player = CreatePlayer(Vector2.zero);
        player.gravityScale = 0f;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));

        // Leave all wind while keeping both zones alive.
        player.position += Vector2.right * 30f;
        player.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero));
        player.position -= Vector2.right * 30f;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));

        // Place the body entirely inside the upper zone, outside the lower one.
        MovePlayerCenter(player, new Vector2(0f, 8f));
        upper.enabled = false;
        player.linearVelocity = Vector2.zero;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero));
        upper.enabled = true;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));

        upper.GetComponent<Collider2D>().enabled = false;
        player.linearVelocity = Vector2.zero;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero));
        upper.GetComponent<Collider2D>().enabled = true;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));

        Object.DestroyImmediate(upper.gameObject);
        player.linearVelocity = Vector2.zero;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero));

        MovePlayerCenter(player, Vector2.zero);
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));
        lower.gameObject.SetActive(false);
        player.linearVelocity = Vector2.zero;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero));
        lower.gameObject.SetActive(true);
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator DisconnectedOrSidewaysZones_DoNotAttractPlayer()
    {
        WindRiseZone lower = CreateZone(Vector2.zero, new Vector2(4f, 8f));
        CreateZone(new Vector2(0f, 30f), new Vector2(4f, 8f));
        CreateZone(new Vector2(8f, 8f), new Vector2(12f, 8f));
        // Its AABB overlaps the lower zone, but the triangle itself is separated.
        GameObject triangleObject = Track(new GameObject("Disconnected polygon wind"));
        PolygonCollider2D triangle = triangleObject.AddComponent<PolygonCollider2D>();
        triangle.points = new[] { new Vector2(-2f, 14f), new Vector2(14f, 14f), new Vector2(14f, -2f) };
        triangle.isTrigger = true;
        triangleObject.AddComponent<WindRiseZone>();
        Rigidbody2D player = CreatePlayer(Vector2.zero);

        yield return WaitForPhysics(500);

        AssertHoveringAt(player, lower);
    }

    [UnityTest]
    public IEnumerator UpperZone_OnlyTakesOverWhenItsUmbrellaRequirementIsMet()
    {
        WindRiseZone lower = CreateZone(Vector2.zero, new Vector2(6f, 8f));
        WindRiseZone upper = CreateZone(new Vector2(0f, 7f), new Vector2(6f, 8f));
        SetField(lower, "requireUmbrellaOpen", false);
        Rigidbody2D player = CreatePlayer(Vector2.zero);
        player.GetComponent<UmbrellaController>().CloseUmbrella();
        yield return WaitForPhysics(500);
        AssertHoveringAt(player, lower);

        player.GetComponent<UmbrellaController>().OpenUmbrella();
        yield return WaitForPhysics(500);
        AssertHoveringAt(player, upper);
    }

    [UnityTest]
    public IEnumerator AirborneRequirement_IsRespectedByEveryZoneInAStack()
    {
        WindRiseZone lower = CreateZone(Vector2.zero, new Vector2(6f, 12f));
        WindRiseZone upper = CreateZone(new Vector2(0f, 8f), new Vector2(6f, 12f));
        SetField(lower, "requireAirborne", true);
        SetField(upper, "requireAirborne", true);
        lower.enabled = false;
        upper.enabled = false;
        Rigidbody2D player = CreatePlayer(Vector2.zero);
        player.gravityScale = 0f;
        player.gameObject.layer = 2;
        Collider2D bodyCollider = player.GetComponent<Collider2D>();
        bodyCollider.isTrigger = true; // Keep this gate test free of floor depenetration.
        GameObject groundObject = Track(new GameObject("Ground check"));
        groundObject.transform.SetParent(player.transform);
        groundObject.transform.position = new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y);
        groundObject.layer = 2;
        groundObject.AddComponent<BoxCollider2D>().isTrigger = true;
        GroundCheck groundCheck = groundObject.AddComponent<GroundCheck>();
        SetField(groundCheck, "groundLayer", (LayerMask)1);
        GameObject floor = Track(new GameObject("Ground"));
        floor.transform.position = groundObject.transform.position;
        floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 0.1f);
        Physics2D.SyncTransforms();
        yield return WaitForPhysics(2);
        Assert.That(groundCheck.IsGround(), Is.True);
        player.linearVelocity = Vector2.zero;
        lower.enabled = true;
        upper.enabled = true;

        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero));
        SetField(lower, "requireAirborne", false);
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator RecoilSuppression_SurvivesUpperHandoff_AndDodgeStillBlocksWind()
    {
        CreateZone(Vector2.zero, new Vector2(6f, 12f));
        WindRiseZone upper = CreateZone(new Vector2(0f, 8f), new Vector2(6f, 12f));
        upper.enabled = false;
        Rigidbody2D player = CreatePlayer(Vector2.zero);
        player.gravityScale = 0f;
        GunController gun = player.gameObject.AddComponent<GunController>();
        gun.enabled = false; // Set the real state without firing or spawning gun effects.
        DodgeController dodge = player.gameObject.AddComponent<DodgeController>();
        SetField(gun, "isRecoiling", true);
        player.linearVelocity = Vector2.up;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.EqualTo(1f).Within(0.0001f));

        SetField(gun, "isRecoiling", false);
        upper.enabled = true;
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.EqualTo(1f).Within(0.0001f),
            "A new controlling zone must retain the player's active recoil-rise suppression.");
        yield return WaitForPhysics(40);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(1f));

        SetField(dodge, "isDodging", true);
        player.linearVelocity = new Vector2(0.2f, 1f);
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(new Vector2(0.2f, 1f)));
        SetField(dodge, "isDodging", false);
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(1f));
    }

    [UnityTest]
    public IEnumerator ChildTrigger_CannotAcquireWindOrRemoveAnOverlappingBody()
    {
        CreateZone(Vector2.zero, new Vector2(6f, 12f));
        Rigidbody2D player = CreatePlayer(new Vector2(15f, 0f));
        player.gravityScale = 0f;
        GameObject child = Track(new GameObject("Parry/GroundCheck trigger"));
        child.transform.SetParent(player.transform);
        child.transform.position = Vector2.zero;
        child.AddComponent<BoxCollider2D>().isTrigger = true;
        Physics2D.SyncTransforms();
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity, Is.EqualTo(Vector2.zero), "Only the child overlaps the wind.");

        // The body enters as the ignored child exits, in either trigger order.
        MovePlayerCenter(player, Vector2.zero);
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f));
        child.transform.position = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return WaitForPhysics(2);
        child.transform.position = Vector2.right * 30f;
        Physics2D.SyncTransforms();
        yield return WaitForPhysics(5);
        Assert.That(player.linearVelocity.y, Is.GreaterThan(0f), "A child exit must not release the player's wind.");
    }

    [UnityTest]
    public IEnumerator MainScene_StackedWindCarriesPlayerToUpperHover()
    {
        WindRiseZone lower = GameObject.Find("WindRise (7)").GetComponent<WindRiseZone>();
        WindRiseZone upper = GameObject.Find("WindRise (4)").GetComponent<WindRiseZone>();
        Bounds lowerBounds = lower.GetComponent<Collider2D>().bounds;
        Bounds upperBounds = upper.GetComponent<Collider2D>().bounds;
        Debug.Log($"Scene wind bounds: lower={lowerBounds}, upper={upperBounds}");
        Rigidbody2D player = CreatePlayer(lowerBounds.center);

        yield return WaitForPhysics(600);

        AssertHoveringAt(player, upper);
        Assert.That(player.GetComponent<UmbrellaController>().GetUmbrellaState(),
            Is.EqualTo(UmbrellaController.UmbrellaState.Open));
        Debug.Log($"Scene wind final player center: {player.GetComponent<Collider2D>().bounds.center}");
    }

    private static bool IsSceneWind(GameObject instance)
    {
        return instance.name == "WindRise (7)" || instance.name == "WindRise (4)";
    }

    private static void ExpectKnownSceneValidationMessage(string message, string stack, LogType type)
    {
        if (type == LogType.Error && message.StartsWith("SendMessage cannot be called during Awake, CheckConsistency, or OnValidate") &&
            (message.Contains("ItemEffectVisual:") || message.Contains("ReflectBreakableWall:")))
            LogAssert.Expect(type, message);
    }

    private static void AssertHoveringAt(Rigidbody2D player, WindRiseZone zone)
    {
        float expected = zone.GetComponent<Collider2D>().bounds.max.y - 0.01f;
        Assert.That(player.GetComponent<Collider2D>().bounds.center.y, Is.EqualTo(expected).Within(0.75f));
    }

    private static void MovePlayerCenter(Rigidbody2D player, Vector2 center)
    {
        player.position += center - (Vector2)player.GetComponent<Collider2D>().bounds.center;
        player.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private static void Invoke(object instance, string name)
    {
        MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        method.Invoke(instance, null);
    }

    private WindRiseZone CreateZone(Vector2 center, Vector2 size)
    {
        GameObject instance = Track(new GameObject("Wind test zone"));
        instance.transform.position = center;
        BoxCollider2D collider = instance.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        WindRiseZone zone = instance.AddComponent<WindRiseZone>();
        SetField(zone, "requireAirborne", false);
        SetField(zone, "hoverOffsetFromTop", 0.01f);
        SetField(zone, "bobAmplitude", 0f);
        return zone;
    }

    private Rigidbody2D CreatePlayer(Vector2 position)
    {
        GameObject instance = Track(new GameObject("Wind test player"));
        instance.tag = "Player";
        instance.transform.position = position;
        Rigidbody2D body = instance.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        // The real body's large vertical extent keeps it inside the lower trigger
        // well above that trigger's hover point, exposing the competing spring.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(prefab, Is.Not.Null);
        body.gravityScale = prefab.GetComponent<Rigidbody2D>().gravityScale;
        instance.transform.localScale = prefab.transform.localScale;
        PolygonCollider2D polygon = instance.AddComponent<PolygonCollider2D>();
        polygon.points = prefab.GetComponent<PolygonCollider2D>().points;
        Physics2D.SyncTransforms();
        instance.transform.position += (Vector3)(position - (Vector2)polygon.bounds.center);
        Physics2D.SyncTransforms();
        UmbrellaController umbrella = instance.AddComponent<UmbrellaController>();
        umbrella.SetUmbrellaState(UmbrellaController.UmbrellaState.Open, false);
        return body;
    }

    private GameObject Track(GameObject instance)
    {
        objects.Add(instance);
        return instance;
    }

    private static IEnumerator WaitForPhysics(int steps)
    {
        // These Editor tests enter Play Mode; wait for actual physics time rather
        // than counting Editor test-runner updates (which can outpace FixedUpdate).
        double end = Time.fixedTimeAsDouble + steps * (double)Time.fixedDeltaTime;
        while (Time.fixedTimeAsDouble < end) yield return null;
    }

    private static void SetField(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(instance, value);
    }
}
