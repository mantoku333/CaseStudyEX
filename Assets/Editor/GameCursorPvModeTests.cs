using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class GameCursorPvModeTests
{
    private GameObject cursorObject;

    [TearDown]
    public void TearDown()
    {
        PvModeState.SetActive(false);

        if (cursorObject != null)
        {
            Object.DestroyImmediate(cursorObject);
        }
    }

    [Test]
    public void PvMode_HidesCursorReticleAndReloadImages()
    {
        GameCursorController controller = CreateControllerWithVisibleImages(
            out Image cursorImage,
            out Image reticleImage,
            out Image reloadImage);

        PvModeState.SetActive(true);
        bool handled = InvokeHideCursorForPvMode(controller);

        Assert.That(handled, Is.True);
        Assert.That(cursorImage.enabled, Is.False);
        Assert.That(reticleImage.enabled, Is.False);
        Assert.That(reloadImage.enabled, Is.False);
    }

    [Test]
    public void PvModeDisabled_AllowsNormalCursorRenderingToResume()
    {
        GameCursorController controller = CreateControllerWithVisibleImages(
            out Image cursorImage,
            out Image reticleImage,
            out Image reloadImage);

        PvModeState.SetActive(false);
        bool handled = InvokeHideCursorForPvMode(controller);

        Assert.That(handled, Is.False);
        Assert.That(cursorImage.enabled, Is.True);
        Assert.That(reticleImage.enabled, Is.True);
        Assert.That(reloadImage.enabled, Is.True);
    }

    [Test]
    public void PvModeState_RemainsActiveUntilExplicitlyDisabled()
    {
        PvModeState.SetActive(true);

        Assert.That(PvModeState.IsActive, Is.True);

        PvModeState.SetActive(false);
        Assert.That(PvModeState.IsActive, Is.False);
    }

    private GameCursorController CreateControllerWithVisibleImages(
        out Image cursorImage,
        out Image reticleImage,
        out Image reloadImage)
    {
        cursorObject = new GameObject("PV Mode Cursor Test");
        GameCursorController controller = cursorObject.AddComponent<GameCursorController>();
        cursorImage = CreateImage("CursorImage");
        reticleImage = CreateImage("ReticleImage");
        reloadImage = CreateImage("ReloadImage");

        SetPrivateField(controller, "cursorImage", cursorImage);
        SetPrivateField(controller, "reticleImage", reticleImage);
        SetPrivateField(controller, "reloadImage", reloadImage);

        cursorImage.enabled = true;
        reticleImage.enabled = true;
        reloadImage.enabled = true;
        return controller;
    }

    private Image CreateImage(string objectName)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(cursorObject.transform, false);
        return imageObject.GetComponent<Image>();
    }

    private static bool InvokeHideCursorForPvMode(GameCursorController controller)
    {
        MethodInfo method = typeof(GameCursorController).GetMethod(
            "HideCursorForPvMode",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(controller, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
