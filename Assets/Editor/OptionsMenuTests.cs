using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

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
    public void ApplyAudioSettings_SeVolumeZeroMutesSeAndRaisingVolumeUnmutesIt()
    {
        const string seVolumeKey = "Options.SeVolume";
        bool hadSavedValue = PlayerPrefs.HasKey(seVolumeKey);
        float savedValue = PlayerPrefs.GetFloat(seVolumeKey, 1f);

        try
        {
            OptionsMenu menu = CreateMenu();
            GameObject sourceObject = new GameObject("TestSeSource");
            objectsToDestroy.Add(sourceObject);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.loop = false;
            source.volume = 0.75f;

            PlayerPrefs.SetFloat(seVolumeKey, 0f);
            InvokePrivate(menu, "ApplyAudioSettingsToScene", true);

            Assert.That(source.mute, Is.True);

            PlayerPrefs.SetFloat(seVolumeKey, 0.5f);
            InvokePrivate(menu, "ApplyAudioSettingsToScene", true);

            Assert.That(source.mute, Is.False);
        }
        finally
        {
            if (hadSavedValue)
            {
                PlayerPrefs.SetFloat(seVolumeKey, savedValue);
            }
            else
            {
                PlayerPrefs.DeleteKey(seVolumeKey);
            }
        }
    }

    [Test]
    public void ApplyAudioSettings_MasterVolumeScalesBgmAndSe()
    {
        const string bgmVolumeKey = "Options.BgmVolume";
        const string seVolumeKey = "Options.SeVolume";
        const string masterVolumeKey = "Options.SystemVolume";
        bool hadBgmValue = PlayerPrefs.HasKey(bgmVolumeKey);
        bool hadSeValue = PlayerPrefs.HasKey(seVolumeKey);
        bool hadMasterValue = PlayerPrefs.HasKey(masterVolumeKey);
        float savedBgmValue = PlayerPrefs.GetFloat(bgmVolumeKey, 1f);
        float savedSeValue = PlayerPrefs.GetFloat(seVolumeKey, 1f);
        float savedMasterValue = PlayerPrefs.GetFloat(masterVolumeKey, 1f);

        try
        {
            OptionsMenu menu = CreateMenu();

            GameObject bgmObject = new GameObject("TestBgmSource");
            objectsToDestroy.Add(bgmObject);
            AudioSource bgmSource = bgmObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = 0.8f;

            GameObject seObject = new GameObject("TestSeSource");
            objectsToDestroy.Add(seObject);
            AudioSource seSource = seObject.AddComponent<AudioSource>();
            seSource.loop = false;
            seSource.volume = 0.6f;

            PlayerPrefs.SetFloat(bgmVolumeKey, 0.5f);
            PlayerPrefs.SetFloat(seVolumeKey, 0.25f);
            PlayerPrefs.SetFloat(masterVolumeKey, 0.5f);
            InvokePrivate(menu, "ApplyAudioSettingsToScene", true);

            Assert.That(bgmSource.volume, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(seSource.volume, Is.EqualTo(0.075f).Within(0.001f));
        }
        finally
        {
            RestoreVolumePref(bgmVolumeKey, hadBgmValue, savedBgmValue);
            RestoreVolumePref(seVolumeKey, hadSeValue, savedSeValue);
            RestoreVolumePref(masterVolumeKey, hadMasterValue, savedMasterValue);
        }
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

    private static void InvokePrivate(OptionsMenu menu, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(OptionsMenu).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        method.Invoke(menu, arguments);
    }

    private static void RestoreVolumePref(string key, bool hadSavedValue, float savedValue)
    {
        if (hadSavedValue)
        {
            PlayerPrefs.SetFloat(key, savedValue);
        }
        else
        {
            PlayerPrefs.DeleteKey(key);
        }
    }
}
