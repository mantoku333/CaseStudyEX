using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Metroidvania.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public abstract class PortraitAnimationTestFixture
{
    protected readonly List<Object> created = new();
    protected const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TearDown]
    public void Cleanup()
    {
        foreach (var o in Enumerable.Reverse(created)) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    protected Sprite Frame()
    {
        var texture = new Texture2D(2, 2); created.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f); created.Add(sprite);
        return sprite;
    }

    protected PortraitAnimationClip Clip(params Sprite[] frames)
    {
        var clip = ScriptableObject.CreateInstance<PortraitAnimationClip>(); created.Add(clip);
        Set(clip, "frames", frames); Set(clip, "framesPerSecond", 10f);
        return clip;
    }

    protected static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    protected static void Call(DialogueView view, string method, params object[] args) => typeof(DialogueView).GetMethod(method, Private).Invoke(view, args);

    protected DialogueView View(PortraitAnimationClip normal, PortraitAnimationClip sad, out Image image, out Sprite smile)
    {
        var go = new GameObject("PortraitAnimationTest"); created.Add(go); go.SetActive(false);
        var view = go.AddComponent<DialogueView>();
        var portrait = new GameObject("RightPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        portrait.transform.SetParent(go.transform, false); image = portrait.GetComponent<Image>();
        Set(view, "rightPortraitImage", image); smile = Frame();
        Set(view, "characterPortraits", new List<CharacterPortrait> { new CharacterPortrait {
            characterName = "イリス", slot = DialoguePortraitSlot.Right, portraitAnimation = normal,
            expressionPortraits = new[] {
                new PortraitExpression { expressionName = "sad", portraitAnimation = sad },
                new PortraitExpression { expressionName = "smile", portraitSprite = smile }
            }
        }});
        go.SetActive(true);
        view.OnDialogueStartedAsync();
        return view;
    }
}

public sealed class DialoguePortraitAnimationTests : PortraitAnimationTestFixture
{
    [Test]
    public void Clip_LoopsSkipsDroppedFramesAndCanHoldLastFrame()
    {
        var a = Frame(); var b = Frame(); var c = Frame(); var clip = Clip(a, b, c);
        Assert.That(clip.GetFrame(.15d), Is.SameAs(b));
        Assert.That(clip.GetFrame(.35d), Is.SameAs(a));
        Assert.That(clip.GetFrame(1000.15d), Is.SameAs(c));
        Set(clip, "loop", false);
        Assert.That(clip.GetFrame(1000d), Is.SameAs(c));
        Assert.That(clip.GetFrame(-10d), Is.SameAs(a));
    }

    [Test]
    public void EmptyOrMissingFrames_DoNotHideAnExistingPortrait()
    {
        Assert.That(Clip().FirstFrame, Is.Null);
        var frame = Frame(); var clip = Clip(null, frame);
        Assert.That(clip.FirstFrame, Is.SameAs(frame));
    }

    [Test]
    public void ExpressionChanges_RestartButRepeatedLinesKeepPlayingAndStaticFacesStop()
    {
        var normal = Clip(Frame(), Frame(), Frame()); var sad = Clip(Frame(), Frame());
        var view = View(normal, sad, out var image, out var smile);
        Call(view, "ApplySpeaker", "イリス", "normal");
        Call(view, "AdvancePortraitAnimations", .15f);
        Assert.That(image.sprite, Is.SameAs(normal.GetFrame(.15d)));
        Call(view, "ApplySpeaker", "イリス", "default");
        Assert.That(image.sprite, Is.SameAs(normal.GetFrame(.15d)), "Another normal line must not restart the loop.");
        Call(view, "ApplySpeaker", "イリス", "sad");
        Assert.That(image.sprite, Is.SameAs(sad.FirstFrame));
        Call(view, "ApplySpeaker", "イリス", "smile");
        Call(view, "AdvancePortraitAnimations", .25f);
        Assert.That(image.sprite, Is.SameAs(smile));
        Call(view, "ApplySpeaker", "イリス", "normal");
        view.OnDialogueCompleteAsync();
        Call(view, "AdvancePortraitAnimations", .15f);
        Assert.That(image.gameObject.activeSelf, Is.False);
        Assert.That(image.sprite, Is.SameAs(normal.FirstFrame));
        view.OnDialogueStartedAsync();
        Call(view, "ApplySpeaker", "イリス", "normal");
        Assert.That(image.sprite, Is.SameAs(normal.FirstFrame));
    }

    [Test]
    public void LeftAndRightPortraits_KeepIndependentAnimationClocks()
    {
        var normal = Clip(Frame(), Frame(), Frame()); var other = Clip(Frame(), Frame(), Frame());
        var view = View(normal, normal, out var right, out _);
        var go = new GameObject("LeftPortrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(view.transform, false);
        var left = go.GetComponent<Image>(); Set(view, "leftPortraitImage", left);
        var portraits = (List<CharacterPortrait>)typeof(DialogueView).GetField("characterPortraits", Private).GetValue(view);
        portraits.Add(new CharacterPortrait { characterName = "ノクス", slot = DialoguePortraitSlot.Left, portraitAnimation = other });
        Call(view, "ApplySpeaker", "イリス", "normal");
        Call(view, "AdvancePortraitAnimations", .15f);
        Call(view, "ApplySpeaker", "ノクス", "normal");
        Assert.That(right.sprite, Is.SameAs(normal.GetFrame(.15d)));
        var nox = (Image)typeof(DialogueView).GetField("_noxPortraitImage", Private).GetValue(view);
        Assert.That(nox.sprite, Is.SameAs(other.FirstFrame));
        Call(view, "AdvancePortraitAnimations", .10f);
        Assert.That(right.sprite, Is.SameAs(normal.GetFrame(.25d)));
        Assert.That(nox.sprite, Is.SameAs(other.GetFrame(.10d)));
    }

    [Test]
    public void SharedPrefab_HasCompleteClipsWithStableSpriteBounds()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/StoryEventCanvas.prefab");
        try
        {
            var view = root.GetComponentInChildren<DialogueView>(true);
            var serialized = new SerializedObject(view);
            var iris = serialized.FindProperty("characterPortraits").GetArrayElementAtIndex(0);
            var normal = (PortraitAnimationClip)iris.FindPropertyRelative("portraitAnimation").objectReferenceValue;
            var expressions = iris.FindPropertyRelative("expressionPortraits");
            var clips = new Dictionary<string, PortraitAnimationClip> { { "normal", normal } };
            for (int i = 0; i < expressions.arraySize; i++)
            {
                var expression = expressions.GetArrayElementAtIndex(i);
                clips.Add(expression.FindPropertyRelative("expressionName").stringValue,
                    (PortraitAnimationClip)expression.FindPropertyRelative("portraitAnimation").objectReferenceValue);
            }
            Assert.That(clips.Keys, Is.EquivalentTo(new[] {
                "normal", "soft_smile", "sad", "surprised", "disgust", "smile", "assertive", "troubled"
            }));
            Assert.That(clips["soft_smile"], Is.SameAs(normal), "Keep the existing soft-smile placeholder.");
            var expected = new Dictionary<string, (string asset, int frames)> {
                { "normal", ("Iris_normal", 240) }, { "sad", ("Iris_sad", 120) },
                { "smile", ("Iris_smile", 240) }, { "assertive", ("Iris_resolve", 120) },
                { "surprised", ("Iris_surprised", 120) }, { "disgust", ("Iris_disgust", 120) },
                { "troubled", ("Iris_troubled", 120) }
            };
            var image = (Image)serialized.FindProperty("rightPortraitImage").objectReferenceValue;
            Vector2? expectedSize = null;
            Vector2? expectedPivot = null;
            view.OnDialogueStartedAsync();
            foreach (var entry in expected)
            {
                var clip = clips[entry.Key];
                Assert.That(clip, Is.Not.Null, entry.Key);
                Assert.That(AssetDatabase.GetAssetPath(clip),
                    Is.EqualTo($"Assets/Data/Dialogue/PortraitAnimations/{entry.Value.asset}.asset"));
                Assert.That(clip.FrameCount, Is.EqualTo(entry.Value.frames));
                Assert.That(clip.FramesPerSecond, Is.EqualTo(30f));
                var frames = new SerializedObject(clip).FindProperty("frames");
                for (int i = 0; i < frames.arraySize; i++)
                {
                    var frame = frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    Assert.That(frame, Is.Not.Null, $"{clip.name}: frame {i}");
                    expectedSize ??= frame.rect.size;
                    expectedPivot ??= frame.pivot;
                    Assert.That(frame.rect.size, Is.EqualTo(expectedSize.Value), $"Unstable bounds: {clip.name}/{i}");
                    Assert.That(frame.pivot, Is.EqualTo(expectedPivot.Value), $"Unstable pivot: {clip.name}/{i}");
                    Assert.That(frame.texture.width, Is.LessThanOrEqualTo(1024));
                    Assert.That(AssetDatabase.GetAssetPath(frame),
                        Is.EqualTo($"Assets/Art/temp/{entry.Value.asset}/イリス立ち絵ver01_{i:00000}.png"));
                }
                Assert.That(clip.GetFrame(clip.FrameCount / 30d), Is.SameAs(clip.FirstFrame));
                Call(view, "ApplySpeaker", "イリス", entry.Key);
                Assert.That(image.sprite, Is.SameAs(clip.FirstFrame), entry.Key);
                Call(view, "AdvancePortraitAnimations", .15f);
                Assert.That(image.sprite, Is.SameAs(clip.GetFrame(.15d)), entry.Key);
                Call(view, "ApplySpeaker", "イリス", entry.Key);
                Assert.That(image.sprite, Is.SameAs(clip.GetFrame(.15d)), "Repeated lines must keep playing.");
            }
            view.OnDialogueCompleteAsync();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

public sealed class DialoguePortraitAnimationRuntimeTests : PortraitAnimationTestFixture
{
    [UnitySetUp]
    public IEnumerator EnterRuntime()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeaveRuntime()
    {
        Cleanup();
        Time.timeScale = 1f;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LateUpdate_AnimatesDuringGameplayPauseAndStopsOnCompletion()
    {
        var clip = Clip(Frame(), Frame(), Frame());
        var view = View(clip, clip, out var image, out _);
        Time.timeScale = 0f;
        Call(view, "ApplySpeaker", "イリス", "normal");
        var first = image.sprite;
        float timeout = Time.realtimeSinceStartup + 2f;
        while (image.sprite == first && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(Time.deltaTime, Is.Zero);
        Assert.That(image.sprite, Is.Not.SameAs(first), "The real LateUpdate must advance using unscaled time.");
        view.gameObject.SetActive(false);
        view.gameObject.SetActive(true);
        Call(view, "ApplySpeaker", "イリス", "normal");
        Assert.That(image.sprite, Is.SameAs(clip.FirstFrame), "Disabling the view must reset playback.");
        view.OnDialogueCompleteAsync();
        var stopped = image.sprite;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(image.sprite, Is.SameAs(stopped));
        Assert.That(image.gameObject.activeSelf, Is.False);
    }
}
