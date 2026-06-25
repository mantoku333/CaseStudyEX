using System.Collections.Generic;
using System.Reflection;
using CaseStudy.EditorTools;
using Metroidvania.Managers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

[Category("Story/Event")]
public sealed class StoryEventPlaybackTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Selection.activeObject = null;
        GameProgressFlags.ClearAll();
        StoryPauseRuntime.DialogueDefaultPolicy = StoryPausePolicy.TimeScaleZero;
        StoryPauseRuntime.ClearOverride();

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
    public void StoryEventDefinition_MatchesConfiguredScene_AllowsExactAndUnityDuplicateSceneNames()
    {
        Assert.That(StoryEventDefinition.MatchesConfiguredScene("Event_IkedaEX", "Event_IkedaEX"), Is.True);
        Assert.That(StoryEventDefinition.MatchesConfiguredScene("Event_IkedaEX", "Event_IkedaEX 1"), Is.True);
        Assert.That(StoryEventDefinition.MatchesConfiguredScene("Event_IkedaEX", "Event_IkedaEX Copy"), Is.False);
        Assert.That(StoryEventDefinition.MatchesConfiguredScene("Event_IkedaEX", "Event_IkedaEXtra 1"), Is.False);
    }

    [Test]
    public void StoryEventDefinition_MatchesScene_WhenConfiguredSceneIsBlank_AppliesToAnyScene()
    {
        StoryEventDefinition definition = new StoryEventDefinition
        {
            sceneName = "  "
        };

        Assert.That(definition.MatchesScene("Stage01"), Is.True);
        Assert.That(definition.MatchesScene(string.Empty), Is.True);
    }

    [Test]
    public void SceneStartStoryEventSource_CreateDefinition_TrimsEventPlaybackFields()
    {
        SceneStartStoryEventSource source = CreateSceneStartSource();
        SetPrivateField(source, "eventId", "  prologue_intro  ");
        SetPrivateField(source, "storyEventControllerId", "  StoryEvent_Intro  ");
        SetPrivateField(source, "dialogueNodeName", "  Prologue_Start  ");

        StoryEventDefinition definition = source.CreateDefinition("Event_IkedaEX");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.eventId, Is.EqualTo("prologue_intro"));
        Assert.That(definition.sceneName, Is.EqualTo("Event_IkedaEX"));
        Assert.That(definition.storyEventControllerId, Is.EqualTo("StoryEvent_Intro"));
        Assert.That(definition.dialogueNodeName, Is.EqualTo("Prologue_Start"));
    }

    [Test]
    public void SceneStartStoryEventSource_CreateDefinition_ReturnsNullWithoutDialogueOrController()
    {
        SceneStartStoryEventSource source = CreateSceneStartSource();
        SetPrivateField(source, "dialogueNodeName", " ");
        SetPrivateField(source, "storyEventControllerId", " ");

        Assert.That(source.CreateDefinition("Event_IkedaEX"), Is.Null);
    }

    [Test]
    public void StoryEventController_EventIdAndMemoName_TrimValuesAndFallbackToObjectName()
    {
        StoryEventController controller = CreateStoryEventController("StoryEventObject");

        SetPrivateField(controller, "eventId", "  event_intro  ");
        SetPrivateField(controller, "memoName", "  Intro Memo  ");

        Assert.That(controller.EventId, Is.EqualTo("event_intro"));
        Assert.That(controller.MemoName, Is.EqualTo("Intro Memo"));

        SetPrivateField(controller, "eventId", " ");
        SetPrivateField(controller, "memoName", " ");

        Assert.That(controller.EventId, Is.EqualTo("StoryEventObject"));
        Assert.That(controller.MemoName, Is.EqualTo(string.Empty));
    }

    [Test]
    public void EventPlaybackWindow_BuildControllerLabel_IncludesMemoWhenAvailable()
    {
        StoryEventController controller = CreateStoryEventController("StoryEventObject");
        SetPrivateField(controller, "eventId", "  event_intro  ");
        SetPrivateField(controller, "memoName", "  Intro Memo  ");

        string label = InvokeEventPlaybackStatic<string>("BuildControllerLabel", controller);

        Assert.That(label, Is.EqualTo("event_intro / Intro Memo"));
    }

    [Test]
    public void EventPlaybackWindow_ResolveSelectionController_UsesSelectedChildsParentController()
    {
        StoryEventController controller = CreateStoryEventController("StoryEventObject");
        GameObject child = new GameObject("Child");
        child.transform.SetParent(controller.transform);
        objectsToDestroy.Add(child);
        Selection.activeGameObject = child;

        StoryEventController resolved = InvokeEventPlaybackStatic<StoryEventController>("ResolveSelectionController");

        Assert.That(resolved, Is.EqualTo(controller));
    }

    [Test]
    public void EventPlaybackWindow_CompareControllers_SortsByEventIdThenObjectName()
    {
        StoryEventController alpha = CreateStoryEventController("B_Object");
        SetPrivateField(alpha, "eventId", "alpha");
        StoryEventController beta = CreateStoryEventController("A_Object");
        SetPrivateField(beta, "eventId", "beta");
        StoryEventController alphaByName = CreateStoryEventController("A_Object");
        SetPrivateField(alphaByName, "eventId", "alpha");

        Assert.That(InvokeEventPlaybackStatic<int>("CompareControllers", alpha, beta), Is.LessThan(0));
        Assert.That(InvokeEventPlaybackStatic<int>("CompareControllers", alphaByName, alpha), Is.LessThan(0));
        Assert.That(InvokeEventPlaybackStatic<int>("CompareControllers", null, alpha), Is.GreaterThan(0));
        Assert.That(InvokeEventPlaybackStatic<int>("CompareControllers", alpha, null), Is.LessThan(0));
    }

    [Test]
    public void StoryPauseRuntime_UsesOverrideAndFallsBackToDialogueDefault()
    {
        StoryPauseRuntime.DialogueDefaultPolicy = StoryPausePolicy.GameplayOnly;
        StoryPauseRuntime.ClearOverride();

        Assert.That(StoryPauseRuntime.HasOverride, Is.False);
        Assert.That(StoryPauseRuntime.EffectivePolicy, Is.EqualTo(StoryPausePolicy.GameplayOnly));

        StoryPauseRuntime.SetOverride(StoryPausePolicy.None);

        Assert.That(StoryPauseRuntime.HasOverride, Is.True);
        Assert.That(StoryPauseRuntime.EffectivePolicy, Is.EqualTo(StoryPausePolicy.None));

        StoryPauseRuntime.SetOverride(StoryPausePolicy.UseDialogueDefault);

        Assert.That(StoryPauseRuntime.EffectivePolicy, Is.EqualTo(StoryPausePolicy.GameplayOnly));

        StoryPauseRuntime.ClearOverride();

        Assert.That(StoryPauseRuntime.HasOverride, Is.False);
        Assert.That(StoryPauseRuntime.EffectivePolicy, Is.EqualTo(StoryPausePolicy.GameplayOnly));
    }

    [Test]
    public void StoryYarnDialogueClip_CreatePlayable_CopiesTrimmedSettingsIntoBehaviour()
    {
        StoryYarnDialogueClip clip = ScriptableObject.CreateInstance<StoryYarnDialogueClip>();
        objectsToDestroy.Add(clip);
        SetPrivateField(clip, "clipId", "  clip-001  ");
        SetPrivateField(clip, "nodeName", "  Prologue_Node  ");
        SetPrivateField(clip, "useControllerDefaultStyle", false);
        SetPrivateField(clip, "dialogueStyle", DialogueStyle.ADV);
        SetPrivateField(clip, "pauseTimelineUntilComplete", false);
        SetPrivateField(clip, "bubbleActorKey", "  iris  ");

        PlayableGraph graph = PlayableGraph.Create("StoryYarnDialogueClipTest");
        try
        {
            ScriptPlayable<StoryYarnDialoguePlayable> playable =
                (ScriptPlayable<StoryYarnDialoguePlayable>)clip.CreatePlayable(graph, null);
            StoryYarnDialoguePlayable behaviour = playable.GetBehaviour();

            Assert.That(behaviour.clipId, Is.EqualTo("  clip-001  "));
            Assert.That(behaviour.nodeName, Is.EqualTo("Prologue_Node"));
            Assert.That(behaviour.useControllerDefaultStyle, Is.False);
            Assert.That(behaviour.dialogueStyle, Is.EqualTo(DialogueStyle.ADV));
            Assert.That(behaviour.pauseTimelineUntilComplete, Is.False);
            Assert.That(behaviour.bubbleActorKey, Is.EqualTo("iris"));
        }
        finally
        {
            graph.Destroy();
        }
    }

    [Test]
    public void StoryYarnDialogueClip_DefaultsBlankNodeAndActorKey()
    {
        StoryYarnDialogueClip clip = ScriptableObject.CreateInstance<StoryYarnDialogueClip>();
        objectsToDestroy.Add(clip);
        SetPrivateField(clip, "clipId", " ");
        SetPrivateField(clip, "nodeName", " ");
        SetPrivateField(clip, "bubbleActorKey", " ");

        Assert.That(clip.NodeName, Is.EqualTo("Start"));
        Assert.That(clip.BubbleActorKey, Is.EqualTo(string.Empty));
        Assert.That(clip.ClipId, Is.EqualTo("Start"));
    }

    [Test]
    public void StoryYarnDialogueMarker_TriggerKey_UsesTrimmedMarkerNodeAndTime()
    {
        StoryYarnDialogueMarker marker = ScriptableObject.CreateInstance<StoryYarnDialogueMarker>();
        objectsToDestroy.Add(marker);
        SetPrivateField(marker, "markerId", "  marker-001  ");
        SetPrivateField(marker, "nodeName", "  Prologue_Node  ");
        SetPrivateField(marker, "bubbleActorKey", "  iris  ");
        marker.time = 1.25d;

        Assert.That(marker.NodeName, Is.EqualTo("Prologue_Node"));
        Assert.That(marker.BubbleActorKey, Is.EqualTo("iris"));
        Assert.That(marker.TriggerKey, Is.EqualTo("marker-001|Prologue_Node|1.25"));
    }

    private SceneStartStoryEventSource CreateSceneStartSource()
    {
        GameObject gameObject = new GameObject("SceneStartStoryEventSource");
        objectsToDestroy.Add(gameObject);
        return gameObject.AddComponent<SceneStartStoryEventSource>();
    }

    private StoryEventController CreateStoryEventController(string objectName)
    {
        GameObject gameObject = new GameObject(objectName);
        objectsToDestroy.Add(gameObject);
        return gameObject.AddComponent<StoryEventController>();
    }

    private static T InvokeEventPlaybackStatic<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(EventPlaybackWindow).GetMethod(methodName, StaticPrivate);
        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        return (T)method.Invoke(null, arguments);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"{fieldName} must exist.");
        field.SetValue(target, value);
    }
}
