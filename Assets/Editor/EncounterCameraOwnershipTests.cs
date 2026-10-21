using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EncounterCameraOwnershipTests
{
    private BossAreaController owner;
    private CinemachineCamera encounterCamera;
    private CinemachineCamera otherCamera;
    private CinemachineBrain brain;

    [SetUp]
    public void SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        owner = new GameObject("Encounter").AddComponent<BossAreaController>();
        encounterCamera = CreateCamera("Combat", 50);
        otherCamera = CreateCamera("Room", 20);
        Camera output = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        output.orthographic = true;
        output.aspect = 16f / 9f;
        brain = output.gameObject.AddComponent<CinemachineBrain>();
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        EncounterCameraOwnership.Release(owner);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator Ownership_TakesPrecedenceOverTimelineOverrideAndReleasesItCleanly()
    {
        int timelineOverride = brain.SetCameraOverride(-1, 100000, null, otherCamera, 1f, 0.02f);
        Assert.That(EncounterCameraOwnership.Acquire(owner, encounterCamera), Is.True);
        yield return null;
        brain.ManualUpdate();
        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(encounterCamera));

        Assert.That(EncounterCameraOwnership.Release(owner), Is.True);
        yield return null;
        brain.ManualUpdate();
        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(otherCamera));
        brain.ReleaseCameraOverride(timelineOverride);
        Assert.That(EncounterCameraOwnership.Release(owner), Is.False);
    }

    [Test]
    public void Ownership_DoesNotRewritePoseLensFollowOrMovementDriver()
    {
        Transform target = new GameObject("ExistingTarget").transform;
        encounterCamera.Follow = target;
        CinemachineFollow follow = encounterCamera.gameObject.AddComponent<CinemachineFollow>();
        follow.FollowOffset = new Vector3(3f, -4f, -24f);
        encounterCamera.transform.position = new Vector3(243f, -54f, -34f);
        LensSettings lens = encounterCamera.Lens;
        lens.OrthographicSize = 10.5f;
        encounterCamera.Lens = lens;
        string followSettings = JsonUtility.ToJson(follow);
        Vector3 position = encounterCamera.transform.position;

        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        EncounterCameraOwnership.Maintain(owner);
        EncounterCameraOwnership.Maintain(owner);

        Assert.That(encounterCamera.Follow, Is.SameAs(target));
        Assert.That(encounterCamera.transform.position, Is.EqualTo(position));
        Assert.That(encounterCamera.Lens.OrthographicSize, Is.EqualTo(10.5f));
        Assert.That(JsonUtility.ToJson(follow), Is.EqualTo(followSettings));
        Assert.That(encounterCamera.GetComponent<DualTargetCameraTarget>(), Is.Null);
    }

    [UnityTest]
    public IEnumerator Ownership_KeepsUsingTheLiveMovingCamera()
    {
        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        yield return null;
        brain.ManualUpdate();
        encounterCamera.transform.position = new Vector3(251f, -54f, -34f);
        LensSettings lens = encounterCamera.Lens;
        lens.OrthographicSize = 14f;
        encounterCamera.Lens = lens;
        yield return null;
        encounterCamera.PreviousStateIsValid = false;
        brain.ManualUpdate();

        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(encounterCamera));
        Assert.That(brain.transform.position.x, Is.EqualTo(251f).Within(0.001f));
        Assert.That(brain.OutputCamera.orthographicSize, Is.EqualTo(14f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator Entry_BlendsFromTheRenderedViewToTheLiveCombatCameraWithoutRestarting()
    {
        otherCamera.Priority.Value = 100;
        otherCamera.transform.position = new Vector3(10f, 20f, -10f);
        SetSize(otherCamera, 12f);
        encounterCamera.transform.position = new Vector3(50f, 40f, -34f);
        SetSize(encounterCamera, 10f);
        yield return null;
        brain.ManualUpdate();
        Vector3 incomingView = brain.transform.position;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 2.4f);

        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        brain.ManualUpdate();
        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(encounterCamera));
        Assert.That(brain.IsBlending, Is.True);
        Assert.That(Vector3.Distance(brain.transform.position, incomingView), Is.LessThan(0.001f));
        Assert.That(brain.OutputCamera.orthographicSize, Is.EqualTo(12f).Within(0.001f));

        // Neither a moving room camera nor a competing Timeline can alter the source.
        otherCamera.transform.position = Vector3.one * -999f;
        SetSize(otherCamera, 40f);
        int timeline = brain.SetCameraOverride(-1, 100000, null, otherCamera, 1f, 0.02f);
        encounterCamera.transform.position = new Vector3(60f, 45f, -34f);
        SetSize(encounterCamera, 8f);
        yield return null;
        encounterCamera.PreviousStateIsValid = false;
        SetEntryElapsed(1.2f);
        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        EncounterCameraOwnership.Maintain(owner);
        brain.ManualUpdate();
        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(encounterCamera));
        Assert.That(Vector3.Distance(brain.transform.position, new Vector3(35f, 32.5f, -22f)), Is.LessThan(0.001f));
        Assert.That(brain.OutputCamera.orthographicSize, Is.EqualTo(10f).Within(0.001f));

        SetEntryElapsed(2.4f);
        EncounterCameraOwnership.Maintain(owner);
        brain.ManualUpdate();
        Assert.That(brain.IsBlending, Is.False);
        Assert.That(brain.transform.position, Is.EqualTo(encounterCamera.transform.position));
        Assert.That(brain.OutputCamera.orthographicSize, Is.EqualTo(8f).Within(0.001f));
        brain.ReleaseCameraOverride(timeline);
    }

    [UnityTest]
    public IEnumerator Entry_UsesTheCurrentRoomBlendAndHonorsCustomBlendSettings()
    {
        otherCamera.Priority.Value = 100;
        otherCamera.transform.position = new Vector3(10f, 0f, -10f);
        encounterCamera.transform.position = new Vector3(50f, 0f, -10f);
        yield return null;
        brain.ManualUpdate();
        var third = CreateCamera("NextRoom", 200);
        third.transform.position = new Vector3(90f, 0f, -10f);
        int roomBlend = brain.SetCameraOverride(-1, 1000, otherCamera, third, 0.25f, 0.02f);
        yield return null;
        brain.ManualUpdate();
        Assert.That(brain.transform.position.x, Is.EqualTo(30f).Within(0.001f));
        encounterCamera.ForceCameraPosition(new Vector3(50f, 0f, -10f), Quaternion.identity);

        var custom = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
        custom.CustomBlends = new[] { new CinemachineBlenderSettings.CustomBlend {
            From = third.Name, To = encounterCamera.Name,
            Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Linear, 4f)
        } };
        brain.CustomBlends = custom;
        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        SetEntryElapsed(1f);
        EncounterCameraOwnership.Maintain(owner);
        brain.ManualUpdate();
        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(encounterCamera));
        Assert.That(brain.transform.position.x, Is.EqualTo(35f).Within(0.001f));
        brain.ReleaseCameraOverride(roomBlend);
        brain.CustomBlends = null;
        Object.DestroyImmediate(custom);
    }

    [UnityTest]
    public IEnumerator Entry_CanBeCancelledAndReacquiredWithoutRetainingTheOldBlend()
    {
        otherCamera.Priority.Value = 100;
        otherCamera.transform.position = new Vector3(10f, 0f, -10f);
        encounterCamera.transform.position = new Vector3(50f, 0f, -10f);
        yield return null;
        brain.ManualUpdate();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 2.4f);
        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        SetEntryElapsed(0.6f);
        EncounterCameraOwnership.Maintain(owner);
        brain.ManualUpdate();
        Assert.That(brain.IsBlending, Is.True);
        EncounterCameraOwnership.Release(owner);
        brain.ManualUpdate();
        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(otherCamera));
        Assert.That(brain.transform.position.x, Is.EqualTo(10f).Within(0.001f));

        otherCamera.transform.position = new Vector3(20f, 0f, -10f);
        yield return null;
        otherCamera.PreviousStateIsValid = false;
        brain.ManualUpdate();
        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        brain.ManualUpdate();
        Assert.That(brain.ActiveVirtualCamera, Is.SameAs(encounterCamera));
        Assert.That(brain.transform.position.x, Is.EqualTo(20f).Within(0.001f));
    }

    [Test]
    public void Ownership_BlocksRoomAndPortalActivationUntilReleased()
    {
        RoomCameraTrigger room = new GameObject("RoomEndpoint").AddComponent<RoomCameraTrigger>();
        SetField(room, "_roomCamera", otherCamera);
        room.ActivateCamera();
        RoomCameraTrigger activeBeforeEncounter = RoomCameraTrigger.ActiveRoom;
        RoomCameraTrigger another = new GameObject("AnotherRoom").AddComponent<RoomCameraTrigger>();
        SetField(another, "_roomCamera", encounterCamera);

        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        another.ActivateCamera();
        Assert.That(RoomCameraTrigger.ActiveRoom, Is.SameAs(activeBeforeEncounter));
        Assert.That(RoomCameraTrigger.HoldCameraForPortal(owner, another), Is.False);
        Assert.That(RoomCameraTrigger.CommitCameraFromPortal(owner, another), Is.False);

        EncounterCameraOwnership.Release(owner);
        another.ActivateCamera();
        Assert.That(RoomCameraTrigger.ActiveRoom, Is.SameAs(another));
    }

    [Test]
    public void Ownership_DoesNotExposeTheCombatCameraToStoryMoveOrZoomTracks()
    {
        StoryEventController story = new GameObject("Story").AddComponent<StoryEventController>();
        SetField(story, "eventCamera", encounterCamera);
        EncounterCameraOwnership.Acquire(owner, encounterCamera);
        Assert.That(story.GetEventCameraForTimeline(), Is.Null);
        EncounterCameraOwnership.Release(owner);
        Assert.That(story.GetEventCameraForTimeline(), Is.SameAs(encounterCamera));
    }

    [Test]
    public void CombatInitialization_CanUseTheIncomingViewWithoutChangingMotionSettings()
    {
        Transform primary = new GameObject("PlayerTarget").transform;
        Transform secondary = new GameObject("BossTarget").transform;
        DualTargetCameraTarget target = new GameObject("ExistingCombatDriver").AddComponent<DualTargetCameraTarget>();
        target.transform.position = new Vector3(0f, 0f, -24f);
        Vector3 incoming = new Vector3(637f, 56f, -10f);
        float size = encounterCamera.Lens.OrthographicSize;
        float smoothing = (float)typeof(DualTargetCameraTarget).GetField("smoothTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        target.Configure(primary, secondary, encounterCamera, null, incoming);

        Assert.That(target.transform.position, Is.EqualTo(new Vector3(637f, 56f, -24f)));
        Assert.That(encounterCamera.Follow, Is.SameAs(target.transform));
        Assert.That(encounterCamera.Lens.OrthographicSize, Is.EqualTo(size));
        Assert.That((float)typeof(DualTargetCameraTarget).GetField("smoothTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target), Is.EqualTo(smoothing));
    }

    private static CinemachineCamera CreateCamera(string name, int priority)
    {
        CinemachineCamera camera = new GameObject(name).AddComponent<CinemachineCamera>();
        camera.Priority.Enabled = true;
        camera.Priority.Value = priority;
        return camera;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void SetSize(CinemachineCamera camera, float size)
    {
        LensSettings lens = camera.Lens;
        lens.OrthographicSize = size;
        camera.Lens = lens;
    }

    private static void SetEntryElapsed(float elapsed)
    {
        var entries = (IList)typeof(EncounterCameraOwnership)
            .GetField("overrides", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        entries[0].GetType().GetField("StartedAt").SetValue(entries[0], Time.unscaledTimeAsDouble - elapsed);
    }
}
