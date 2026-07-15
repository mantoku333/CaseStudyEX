using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Yarn.Unity;

public sealed class StoryEventControllerDialogueCompletionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject controllerObject;
    private StoryEventController controller;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("StoryEventController_DialogueCompletionTest");
        controller = controllerObject.AddComponent<StoryEventController>();
    }

    [TearDown]
    public void TearDown()
    {
        if (controllerObject != null)
        {
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void CompletionCallback_ClearsPendingState()
    {
        SetField("waitingDialogueCompletion", true);

        Invoke("OnDialogueComplete");

        Assert.That(EvaluatePending(hasRunner: true, runnerIsRunning: true), Is.False);
        Assert.That(GetField<bool>("waitingDialogueCompletion"), Is.False);
    }

    [Test]
    public void ActiveRunner_RemainsPendingWithoutTimeout()
    {
        SetField("waitingDialogueCompletion", true);

        Assert.That(EvaluatePending(hasRunner: true, runnerIsRunning: true), Is.True);
        Assert.That(EvaluatePending(hasRunner: true, runnerIsRunning: true), Is.True);
        Assert.That(GetField<bool>("waitingDialogueCompletion"), Is.True);
    }

    [Test]
    public void StoppedRunner_RecoversWhenCompletionCallbackWasMissed()
    {
        SetField("waitingDialogueCompletion", true);
        Assert.That(EvaluatePending(hasRunner: true, runnerIsRunning: true), Is.True);

        Assert.That(EvaluatePending(hasRunner: true, runnerIsRunning: false), Is.False);
        Assert.That(GetField<bool>("waitingDialogueCompletion"), Is.False);
    }

    [Test]
    public void MissingRunner_GetsStartupObservationThenRecovers()
    {
        SetField("waitingDialogueCompletion", true);

        Assert.That(EvaluatePending(hasRunner: false, runnerIsRunning: false, isStartFrame: true), Is.True);
        Assert.That(EvaluatePending(hasRunner: false, runnerIsRunning: false), Is.False);
        Assert.That(GetField<bool>("waitingDialogueCompletion"), Is.False);
    }

    [Test]
    public void Unsubscribe_ClearsRunnerAndStateForNextDialogue()
    {
        GameObject runnerObject = new GameObject("DialogueRunner_DialogueCompletionTest");
        DialogueRunner runner = runnerObject.AddComponent<DialogueRunner>();

        try
        {
            SetField("activeDialogueRunner", runner);
            SetField("waitingDialogueCompletion", true);
            SetField("dialogueStartedFrame", Time.frameCount);

            Invoke("UnsubscribeDialogueComplete");

            Assert.That(GetField<DialogueRunner>("activeDialogueRunner"), Is.Null);
            Assert.That(GetField<bool>("waitingDialogueCompletion"), Is.False);
            Assert.That(GetField<int>("dialogueStartedFrame"), Is.EqualTo(-1));
        }
        finally
        {
            Object.DestroyImmediate(runnerObject);
        }
    }

    private bool EvaluatePending(bool hasRunner, bool runnerIsRunning, bool isStartFrame = false)
    {
        return (bool)Invoke(
            "UpdateDialogueCompletionPending",
            hasRunner,
            runnerIsRunning,
            isStartFrame);
    }

    private object Invoke(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(StoryEventController).GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found.");
        return method.Invoke(controller, arguments);
    }

    private void SetField(string fieldName, object value)
    {
        FieldInfo field = typeof(StoryEventController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
        field.SetValue(controller, value);
    }

    private T GetField<T>(string fieldName)
    {
        FieldInfo field = typeof(StoryEventController).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
        return (T)field.GetValue(controller);
    }
}
