using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[Category("UI/Menu")]
public sealed class OptionsMenuTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

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
    public void CaptureAndRestorePausedPlayerVelocity_PreservesPlayerRigidbodyVelocity()
    {
        OptionsMenu menu = CreateMenu();
        Rigidbody2D playerRigidbody = CreateTaggedPlayer();
        Vector2 launchVelocity = new Vector2(3.25f, 12.5f);
        float angularVelocity = 22.5f;
        playerRigidbody.linearVelocity = launchVelocity;
        playerRigidbody.angularVelocity = angularVelocity;

        InvokePrivate(menu, "CapturePausedPlayerVelocity", playerRigidbody.gameObject);

        Assert.That(playerRigidbody.linearVelocity.x, Is.EqualTo(launchVelocity.x).Within(0.001f));
        Assert.That(playerRigidbody.linearVelocity.y, Is.EqualTo(launchVelocity.y).Within(0.001f));
        Assert.That(playerRigidbody.angularVelocity, Is.EqualTo(angularVelocity).Within(0.001f));

        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;

        InvokePrivate(menu, "RestorePausedPlayerVelocity");

        Assert.That(playerRigidbody.linearVelocity.x, Is.EqualTo(launchVelocity.x).Within(0.001f));
        Assert.That(playerRigidbody.linearVelocity.y, Is.EqualTo(launchVelocity.y).Within(0.001f));
        Assert.That(playerRigidbody.angularVelocity, Is.EqualTo(angularVelocity).Within(0.001f));
    }

    [Test]
    public void PauseAndRestoreGameplay_RestoresTimeScaleAndPausedPlayerVelocity()
    {
        OptionsMenu menu = CreateMenu();
        Rigidbody2D playerRigidbody = CreateTaggedPlayer();
        Vector2 launchVelocity = new Vector2(-4f, 6.5f);
        float angularVelocity = -18f;
        playerRigidbody.linearVelocity = launchVelocity;
        playerRigidbody.angularVelocity = angularVelocity;
        Time.timeScale = 0.65f;

        InvokePrivate(menu, "PauseGameplay");

        Assert.That(Time.timeScale, Is.EqualTo(0f));

        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;

        InvokePrivate(menu, "RestoreGameplayState");

        Assert.That(Time.timeScale, Is.EqualTo(0.65f).Within(0.001f));
        Assert.That(playerRigidbody.linearVelocity.x, Is.EqualTo(launchVelocity.x).Within(0.001f));
        Assert.That(playerRigidbody.linearVelocity.y, Is.EqualTo(launchVelocity.y).Within(0.001f));
        Assert.That(playerRigidbody.angularVelocity, Is.EqualTo(angularVelocity).Within(0.001f));
    }

    [Test]
    public void CapturePausedPlayerVelocity_WhenTargetHasNoRigidbody_ClearsPreviousCapture()
    {
        OptionsMenu menu = CreateMenu();
        Rigidbody2D playerRigidbody = CreateTaggedPlayer();
        playerRigidbody.linearVelocity = new Vector2(2f, 3f);

        InvokePrivate(menu, "CapturePausedPlayerVelocity", playerRigidbody.gameObject);

        Assert.That(GetPrivateField<bool>(menu, "hasPausedPlayerVelocity"), Is.True);
        Assert.That(GetPrivateField<Rigidbody2D>(menu, "pausedPlayerRigidbody"), Is.EqualTo(playerRigidbody));

        GameObject noRigidbodyObject = new GameObject("NoRigidbody");
        objectsToDestroy.Add(noRigidbodyObject);

        InvokePrivate(menu, "CapturePausedPlayerVelocity", noRigidbodyObject);

        Assert.That(GetPrivateField<bool>(menu, "hasPausedPlayerVelocity"), Is.False);
        Assert.That(GetPrivateField<Rigidbody2D>(menu, "pausedPlayerRigidbody"), Is.Null);
    }

    [Test]
    public void SetMenuVisible_TogglesCanvasRaycasterAndMenuRoot()
    {
        OptionsMenu menu = CreateMenu();
        GameObject menuRoot = new GameObject("MenuRoot");
        menuRoot.transform.SetParent(menu.transform);
        objectsToDestroy.Add(menuRoot);

        Canvas canvas = menu.gameObject.AddComponent<Canvas>();
        GraphicRaycaster raycaster = menu.gameObject.AddComponent<GraphicRaycaster>();
        SetPrivateField(menu, "rootCanvas", canvas);
        SetPrivateField(menu, "graphicRaycaster", raycaster);
        SetPrivateField(menu, "menuRoot", menuRoot);

        InvokePrivate(menu, "SetMenuVisible", false);

        Assert.That(canvas.enabled, Is.False);
        Assert.That(raycaster.enabled, Is.False);
        Assert.That(menuRoot.activeSelf, Is.False);

        InvokePrivate(menu, "SetMenuVisible", true);

        Assert.That(canvas.enabled, Is.True);
        Assert.That(raycaster.enabled, Is.True);
        Assert.That(menuRoot.activeSelf, Is.True);
    }

    [Test]
    public void BindingHelpers_RecognizeKeyboardMouseGroupAndAllowedInputs()
    {
        Assert.That(InvokePrivateStatic<bool>("BindingContainsGroup", "Gamepad;Keyboard&Mouse", "Keyboard&Mouse"), Is.True);
        Assert.That(InvokePrivateStatic<bool>("BindingContainsGroup", "Gamepad", "Keyboard&Mouse"), Is.False);
        Assert.That(InvokePrivateStatic<bool>("IsAllowedRebindPath", "<Keyboard>/space", false), Is.True);
        Assert.That(InvokePrivateStatic<bool>("IsAllowedRebindPath", "<Mouse>/leftButton", false), Is.False);
        Assert.That(InvokePrivateStatic<bool>("IsAllowedRebindPath", "<Mouse>/leftButton", true), Is.True);
    }

    [Test]
    public void OptionsMenuButtonState_TracksKeyboardSelectionAndPointerHover()
    {
        EventSystem eventSystem = CreateEventSystem();
        OptionsMenuButtonState first = CreateOptionButtonState("First", out GameObject firstSelect, out GameObject firstNotSelect);
        OptionsMenuButtonState second = CreateOptionButtonState("Second", out GameObject secondSelect, out GameObject secondNotSelect);

        first.OnSelect(null);
        eventSystem.SetSelectedGameObject(first.gameObject);
        first.OnSelect(null);

        Assert.That(firstSelect.activeSelf, Is.True);
        Assert.That(firstNotSelect.activeSelf, Is.False);
        Assert.That(secondSelect.activeSelf, Is.False);
        Assert.That(secondNotSelect.activeSelf, Is.True);

        second.OnPointerEnter(null);

        Assert.That(firstSelect.activeSelf, Is.False);
        Assert.That(firstNotSelect.activeSelf, Is.True);
        Assert.That(secondSelect.activeSelf, Is.True);
        Assert.That(secondNotSelect.activeSelf, Is.False);

        second.OnPointerExit(null);
        OptionsMenuButtonState.ResetPointerVisualMode();

        Assert.That(firstSelect.activeSelf, Is.True);
        Assert.That(firstNotSelect.activeSelf, Is.False);
        Assert.That(secondSelect.activeSelf, Is.False);
        Assert.That(secondNotSelect.activeSelf, Is.True);
    }

    private OptionsMenu CreateMenu()
    {
        GameObject menuObject = new GameObject("OptionsMenu");
        objectsToDestroy.Add(menuObject);
        return menuObject.AddComponent<OptionsMenu>();
    }

    private Rigidbody2D CreateTaggedPlayer()
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.tag = "Player";
        objectsToDestroy.Add(playerObject);

        Rigidbody2D rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        rigidbody2D.gravityScale = 0f;
        return rigidbody2D;
    }

    private EventSystem CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        objectsToDestroy.Add(eventSystemObject);
        return eventSystemObject.AddComponent<EventSystem>();
    }

    private OptionsMenuButtonState CreateOptionButtonState(
        string objectName,
        out GameObject selectState,
        out GameObject notSelectState)
    {
        GameObject buttonObject = new GameObject(objectName);
        objectsToDestroy.Add(buttonObject);

        selectState = new GameObject("Select");
        selectState.transform.SetParent(buttonObject.transform);
        notSelectState = new GameObject("Not Select");
        notSelectState.transform.SetParent(buttonObject.transform);

        OptionsMenuButtonState state = buttonObject.AddComponent<OptionsMenuButtonState>();
        state.Configure(selectState.transform, notSelectState.transform);
        return state;
    }

    private static void InvokePrivate(OptionsMenu menu, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(OptionsMenu).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        method.Invoke(menu, arguments);
    }

    private static T InvokePrivateStatic<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(OptionsMenu).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        return (T)method.Invoke(null, arguments);
    }

    private static T GetPrivateField<T>(OptionsMenu menu, string fieldName)
    {
        FieldInfo field = typeof(OptionsMenu).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"{fieldName} must exist.");
        return (T)field.GetValue(menu);
    }

    private static void SetPrivateField(OptionsMenu menu, string fieldName, object value)
    {
        FieldInfo field = typeof(OptionsMenu).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"{fieldName} must exist.");
        field.SetValue(menu, value);
    }
}
