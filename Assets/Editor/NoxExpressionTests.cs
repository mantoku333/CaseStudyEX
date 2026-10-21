using System.Linq;
using Metroidvania.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class NoxExpressionTests : PortraitAnimationTestFixture
{
    [Test]
    public void Overlay_FollowsClockAndClearsForStaticFacesAndConversationEnd()
    {
        var mark = Clip(Frame(), Frame(), Frame());
        var normal = Clip(Frame(), Frame(), Frame());
        Set(normal, "overlayAnimation", mark);
        var view = View(normal, Clip(Frame()), out var portrait, out _);
        Call(view, "ApplySpeaker", "イリス", "normal");
        var overlay = portrait.transform.Find("ExpressionOverlay").GetComponent<Image>();
        Assert.That(overlay.sprite, Is.SameAs(mark.FirstFrame));
        Assert.That(overlay.raycastTarget, Is.False);
        Call(view, "AdvancePortraitAnimations", .15f);
        Assert.That(overlay.sprite, Is.SameAs(mark.GetFrame(.15d)));
        Call(view, "ApplySpeaker", "イリス", "normal");
        Assert.That(overlay.sprite, Is.SameAs(mark.GetFrame(.15d)));
        Call(view, "ApplySpeaker", "イリス", "smile");
        Assert.That(overlay.gameObject.activeSelf, Is.False);
        Call(view, "ApplySpeaker", "イリス", "normal");
        Assert.That(overlay.sprite, Is.SameAs(mark.FirstFrame));
        view.OnDialogueCompleteAsync();
        Assert.That(overlay.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void SharedPrefab_NoxExpressionsHaveAllFramesAndFollowSideChanges()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/StoryEventCanvas.prefab");
        try
        {
            var view = root.GetComponentInChildren<DialogueView>(true);
            var portraits = new SerializedObject(view).FindProperty("characterPortraits");
            var nox = Enumerable.Range(0, portraits.arraySize).Select(portraits.GetArrayElementAtIndex)
                .Single(p => p.FindPropertyRelative("characterName").stringValue == "ノクス");
            var normal = (PortraitAnimationClip)nox.FindPropertyRelative("portraitAnimation").objectReferenceValue;
            view.OnDialogueStartedAsync();
            foreach (var expression in new[] { "angry", "happy" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<PortraitAnimationClip>(
                    "Assets/Data/Dialogue/PortraitAnimations/Nox_" + expression + ".asset");
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.OverlayAnimation, Is.Not.Null);
                foreach (var animation in new[] { clip, clip.OverlayAnimation })
                {
                    Assert.That(animation.FrameCount, Is.EqualTo(120));
                    Assert.That(animation.FramesPerSecond, Is.EqualTo(30));
                    var frames = new SerializedObject(animation).FindProperty("frames");
                    for (int i = 0; i < 120; i++)
                        Assert.That(frames.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, animation.name + "/" + i);
                    Assert.That(animation.GetFrame(4d), Is.SameAs(animation.FirstFrame));
                }
                Assert.That(clip.FirstFrame.rect.size, Is.EqualTo(normal.FirstFrame.rect.size));
                Call(view, "ApplySpeaker", "ノクス", expression);
                var image = (Image)typeof(DialogueView).GetField("_noxPortraitImage", Private).GetValue(view);
                var overlay = image.transform.Find("ExpressionOverlay").GetComponent<Image>();
                Assert.That(image.sprite, Is.SameAs(clip.FirstFrame));
                Call(view, "AdvancePortraitAnimations", .5f);
                Assert.That(overlay.sprite, Is.SameAs(clip.OverlayAnimation.GetFrame(.5d)));
                Call(view, "ApplySpeaker", "ノクス", expression);
                Assert.That(image.sprite, Is.SameAs(clip.GetFrame(.5d)));
                Call(view, "ApplySpeaker", "タナトス", "default");
                Call(view, "AdvancePortraitAnimations", 0f);
                Assert.That(overlay.transform.parent, Is.SameAs(image.transform));
                Assert.That(overlay.color, Is.EqualTo(image.color));
                Assert.That(overlay.rectTransform.anchoredPosition.x, Is.LessThan(0), "Right-side marks must face into the screen.");
                Call(view, "ApplyNoxPlacementMetadata", new object[] { new[] { "nox:left" } });
                Assert.That(overlay.rectTransform.anchoredPosition.x, Is.GreaterThan(0));
                Call(view, "ApplySpeaker", "ノクス", "normal");
                Assert.That(overlay.gameObject.activeSelf, Is.False);
            }
            view.OnDialogueCompleteAsync();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
