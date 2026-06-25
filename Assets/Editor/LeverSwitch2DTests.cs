using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("Gameplay")]
public sealed class LeverSwitch2DTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

    [TearDown]
    public void TearDown()
    {
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
    public void Awake_WhenShutterClosed_AppliesClosedSprite()
    {
        Sprite closedSprite = CreateSprite("Closed");
        Sprite openedSprite = CreateSprite("Opened");
        CreateShutterWall(false);
        CreateLever(closedSprite, openedSprite, null, out SpriteRenderer renderer);

        Assert.That(renderer.sprite, Is.SameAs(closedSprite));
    }

    [Test]
    public void ActivateFromAttack_WhenOpenStarts_AppliesOpenedSprite()
    {
        Sprite closedSprite = CreateSprite("Closed");
        Sprite openedSprite = CreateSprite("Opened");
        CreateShutterWall(false);
        LeverSwitch2D lever = CreateLever(closedSprite, openedSprite, null, out SpriteRenderer renderer);

        lever.ActivateFromAttack();

        Assert.That(renderer.sprite, Is.SameAs(openedSprite));
    }

    [Test]
    public void Awake_WhenShutterStartsOpened_AppliesOpenedSprite()
    {
        Sprite closedSprite = CreateSprite("Closed");
        Sprite openedSprite = CreateSprite("Opened");
        CreateShutterWall(true);
        CreateLever(closedSprite, openedSprite, null, out SpriteRenderer renderer);

        Assert.That(renderer.sprite, Is.SameAs(openedSprite));
    }

    [Test]
    public void ActivateFromAttack_WhenShutterMissing_KeepsClosedSprite()
    {
        Sprite closedSprite = CreateSprite("Closed");
        Sprite openedSprite = CreateSprite("Opened");
        LeverSwitch2D lever = CreateLever(closedSprite, openedSprite, null, out SpriteRenderer renderer);

        LogAssert.Expect(LogType.Warning, new Regex("ShutterWallBlockRise"));
        lever.ActivateFromAttack();

        Assert.That(renderer.sprite, Is.SameAs(closedSprite));
    }

    [Test]
    public void ActivateFromAttack_WhenSpritesUnset_KeepsRendererSprite()
    {
        Sprite legacySprite = CreateSprite("Legacy");
        CreateShutterWall(false);
        LeverSwitch2D lever = CreateLever(null, null, legacySprite, out SpriteRenderer renderer);

        lever.ActivateFromAttack();

        Assert.That(renderer.sprite, Is.SameAs(legacySprite));
    }

    private LeverSwitch2D CreateLever(
        Sprite closedSprite,
        Sprite openedSprite,
        Sprite initialSprite,
        out SpriteRenderer renderer)
    {
        GameObject leverObject = new GameObject("Lever");
        objectsToDestroy.Add(leverObject);

        renderer = leverObject.AddComponent<SpriteRenderer>();
        renderer.sprite = initialSprite;

        LeverSwitch2D lever = leverObject.AddComponent<LeverSwitch2D>();
        SetPrivateField(lever, "leverRenderer", renderer);
        SetPrivateField(lever, "closedSprite", closedSprite);
        SetPrivateField(lever, "openedSprite", openedSprite);
        SetPrivateField(lever, "initialized", false);
        InvokePrivate(lever, "Awake");

        return lever;
    }

    private ShutterWallBlockRise CreateShutterWall(bool startsOpened)
    {
        GameObject shutterObject = new GameObject("ShutterWall");
        objectsToDestroy.Add(shutterObject);

        GameObject blockObject = new GameObject("Block");
        objectsToDestroy.Add(blockObject);
        blockObject.transform.SetParent(shutterObject.transform);

        ShutterWallBlockRise shutterWall = shutterObject.AddComponent<ShutterWallBlockRise>();
        SetPrivateField(shutterWall, "startsOpened", startsOpened);
        SetPrivateField(shutterWall, "initialized", false);
        InvokePrivate(shutterWall, "Awake");
        return shutterWall;
    }

    private Sprite CreateSprite(string name)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.name = name + "Texture";
        objectsToDestroy.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
        sprite.name = name;
        objectsToDestroy.Add(sprite);
        return sprite;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}");
        method.Invoke(target, null);
    }
}
