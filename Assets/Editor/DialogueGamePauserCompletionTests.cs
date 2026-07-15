using System.Reflection;
using Metroidvania.Managers;
using NUnit.Framework;
using UnityEngine;

public sealed class DialogueGamePauserCompletionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    private GameObject pauserObject;
    private DialogueGamePauser pauser;

    [SetUp]
    public void SetUp()
    {
        pauserObject = new GameObject("DialogueGamePauser_CompletionTest");
        pauserObject.SetActive(false);
        pauser = pauserObject.AddComponent<DialogueGamePauser>();
    }

    [TearDown]
    public void TearDown()
    {
        if (pauserObject != null)
        {
            Object.DestroyImmediate(pauserObject);
        }
    }

    [Test]
    public void ActiveDialogue_DoesNotReleaseGameplayPause()
    {
        Assert.That(ShouldRecover(isPaused: true, hasRunner: true, isRunning: true), Is.False);
    }

    [TestCase(true, true, false)]
    [TestCase(true, false, false)]
    public void StoppedOrMissingRunner_ReleasesMissedCompletion(
        bool isPaused,
        bool hasRunner,
        bool isRunning)
    {
        Assert.That(ShouldRecover(isPaused, hasRunner, isRunning), Is.True);
    }

    [Test]
    public void IdlePauser_DoesNotRunRecovery()
    {
        Assert.That(ShouldRecover(isPaused: false, hasRunner: false, isRunning: false), Is.False);
    }

    [Test]
    public void LateUpdate_WithMissingRunner_ClearsStaleGameplayPause()
    {
        SetField("gameplayPaused", true);

        InvokeInstance("LateUpdate");

        Assert.That(GetField<bool>("gameplayPaused"), Is.False);
    }

    [Test]
    public void ForceResumeForStoryEventExit_ClearsStaleGameplayPause()
    {
        SetField("gameplayPaused", true);

        pauser.ForceResumeForStoryEventExit();

        Assert.That(GetField<bool>("gameplayPaused"), Is.False);
    }

    private static bool ShouldRecover(bool isPaused, bool hasRunner, bool isRunning)
    {
        MethodInfo method = typeof(DialogueGamePauser).GetMethod(
            "ShouldRecoverMissedDialogueCompletion",
            PrivateStatic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(null, new object[] { isPaused, hasRunner, isRunning });
    }

    private void InvokeInstance(string methodName)
    {
        MethodInfo method = typeof(DialogueGamePauser).GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null);
        method.Invoke(pauser, null);
    }

    private void SetField(string fieldName, object value)
    {
        FieldInfo field = typeof(DialogueGamePauser).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(pauser, value);
    }

    private T GetField<T>(string fieldName)
    {
        FieldInfo field = typeof(DialogueGamePauser).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(pauser);
    }
}
