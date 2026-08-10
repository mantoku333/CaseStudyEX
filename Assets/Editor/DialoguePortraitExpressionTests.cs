using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Metroidvania.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DialoguePortraitExpressionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

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
    public void FaceTag_SelectsExpressionName()
    {
        string expressionName = GetExpressionName(new[] { "line:example", "face:smile" });

        Assert.That(expressionName, Is.EqualTo("smile"));
    }

    [Test]
    public void MissingOrEmptyFaceTag_SelectsDefault()
    {
        Assert.That(GetExpressionName(null), Is.EqualTo("default"));
        Assert.That(GetExpressionName(new[] { "line:example" }), Is.EqualTo("default"));
        Assert.That(GetExpressionName(new[] { "face:" }), Is.EqualTo("default"));
    }

    [Test]
    public void FaceTag_IsCaseInsensitiveAndAcceptsLeadingHash()
    {
        string expressionName = GetExpressionName(new[] { "  #FACE:Angry  " });

        Assert.That(expressionName, Is.EqualTo("Angry"));
    }

    [Test]
    public void ConfiguredExpression_OverridesDefaultPortrait()
    {
        DialogueView view = CreateView();
        Sprite defaultPortrait = CreateSprite();
        Sprite smilePortrait = CreateSprite();
        var portrait = new CharacterPortrait
        {
            characterName = "イリス",
            portraitSprite = defaultPortrait,
            slot = DialoguePortraitSlot.Right,
            expressionPortraits = new[]
            {
                new PortraitExpression
                {
                    expressionName = "smile",
                    portraitSprite = smilePortrait
                }
            }
        };

        Assert.That(FindExpressionSprite(view, portrait, "smile"), Is.SameAs(smilePortrait));
        Assert.That(FindExpressionSprite(view, portrait, "default"), Is.SameAs(defaultPortrait));
    }

    [Test]
    public void UnknownExpression_FallsBackToDefaultPortrait()
    {
        DialogueView view = CreateView();
        Sprite defaultPortrait = CreateSprite();
        var portrait = new CharacterPortrait
        {
            characterName = "イリス",
            portraitSprite = defaultPortrait,
            slot = DialoguePortraitSlot.Right,
            expressionPortraits = new PortraitExpression[0]
        };

        LogAssert.Expect(LogType.Warning, new Regex("Portrait expression was not found"));

        Assert.That(FindExpressionSprite(view, portrait, "unknown"), Is.SameAs(defaultPortrait));
    }

    private DialogueView CreateView()
    {
        var gameObject = new GameObject("DialoguePortraitExpressionTest");
        gameObject.SetActive(false);
        objectsToDestroy.Add(gameObject);
        return gameObject.AddComponent<DialogueView>();
    }

    private Sprite CreateSprite()
    {
        var texture = new Texture2D(2, 2);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
        objectsToDestroy.Add(texture);
        objectsToDestroy.Add(sprite);
        return sprite;
    }

    private static string GetExpressionName(string[] metadata)
    {
        MethodInfo method = typeof(DialogueView).GetMethod("GetExpressionName", PrivateStatic);
        Assert.That(method, Is.Not.Null);
        return (string)method.Invoke(null, new object[] { metadata });
    }

    private static Sprite FindExpressionSprite(
        DialogueView view,
        CharacterPortrait portrait,
        string expressionName)
    {
        MethodInfo method = typeof(DialogueView).GetMethod("FindExpressionSprite", PrivateInstance);
        Assert.That(method, Is.Not.Null);
        return (Sprite)method.Invoke(view, new object[] { portrait, expressionName });
    }
}
