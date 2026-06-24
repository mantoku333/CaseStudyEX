using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StageBgmControllerTests
{
    private GameObject controllerObject;
    private GameObject areaA;
    private GameObject areaB;
    private AudioClip normalClip;
    private AudioClip bossClip;
    private AudioClip areaClipA;
    private AudioClip areaClipB;
    private StageBgmController controller;

    [SetUp]
    public void SetUp()
    {
        normalClip = CreateClip("Normal");
        bossClip = CreateClip("Boss");
        areaClipA = CreateClip("AreaA");
        areaClipB = CreateClip("AreaB");
        areaA = new GameObject("AreaAOwner");
        areaB = new GameObject("AreaBOwner");
        controllerObject = new GameObject("StageBgmController");
        controller = controllerObject.AddComponent<StageBgmController>();

        SetPrivateField("normalStageBgm", normalClip);
        SetPrivateField("bossStageBgm", bossClip);
        SetPrivateField("crossfadeDuration", 0f);
        InvokePrivate("Awake");
        controller.PlayNormalImmediate();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(controllerObject);
        Object.DestroyImmediate(areaA);
        Object.DestroyImmediate(areaB);
        Object.DestroyImmediate(normalClip);
        Object.DestroyImmediate(bossClip);
        Object.DestroyImmediate(areaClipA);
        Object.DestroyImmediate(areaClipB);
    }

    [Test]
    public void AreaBgm_HigherPriorityWins_AndClearingRestoresPreviousArea()
    {
        controller.SetAreaBgm(areaA, areaClipA, 0.2f, 0);
        controller.SetAreaBgm(areaB, areaClipB, 0.2f, 10);

        Assert.That(controller.RequestedBgmClip, Is.SameAs(areaClipB));

        controller.ClearAreaBgm(areaB);

        Assert.That(controller.RequestedBgmClip, Is.SameAs(areaClipA));
    }

    [Test]
    public void BossBgm_IsNotOverriddenByArea_AndPlayNormalReturnsToArea()
    {
        controller.PlayBoss();
        controller.SetAreaBgm(areaA, areaClipA, 0.2f, 0);

        Assert.That(controller.RequestedBgmClip, Is.SameAs(bossClip));

        controller.PlayNormal();

        Assert.That(controller.RequestedBgmClip, Is.SameAs(areaClipA));
    }

    [Test]
    public void ClearingLastArea_RestoresNormalBgm()
    {
        controller.SetAreaBgm(areaA, areaClipA, 0.2f, 0);
        controller.ClearAreaBgm(areaA);

        Assert.That(controller.RequestedBgmClip, Is.SameAs(normalClip));
    }

    private static AudioClip CreateClip(string name)
    {
        return AudioClip.Create(name, 64, 1, 44100, false);
    }

    private void SetPrivateField(string fieldName, object value)
    {
        FieldInfo field = typeof(StageBgmController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(controller, value);
    }

    private void InvokePrivate(string methodName)
    {
        MethodInfo method = typeof(StageBgmController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(controller, null);
    }
}
