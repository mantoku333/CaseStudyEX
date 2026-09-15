using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Metroidvania.Data;
using NUnit.Framework;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Fix0906RuntimeRegressionTests
{
    private List<Object> createdObjects;
    private string saveDirectory;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        createdObjects = new List<Object>();
        saveDirectory = Path.Combine(Application.temporaryCachePath, "Fix0906Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveDirectory);
        SetSaveDirectory(saveDirectory);
        SaveManager.ResetResumeCheckpoint();
        GameProgressFlags.ClearAll();
        GameItems.ClearAll();
        PvModeState.SetActive(false);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        InvokeStatic(typeof(SaveManager), "ClearPendingLoad");
        var manager = Object.FindFirstObjectByType<SaveManager>();
        if (manager != null)
        {
            manager.StopAllCoroutines();
        }
        if (createdObjects != null)
        {
            foreach (var obj in createdObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
        }
        SaveManager.ResetResumeCheckpoint();
        GameProgressFlags.ClearAll();
        SetSaveDirectory(null);
        if (!string.IsNullOrEmpty(saveDirectory) && Directory.Exists(saveDirectory))
        {
            Directory.Delete(saveDirectory, true);
        }
        Time.timeScale = 1f;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HpPickups_SaveDifferentSubsetsWithoutLosingOtherPickupsOrGrantingHealthAgain()
    {
        foreach (int mask in new[] { 1, 2, 5, 10, 15 })
        {
            GameProgressFlags.ClearAll();
            var health = CreatePlayer(false);
            int initialMax = health.MaxHealth;
            var data = Track(ScriptableObject.CreateInstance<ItemData>());
            data.itemId = "shared_hp_upgrade";
            data.maxHealthBonus = 2;
            var pickups = new ItemPickup[4];
            int collected = 0;
            for (int i = 0; i < pickups.Length; i++)
            {
                pickups[i] = CreatePickup(data, "test_hp_" + i);
                if ((mask & (1 << i)) == 0) continue;
                Invoke(pickups[i], "OnTriggerEnter2D", health.GetComponent<Collider2D>());
                collected++;
            }
            var save = Capture();
            Assert.That(health.MaxHealth, Is.EqualTo(initialMax + collected * 2));
            foreach (var pickup in pickups)
                if (pickup != null) Object.DestroyImmediate(pickup.gameObject);
            Object.DestroyImmediate(health.gameObject);

            GameProgressFlags.ClearAll();
            GameProgressFlags.RestoreFromSaveData(save);
            health = CreatePlayer(false);
            health.Restore(save);
            for (int i = 0; i < pickups.Length; i++)
            {
                pickups[i] = CreatePickup(data, "test_hp_" + i);
                pickups[i].Restore(save);
                if ((mask & (1 << i)) != 0)
                    Invoke(pickups[i], "OnTriggerEnter2D", health.GetComponent<Collider2D>());
            }
            yield return null;
            for (int i = 0; i < pickups.Length; i++)
            {
                Assert.That(pickups[i] != null && pickups[i].gameObject.activeSelf,
                    Is.EqualTo((mask & (1 << i)) == 0), $"mask={mask}, pickup={i}");
                if (pickups[i] != null) Object.DestroyImmediate(pickups[i].gameObject);
            }
            Assert.That(health.MaxHealth, Is.EqualTo(initialMax + collected * 2));
            Object.DestroyImmediate(health.gameObject);
        }

        // Existing pickups with no instance override still use their ItemData key.
        GameProgressFlags.ClearAll();
        var fallbackData = Track(ScriptableObject.CreateInstance<ItemData>());
        fallbackData.itemId = "legacy_item_key";
        fallbackData.maxHealthBonus = 2;
        var fallbackPlayer = CreatePlayer(false);
        var fallbackPickup = CreatePickup(fallbackData, "");
        Invoke(fallbackPickup, "OnTriggerEnter2D", fallbackPlayer.GetComponent<Collider2D>());
        Assert.That(GameProgressFlags.Get(fallbackData.itemId), Is.True);
    }

    [UnityTest]
    public IEnumerator Shutters_RestoreAcceptedTargetsAndResetLeversWhenLoadingEarlierSave()
    {
        var shutters = new ShutterWallBlockRise[7];
        var levers = new LeverSwitch2D[7];
        var closedSprite = CreateSprite("Closed");
        var openedSprite = CreateSprite("Opened");
        for (int i = 0; i < shutters.Length; i++)
        {
            shutters[i] = CreateShutter("test_door_" + i);
            levers[i] = CreateLever(shutters[i], closedSprite, openedSprite);
        }
        var closedSave = Capture();
        for (int i = 0; i < shutters.Length; i += 2)
        {
            levers[i].ActivateFromAttack();
            Assert.That(shutters[i].IsTransitioning, Is.True);
            Assert.That(GameProgressFlags.Get("test_door_" + i), Is.True);
        }
        var movingSave = Capture();
        InvokeStatic(typeof(SaveManager), "RestoreModules", movingSave);
        for (int i = 0; i < shutters.Length; i++)
        {
            bool opened = i % 2 == 0;
            Assert.That(shutters[i].IsOpen, Is.EqualTo(opened));
            Assert.That(shutters[i].IsTransitioning, Is.False);
            Assert.That(levers[i].GetComponent<SpriteRenderer>().sprite, Is.SameAs(opened ? openedSprite : closedSprite));
            Assert.That(Get<bool>(levers[i], "consumed"), Is.EqualTo(opened));
            AssertBlocks(shutters[i], opened);
        }
        yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < shutters.Length; i++) AssertBlocks(shutters[i], i % 2 == 0);

        InvokeStatic(typeof(SaveManager), "RestoreModules", closedSave);
        for (int i = 0; i < shutters.Length; i++)
        {
            AssertBlocks(shutters[i], false);
            Assert.That(Get<bool>(levers[i], "consumed"), Is.False);
            Assert.That(levers[i].GetComponent<SpriteRenderer>().sprite, Is.SameAs(closedSprite));
            levers[i].ActivateFromAttack();
        }
        yield return new WaitForSeconds(0.6f);
        var completedSave = Capture();
        InvokeStatic(typeof(SaveManager), "RestoreModules", completedSave);
        foreach (var shutter in shutters) AssertBlocks(shutter, true);

        // Recreated scene objects also initialize from the saved flag before Start.
        GameProgressFlags.Set("recreated_door", true);
        var recreated = CreateShutter("recreated_door");
        AssertBlocks(recreated, true);
        var recreatedLever = CreateLever(recreated, closedSprite, openedSprite);
        Assert.That(Get<bool>(recreatedLever, "consumed"), Is.True);

        var unsaved = CreateShutter("");
        unsaved.TryOpen();
        unsaved.Restore(closedSave);
        Assert.That(unsaved.IsTransitioning, Is.True, "An unkeyed shutter must ignore save restoration.");
    }

    [UnityTest]
    public IEnumerator FallingDuringDodge_KeepsRespawnPositionThroughDodgeCompletion()
    {
        var health = CreatePlayer(false);
        health.AddMaxHealth(10, true);
        var body = health.GetComponent<Rigidbody2D>();
        var dodge = health.GetComponent<DodgeController>();
        var destination = Track(new GameObject("RespawnDestination")).transform;
        destination.position = new Vector3(10, -2, 0);
        var fall = Track(new GameObject("FallTrigger")).AddComponent<RespawnOnFall>();
        Set(fall, "respawnPoint", destination);

        int beforeFall = health.CurrentHealth;
        body.linearVelocity = new Vector2(2, -4);
        Invoke(fall, "OnTriggerEnter2D", health.GetComponent<Collider2D>());
        Assert.That(health.CurrentHealth, Is.EqualTo(beforeFall - 1));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));

        body.position = Vector2.zero;
        health.transform.position = Vector3.zero;
        dodge.SetDodgeDistance(3);
        dodge.SetDodgeDuration(0.2f);
        dodge.SetDodgeCooldown(0.5f);
        dodge.Dodge(Vector2.right);
        yield return new WaitForFixedUpdate();
        Invoke(fall, "OnTriggerEnter2D", health.GetComponent<Collider2D>());
        Physics2D.SyncTransforms();
        Assert.That(dodge.IsDodging(), Is.True);
        float deadline = Time.realtimeSinceStartup + 2;
        while (dodge.IsDodging() && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(body.position, destination.position), Is.LessThan(0.01f));
        }
        Assert.That(dodge.IsDodging(), Is.False);
        Assert.That(dodge.CanDodge(), Is.False, "Respawning must retain the dodge cooldown.");

        var child = Track(new GameObject("AttackCollider"));
        child.transform.SetParent(health.transform, false);
        var attackCollider = child.AddComponent<BoxCollider2D>();
        attackCollider.isTrigger = true;
        destination.position = new Vector3(20, 2, 0);
        Invoke(fall, "OnTriggerEnter2D", attackCollider);
        Assert.That(Vector2.Distance(body.position, destination.position), Is.GreaterThan(1));
    }

    [UnityTest]
    public IEnumerator Retry_UsesSessionCheckpointAcrossLoadsSavesFailuresDeletionAndReset()
    {
        // Use a synthetic active scene whose name is already build-enabled. This exercises
        // both public loading APIs without loading the project's real Title scene or saves.
        SceneManager.SetActiveScene(SceneManager.CreateScene("Title"));
        var health = CreatePlayer(true);
        var saved = Capture();
        saved.sceneName = "Title";
        saved.playerPosition = SerializableVector3.FromVector3(new Vector3(8, 5, 0));
        saved.savedAtUtc = "2026-01-01T00:00:00Z";
        Assert.That(SaveRepository.TryWrite(2, saved), Is.True);
        saved.savedAtUtc = "2026-08-01T00:00:00Z";
        Assert.That(SaveRepository.TryWrite(9, saved), Is.True);
        Assert.That(SaveManager.TryGetResumeSaveSlot(out _, out _), Is.False);
        Assert.That(SaveManager.TryGetLatestSaveSlot(out int newest, out _), Is.True);
        Assert.That(newest, Is.EqualTo(9));

        Assert.That(SaveManager.TryLoadGame(2), Is.True);
        Assert.That(SaveManager.TryGetResumeSaveSlot(out _, out _), Is.False, "A pending load is not yet a checkpoint.");
        yield return WaitForLoad();
        AssertResumeSlot(2);
        Assert.That(Vector3.Distance(health.transform.position, new Vector3(8, 5, 0)), Is.LessThan(0.01f));

        Assert.That(SaveManager.TrySaveCurrentGame(), Is.True);
        AssertResumeSlot(1);
        string blockedTempPath = SaveRepository.GetSaveFilePath(5) + ".tmp";
        Directory.CreateDirectory(blockedTempPath);
        LogAssert.Expect(LogType.Error, new Regex("SaveRepository.*Failed to write save"));
        Assert.That(SaveManager.TrySaveCurrentGame(5), Is.False);
        Directory.Delete(blockedTempPath);
        AssertResumeSlot(1);
        Assert.That(SaveManager.TryLoadGame(10), Is.False);
        AssertResumeSlot(1);

        saved.sceneName = "MissingScene_Fix0906Test";
        Assert.That(SaveRepository.TryWrite(4, saved), Is.True);
        LogAssert.Expect(LogType.Error, new Regex("SaveManager.*No loadable scene found"));
        Assert.That(SaveManager.TryLoadGame(4), Is.False);
        AssertResumeSlot(1);

        yield return SaveManager.LoadGameAsync(2);
        AssertResumeSlot(2);
        yield return SaveManager.LoadGameAsync(10);
        AssertResumeSlot(2);
        LogAssert.Expect(LogType.Error, new Regex("SaveManager.*No loadable scene found"));
        yield return SaveManager.LoadGameAsync(4);
        AssertResumeSlot(2);
        Assert.That(SaveManager.DeleteSave(9), Is.True);
        AssertResumeSlot(2);

        using (File.Open(SaveRepository.GetSaveFilePath(2), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            LogAssert.Expect(LogType.Error, new Regex("SaveRepository.*Failed to delete save"));
            Assert.That(SaveManager.DeleteSave(2), Is.False);
        }
        AssertResumeSlot(2);
        Assert.That(SaveManager.DeleteSave(2), Is.True);
        Assert.That(SaveManager.TryGetResumeSaveSlot(out _, out _), Is.False);
        Assert.That(SaveRepository.HasSave(1), Is.True, "Other saves must never become an automatic fallback.");

        yield return SaveManager.LoadGameAsync(1);
        AssertResumeSlot(1);
        File.WriteAllText(SaveRepository.GetSaveFilePath(1), "invalid JSON");
        LogAssert.Expect(LogType.Error, new Regex("SaveRepository.*Failed to read save"));
        Assert.That(SaveManager.TryGetResumeSaveSlot(out _, out _), Is.False);
        Assert.That(SaveManager.TrySaveCurrentGame(), Is.True);
        AssertResumeSlot(1);
        SaveManager.ResetResumeCheckpoint();
        Assert.That(SaveManager.TryGetResumeSaveSlot(out _, out _), Is.False);
        Assert.That(SaveRepository.HasSave(1), Is.True);
    }

    [UnityTest]
    public IEnumerator Retry_RefreshesVisibleButtonWhenDeathHappensDuringLoad()
    {
        SceneManager.SetActiveScene(SceneManager.CreateScene("Title"));
        var health = CreatePlayer(true);
        Set(health, "currentHealth", 0);
        var save = Capture();
        save.sceneName = "Title";
        Assert.That(SaveRepository.TryWrite(2, save), Is.True);

        foreach (bool asynchronous in new[] { false, true })
        {
            health.RestoreFullHealth();
            SaveManager.ResetResumeCheckpoint();
            var canvas = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/PlayerGameOverCanvas_ManualPlace.prefab")));
            var controller = canvas.GetComponent<GameName.UI.PlayerGameOverCanvasController>();
            var retry = Get<UnityEngine.UI.Button>(controller, "retryButton");

            if (asynchronous)
                Object.FindFirstObjectByType<SaveManager>().StartCoroutine(SaveManager.LoadGameAsync(2));
            else
                Assert.That(SaveManager.TryLoadGame(2), Is.True);

            InvokeStatic(typeof(SaveManager), "TryApplyPendingLoad");
            Assert.That(Get<bool>(controller, "isVisible"), Is.True, "Restored zero HP must open Game Over.");
            Assert.That(SaveManager.IsLoadInProgress, Is.True);
            Assert.That(retry.interactable, Is.False, "The pending load must not become a checkpoint early.");
            yield return WaitForLoad();
            AssertResumeSlot(2);
            Assert.That(retry.interactable, Is.True, "The already-visible Retry button must refresh after commit.");
            Object.DestroyImmediate(canvas);
        }
    }

    [UnityTest]
    public IEnumerator RejectedLoads_PreserveLiveProgressAndCheckpoint()
    {
        SceneManager.SetActiveScene(SceneManager.CreateScene("Title"));
        var health = CreatePlayer(true);
        health.AddMaxHealth(6, true);
        GameProgressFlags.Set("live_flag", true);
        GameItems.SetCount("live_item", 3);
        CurrentLocationService.SetCurrentLocation("live_location");
        ElegantPointWallet.Clear();
        ElegantPointWallet.Add(87);
        Assert.That(SaveManager.TrySaveCurrentGame(2), Is.True);
        string before = JsonUtility.ToJson(Capture());
        Vector3 position = health.transform.position;
        var rejected = new SaveGameData { sceneName = "MissingScene_Fix0906Test" };
        Assert.That(SaveRepository.TryWrite(4, rejected), Is.True);
        File.WriteAllText(SaveRepository.GetSaveFilePath(5), "invalid JSON");
        int balanceNotifications = 0;
        Action<int> onBalanceChanged = _ => balanceNotifications++;
        ElegantPointWallet.BalanceChanged += onBalanceChanged;
        try
        {
            foreach (bool asynchronous in new[] { false, true })
            {
                foreach (int slot in new[] { 4, 5, 10 })
                {
                    if (slot == 4) LogAssert.Expect(LogType.Error, new Regex("SaveManager.*No loadable scene found"));
                    if (slot == 5) LogAssert.Expect(LogType.Error, new Regex("SaveRepository.*Failed to read save"));
                    if (asynchronous)
                        yield return SaveManager.LoadGameAsync(slot);
                    else
                        Assert.That(SaveManager.TryLoadGame(slot), Is.False);
                    Assert.That(SaveManager.IsLoadInProgress, Is.False);
                    Assert.That(JsonUtility.ToJson(Capture()), Is.EqualTo(before), $"Rejected slot {slot}, async={asynchronous}");
                    Assert.That(health.transform.position, Is.EqualTo(position));
                    Assert.That(balanceNotifications, Is.Zero, "A rejected load must not publish transient wallet changes.");
                    AssertResumeSlot(2);
                }
            }
        }
        finally
        {
            ElegantPointWallet.BalanceChanged -= onBalanceChanged;
        }
    }

    [UnityTest]
    public IEnumerator BuyingAura_RemovesExistingDropsWithoutGrantingTwice_AndEarlierLoadRestoresThem()
    {
        foreach (string color in new[] { "Blue", "Red" })
        {
            GameProgressFlags.ClearAll();
            GameItems.ClearAll();
            ElegantPointWallet.Clear();
            ElegantPointWallet.Add(100);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Items/{color}AuraEquipmentItem.prefab");
            var data = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Data/Items/Equipment/{color}Aura_Equipment.asset");
            var first = Track(Object.Instantiate(prefab));
            var second = Track(Object.Instantiate(prefab));
            first.transform.position = second.transform.position = new Vector3(1000, 1000, 0);
            yield return null;
            var earlier = Capture();
            Assert.That(DecorationPurchaseService.TryPurchase(data, out var result), Is.True);
            Assert.That(result, Is.EqualTo(DecorationPurchaseResult.Success));
            yield return null;
            Assert.That(first != null && first.activeSelf, Is.False, "Buying the aura must remove its existing drop immediately.");
            Assert.That(second != null && second.activeSelf, Is.False);
            Assert.That(GameItems.GetCount(data.itemId), Is.EqualTo(1));
            Assert.That(ElegantPointWallet.Balance, Is.EqualTo(100 - data.elegantPointCost));

            InvokeStatic(typeof(SaveManager), "RestoreModules", earlier);
            Assert.That(first != null && first.activeSelf, Is.True);
            Assert.That(second != null && second.activeSelf, Is.True);
            Assert.That(GameItems.GetCount(data.itemId), Is.Zero);
            Assert.That(ElegantPointWallet.Balance, Is.EqualTo(100));
            var health = CreatePlayer(false);
            Invoke(first.GetComponent<ItemPickup>(), "OnTriggerEnter2D", health.GetComponent<Collider2D>());
            Assert.That(GameItems.GetCount(data.itemId), Is.EqualTo(1));
            Assert.That(second.activeSelf, Is.False, "Normal collection must also clear another drop of the owned aura.");
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(health.gameObject);
        }
    }

    [UnityTest]
    public IEnumerator EarlierSameSceneLoads_RestorePickupAndEffectBeforeAndAfterCollectionFinishes()
    {
        SceneManager.SetActiveScene(SceneManager.CreateScene("Title"));
        var health = CreatePlayer(true);
        int initialMax = health.MaxHealth;
        var root = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/HitPointUpItem.prefab")));
        root.transform.position = new Vector3(1000, 1000, 0);
        var pickup = root.GetComponent<ItemPickup>();
        Set(pickup, "pickupSaveId", "same_scene_hp");
        var effect = root.GetComponent<ItemEffectController>();
        var visual = Get<Transform>(effect, "effectTransform");
        Transform visualParent = visual.parent;
        var disabledCollider = root.AddComponent<CircleCollider2D>();
        disabledCollider.enabled = false;
        yield return null;
        var earlier = Capture();
        earlier.sceneName = "Title";
        Assert.That(SaveRepository.TryWrite(2, earlier), Is.True);

        foreach (bool waitForEffect in new[] { true, false })
        {
            Invoke(pickup, "OnTriggerEnter2D", health.GetComponent<Collider2D>());
            Assert.That(health.MaxHealth, Is.EqualTo(initialMax + 2));
            var later = Capture();
            later.sceneName = "Title";
            Assert.That(SaveRepository.TryWrite(3, later), Is.True);
            if (waitForEffect)
            {
                float deadline = Time.realtimeSinceStartup + 5;
                while (root != null && root.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(root != null && root.activeSelf, Is.False, "The normal collection effect must finish.");
                Assert.That(SaveManager.TryLoadGame(2), Is.True);
                yield return WaitForLoad();
            }
            else
            {
                yield return SaveManager.LoadGameAsync(2);
            }

            Assert.That(root != null && root.activeSelf, Is.True, "An earlier same-scene save must restore the original pickup.");
            Assert.That(root.GetComponent<BoxCollider2D>().enabled, Is.True);
            Assert.That(disabledCollider.enabled, Is.False, "Restore only colliders disabled by collection.");
            Assert.That(visual.parent, Is.SameAs(visualParent));
            Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(health.MaxHealth, Is.EqualTo(initialMax));
            Assert.That(GameProgressFlags.Get("same_scene_hp"), Is.False);
            yield return new WaitForSeconds(0.5f);
            Assert.That(root.activeSelf, Is.True, "A stale pickup effect must not hide the restored pickup later.");

            yield return SaveManager.LoadGameAsync(3);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(health.MaxHealth, Is.EqualTo(initialMax + 2));
            yield return SaveManager.LoadGameAsync(2);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(health.MaxHealth, Is.EqualTo(initialMax));
        }

        var healData = Track(ScriptableObject.CreateInstance<ItemData>());
        healData.healAmount = 1;
        var healPickup = CreatePickup(healData, "");
        Invoke(healPickup, "OnTriggerEnter2D", health.GetComponent<Collider2D>());
        yield return null;
        Assert.That(healPickup == null, Is.True, "Nonpersistent healing drops retain their existing lifetime.");
    }

    private IEnumerator WaitForLoad()
    {
        float deadline = Time.realtimeSinceStartup + 3;
        while (SaveManager.IsLoadInProgress && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(SaveManager.IsLoadInProgress, Is.False);
    }

    private PlayerHealth CreatePlayer(bool withController)
    {
        var go = Track(new GameObject("Player"));
        go.SetActive(false);
        go.tag = "Player";
        go.AddComponent<Rigidbody2D>().gravityScale = 0;
        go.AddComponent<CapsuleCollider2D>().size = Vector2.one;
        go.AddComponent<DodgeController>();
        var health = go.AddComponent<PlayerHealth>();
        if (withController)
        {
            go.AddComponent<PlayerInput>().enabled = false;
            go.AddComponent<PlayerController>().enabled = false;
        }
        go.SetActive(true);
        health.RestoreFullHealth();
        return health;
    }

    private ItemPickup CreatePickup(ItemData data, string key)
    {
        var go = Track(new GameObject("Pickup"));
        go.SetActive(false);
        go.transform.position = new Vector3(1000, 1000, 0);
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        var pickup = go.AddComponent<ItemPickup>();
        Set(pickup, "itemData", data);
        Set(pickup, "pickupSaveId", key);
        go.SetActive(true);
        return pickup;
    }

    private ShutterWallBlockRise CreateShutter(string key)
    {
        var go = Track(new GameObject("Shutter"));
        go.SetActive(false);
        for (int i = 0; i < 3; i++)
        {
            var block = new GameObject("Block");
            block.transform.SetParent(go.transform, false);
            block.transform.localPosition = new Vector3(0, i, 0);
        }
        var shutter = go.AddComponent<ShutterWallBlockRise>();
        Set(shutter, "persistentStateKey", key);
        go.SetActive(true);
        return shutter;
    }

    private LeverSwitch2D CreateLever(ShutterWallBlockRise shutter, Sprite closed, Sprite opened)
    {
        var go = Track(new GameObject("Lever"));
        go.SetActive(false);
        go.AddComponent<SpriteRenderer>().sprite = closed;
        var lever = go.AddComponent<LeverSwitch2D>();
        Set(lever, "shutterWall", shutter);
        Set(lever, "closedSprite", closed);
        Set(lever, "openedSprite", opened);
        go.SetActive(true);
        return lever;
    }

    private Sprite CreateSprite(string name)
    {
        var texture = Track(new Texture2D(1, 1));
        var sprite = Track(Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero));
        sprite.name = name;
        return sprite;
    }

    private SaveGameData Capture()
    {
        var data = new SaveGameData();
        InvokeStatic(typeof(SaveManager), "CaptureModules", data);
        return JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data));
    }

    private static void AssertBlocks(ShutterWallBlockRise shutter, bool opened)
    {
        for (int i = 0; i < shutter.transform.childCount; i++)
            Assert.That(shutter.transform.GetChild(i).localPosition.y, Is.EqualTo(opened ? 3 : i).Within(0.001f));
    }

    private static void AssertResumeSlot(int expected)
    {
        Assert.That(SaveManager.TryGetResumeSaveSlot(out int slot, out _), Is.True);
        Assert.That(slot, Is.EqualTo(expected));
    }

    private T Track<T>(T obj) where T : Object { createdObjects.Add(obj); return obj; }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
    private static object InvokeStatic(Type type, string name, params object[] args) => type.GetMethod(name, PrivateStatic).Invoke(null, args);
    private static void SetSaveDirectory(string path) => typeof(SaveRepository).GetProperty("SaveDirectoryOverride", PrivateStatic).SetValue(null, path);
}
