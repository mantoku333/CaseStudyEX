using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ElegantPointIntegrationTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [Test]
    public void PlayerAndHudPrefabs_AreWiredForElegantPoints()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/Player.prefab");
        GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/PlayerHUDCanvas.prefab");

        Assert.That(playerPrefab, Is.Not.Null);
        PlayerElegantPointController elegantController =
            playerPrefab.GetComponent<PlayerElegantPointController>();
        Assert.That(elegantController, Is.Not.Null);
        var serializedElegantController = new SerializedObject(elegantController);
        Assert.That(
            serializedElegantController.FindProperty("glideController").objectReferenceValue,
            Is.TypeOf<UmbrellaController>());
        Assert.That(
            serializedElegantController.FindProperty("recoilController").objectReferenceValue,
            Is.TypeOf<GunController>());
        Assert.That(hudPrefab, Is.Not.Null);

        ElegantPointHudView hudView = hudPrefab.GetComponent<ElegantPointHudView>();
        Assert.That(hudView, Is.Not.Null);
        var serializedHud = new SerializedObject(hudView);
        Transform gatedGroup = serializedHud.FindProperty("hudGroup").objectReferenceValue as Transform;
        Image serializedFill = serializedHud.FindProperty("gaugeFill").objectReferenceValue as Image;
        TMP_Text serializedBalance = serializedHud.FindProperty("balanceText").objectReferenceValue as TMP_Text;
        Assert.That(gatedGroup, Is.Not.Null);
        Assert.That(gatedGroup.name, Is.EqualTo("Player HP"));

        Transform gauge = gatedGroup.Find("Elegant Point Gauge");
        Assert.That(gauge, Is.Not.Null, "The gauge must be a permanent prefab child, not runtime-only UI.");
        Assert.That(gauge.parent, Is.SameAs(gatedGroup));

        Transform fill = gauge.Find("Fill");
        Transform balance = gauge.Find("Balance");
        Assert.That(fill, Is.Not.Null);
        Assert.That(balance, Is.Not.Null);
        Assert.That(serializedFill, Is.SameAs(fill.GetComponent<Image>()));
        Assert.That(serializedBalance, Is.SameAs(balance.GetComponent<TMP_Text>()));
    }

    [Test]
    public void HudFillRect_RepresentsHalfBalanceWithoutRequiringASprite()
    {
        var hudObject = new GameObject("Test HUD", typeof(RectTransform));
        hudObject.SetActive(false);
        var groupObject = new GameObject("Player HP", typeof(RectTransform));
        groupObject.transform.SetParent(hudObject.transform, false);
        ElegantPointHudView view = hudObject.AddComponent<ElegantPointHudView>();

        try
        {
            InvokePrivate(view, "EnsureView");
            InvokePrivate(view, "Refresh", 250);

            Image fill = GetPrivateField<Image>(view, "gaugeFill");
            TMP_Text text = GetPrivateField<TMP_Text>(view, "balanceText");
            Assert.That(fill.sprite, Is.Null);
            Assert.That(fill.rectTransform.anchorMin.x, Is.EqualTo(0.015f).Within(0.0001f));
            Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(text.text, Is.EqualTo("250 / 500"));
        }
        finally
        {
            Object.DestroyImmediate(hudObject);
        }
    }

    [Test]
    public void PlayerPrefab_UmbrellaAwakePreservesSiblingGunReference()
    {
        const string prefabPath = "Assets/Prefabs/Player/Player.prefab";
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            UmbrellaController umbrella =
                prefabContents.GetComponentInChildren<UmbrellaController>(true);
            GunController siblingGun =
                prefabContents.GetComponentInChildren<GunController>(true);

            Assert.That(umbrella, Is.Not.Null);
            Assert.That(siblingGun, Is.Not.Null);
            Assert.That(GetPrivateField<GunController>(umbrella, "gunController"), Is.SameAs(siblingGun));

            InvokePrivate(umbrella, "Awake");

            Assert.That(
                GetPrivateField<GunController>(umbrella, "gunController"),
                Is.SameAs(siblingGun),
                "Awake must not replace the serialized sibling-Gun reference with null.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
    }

    [Test]
    public void AttackControllers_ExposeRequiredLifecycleEvents()
    {
        AssertAttackEvents(typeof(UmbrellaAttackController));
        AssertAttackEvents(typeof(PlayerDiveAttackController));
        Assert.That(typeof(UmbrellaController).GetEvent("GlideStarted"), Is.Not.Null);
        Assert.That(typeof(UmbrellaController).GetProperty("IsGlideActionActive"), Is.Not.Null);
        Assert.That(typeof(GunController).GetEvent("AirborneRecoilStarted"), Is.Not.Null);
        Assert.That(typeof(GunController).GetProperty("IsAirborneRecoilActive"), Is.Not.Null);
    }

    [Test]
    public void GlideSession_RecoilInterruptionDoesNotCreateAnotherStart()
    {
        var playerObject = new GameObject("Glide Action Test");
        Rigidbody2D rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        Player.PlayerAbilityController abilities =
            playerObject.AddComponent<Player.PlayerAbilityController>();
        GunController gun = playerObject.AddComponent<GunController>();
        UmbrellaController umbrella = playerObject.AddComponent<UmbrellaController>();
        var state = new TestPlayerStateProvider { IsGrounded = false };

        SetPrivateField(abilities, "canGlide", true);
        SetPrivateField(umbrella, "rigidBody2D", rigidbody2D);
        SetPrivateField(umbrella, "playerAbilityController", abilities);
        SetPrivateField(umbrella, "playerStateProvider", state);
        SetPrivateField(umbrella, "gunController", gun);
        umbrella.SetUmbrellaState(UmbrellaController.UmbrellaState.Open, false);

        int startCount = 0;
        umbrella.GlideStarted += () => startCount++;

        try
        {
            rigidbody2D.linearVelocity = new Vector2(0f, 2f);
            InvokePrivate(umbrella, "Glide");
            Assert.That(startCount, Is.Zero, "Opening while rising is not an actual glide start.");

            rigidbody2D.linearVelocity = new Vector2(0f, -2f);
            InvokePrivate(umbrella, "Glide");
            Assert.That(startCount, Is.EqualTo(1));
            Assert.That(umbrella.IsGlideActionActive, Is.True);

            SetPrivateField(gun, "isRecoiling", true);
            InvokePrivate(umbrella, "Glide");
            SetPrivateField(gun, "isRecoiling", false);
            InvokePrivate(umbrella, "Glide");

            Assert.That(startCount, Is.EqualTo(1), "Resuming the same held glide must not add a step.");
            Assert.That(umbrella.IsGlideActionActive, Is.True);

            state.IsGrounded = true;
            InvokePrivate(umbrella, "Glide");
            Assert.That(umbrella.IsGlideActionActive, Is.False);

            state.IsGrounded = false;
            InvokePrivate(umbrella, "Glide");
            Assert.That(startCount, Is.EqualTo(2), "A new airborne session may start a new glide.");
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
        }
    }

    [TestCase("Assets/Prefabs/Enemies/Enemy.prefab")]
    [TestCase("Assets/Prefabs/Enemies/StageBoss.prefab")]
    [TestCase("Assets/Prefabs/Enemies/LastBoss.prefab")]
    public void NormalAttackEnemyClassifier_CountsEnemyAndBossPrefabs(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null);
        Collider2D targetCollider = prefab.GetComponentInChildren<Collider2D>(true);
        Assert.That(targetCollider, Is.Not.Null);

        Assert.That(InvokeNormalAttackEnemyClassifier(targetCollider), Is.True);
    }

    [Test]
    public void NormalAttackEnemyClassifier_ExcludesDestructibles()
    {
        var target = new GameObject("Destructible");
        target.SetActive(false);
        target.AddComponent<AttackDestructible>();
        Collider2D targetCollider = target.AddComponent<BoxCollider2D>();

        try
        {
            Assert.That(InvokeNormalAttackEnemyClassifier(targetCollider), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void SettleForDeathOrTeardown_PaysOnlyOnceAndResets()
    {
        var chain = new ElegantActionChain();
        chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        chain.RegisterAttackHit(ElegantActionType.NormalAttack);
        chain.RegisterAttackEnd(ElegantActionType.NormalAttack, true);
        chain.RegisterActionStart(ElegantActionType.Glide);

        Assert.That(chain.Settle(), Is.EqualTo(10));
        Assert.That(chain.Settle(), Is.Zero);
        Assert.That(chain.IsArmed, Is.False);
    }

    [Test]
    public void DisablingNormalAttackDuringScaledPause_PreservesPendingResult()
    {
        var attackObject = new GameObject("Attack Controller Test");
        UmbrellaAttackController controller = attackObject.AddComponent<UmbrellaAttackController>();
        FieldInfo attackingField = typeof(UmbrellaAttackController).GetField("isAttacking", InstancePrivate);
        Assert.That(attackingField, Is.Not.Null);
        attackingField.SetValue(controller, true);

        int completionCount = 0;
        controller.ActionEnded += _ => completionCount++;
        float originalTimeScale = Time.timeScale;

        try
        {
            Time.timeScale = 0f;
            InvokePrivate(controller, "OnDisable");

            Assert.That(completionCount, Is.Zero);
            Assert.That(controller.IsAttacking(), Is.True);
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            Object.DestroyImmediate(attackObject);
        }
    }

    private static void AssertAttackEvents(Type controllerType)
    {
        Assert.That(controllerType.GetEvent("ActionStarted"), Is.Not.Null);
        Assert.That(controllerType.GetEvent("FirstEnemyHit"), Is.Not.Null);
        Assert.That(controllerType.GetEvent("ActionEnded"), Is.Not.Null);
    }

    private static bool InvokeNormalAttackEnemyClassifier(Collider2D collider)
    {
        MethodInfo method = typeof(UmbrellaAttackController).GetMethod("IsEnemyHit", StaticPrivate);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(null, new object[] { collider });
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null);
        return field.GetValue(target) as T;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, arguments);
    }

    private sealed class TestPlayerStateProvider : Player.IPlayerViewStateProvider
    {
        public bool IsGrounded { get; set; }
        public bool IsMoving => false;
        public bool IsGliding => false;
        public bool IsUmbrellaOpen => true;
        public bool IsFacingRight => true;
        public bool IsDodging => false;
        public bool IsParrying => false;
        public bool IsUmbrellaChanging => false;
        public bool IsAttacking => false;
        public bool IsDiveAttacking => false;
        public bool IsDiveAttackLanding => false;
        public bool IsDiveAttackBouncing => false;
        public bool IsRecoilBoosting => false;
    }
}
