using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class UmbrellaParryAbilityGateTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();
    private List<GameProgressFlags.GameProgressFlagSnapshot> progressFlagSnapshot;

    [SetUp]
    public void SetUp()
    {
        progressFlagSnapshot = GameProgressFlags.GetSnapshot();
    }

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

        GameProgressFlags.ClearAll();
        if (progressFlagSnapshot != null)
        {
            for (int i = 0; i < progressFlagSnapshot.Count; i++)
            {
                GameProgressFlags.Set(progressFlagSnapshot[i].Key, progressFlagSnapshot[i].Value);
            }
        }
    }

    [Test]
    public void ParryAbilityLocked_DirectParryCallDoesNotEnterParryOrPlayEffect()
    {
        UmbrellaParryController parryController = CreateParryController(canParry: false);

        parryController.Parry(Vector2.right);
        parryController.PlayParrySuccessEffect(Vector2.right);
        parryController.PlayJustParrySuccessEffect(Vector2.right);

        Assert.That(parryController.IsParrying(), Is.False);
        Assert.That(parryController.transform.Find("ParrySuccessEffect"), Is.Null);
        Assert.That(parryController.transform.Find("JustParrySuccessEffect"), Is.Null);
    }

    [Test]
    public void ParryAbilityUnlocked_DirectEffectCallCanPlayEffect()
    {
        UmbrellaParryController parryController = CreateParryController(canParry: true);

        parryController.PlayParrySuccessEffect(Vector2.right);

        Assert.That(parryController.transform.Find("ParrySuccessEffect"), Is.Not.Null);
    }

    private UmbrellaParryController CreateParryController(bool canParry)
    {
        GameObject playerObject = CreateObject("Player");
        PlayerAbilityController abilityController = playerObject.AddComponent<PlayerAbilityController>();
        abilityController.SetCanParry(canParry);

        GameObject viewObject = CreateObject("PlayerView");
        viewObject.transform.SetParent(playerObject.transform);
        SpriteRenderer playerRenderer = viewObject.AddComponent<SpriteRenderer>();

        GameObject parryObject = CreateObject("UmbrellaParry");
        parryObject.transform.SetParent(playerObject.transform);
        UmbrellaParryController parryController = parryObject.AddComponent<UmbrellaParryController>();

        Texture2D effectTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        effectTexture.SetPixel(0, 0, Color.white);
        effectTexture.Apply();
        objectsToDestroy.Add(effectTexture);

        SetPrivateField(parryController, "playerSprite", playerRenderer);
        SetPrivateField(parryController, "parryEffectSpriteSheet", effectTexture);
        SetPrivateField(parryController, "justParryEffectSpriteSheet", effectTexture);
        SetPrivateField(parryController, "parryEffectFrameColumns", 1);
        SetPrivateField(parryController, "parryEffectFrameRows", 1);
        SetPrivateField(parryController, "parryEffectFrameCount", 1);
        SetPrivateField(parryController, "justParryEffectFrameColumns", 1);
        SetPrivateField(parryController, "justParryEffectFrameRows", 1);
        SetPrivateField(parryController, "justParryEffectFrameCount", 1);

        return parryController;
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName} on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
