using System.Collections.Generic;
using System.Reflection;
using Metroidvania.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ThanatosPortraitAnimationTests
{
    [Test]
    public void SharedPrefab_PlaysAllThanatosExpressionsAndPreservesConversationLayout()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/StoryEventCanvas.prefab");
        try
        {
            var view = root.GetComponentInChildren<DialogueView>(true);
            var serialized = new SerializedObject(view);
            var portrait = serialized.FindProperty("characterPortraits").GetArrayElementAtIndex(2);
            Assert.That(portrait.FindPropertyRelative("characterName").stringValue, Is.EqualTo("タナトス"));
            Assert.That(portrait.FindPropertyRelative("slot").enumValueIndex, Is.EqualTo((int)DialoguePortraitSlot.Left));
            var normal = (PortraitAnimationClip)portrait.FindPropertyRelative("portraitAnimation").objectReferenceValue;
            Assert.That(normal, Is.Not.Null);
            Assert.That(portrait.FindPropertyRelative("portraitSprite").objectReferenceValue, Is.SameAs(normal.FirstFrame));
            var clips = new Dictionary<string, PortraitAnimationClip>();
            Vector2? size = null;
            Vector2? pivot = null;
            foreach (var expression in new[] { "normal", "doubt", "angry", "relieved" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<PortraitAnimationClip>(
                    $"Assets/Data/Dialogue/PortraitAnimations/Thanatos_{expression}.asset");
                Assert.That(clip, Is.Not.Null, expression);
                clips.Add(expression, clip);
                Assert.That(clip.FrameCount, Is.EqualTo(240));
                Assert.That(clip.FramesPerSecond, Is.EqualTo(30f));
                var frames = new SerializedObject(clip).FindProperty("frames");
                for (int i = 0; i < frames.arraySize; i++)
                {
                    var frame = frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    Assert.That(frame, Is.Not.Null, $"{expression}/{i}");
                    size ??= frame.rect.size;
                    pivot ??= frame.pivot;
                    Assert.That(frame.rect.size, Is.EqualTo(size.Value));
                    Assert.That(frame.pivot, Is.EqualTo(pivot.Value));
                    Assert.That(Mathf.Max(frame.texture.width, frame.texture.height), Is.LessThanOrEqualTo(1024));
                    Assert.That(AssetDatabase.GetAssetPath(frame), Is.EqualTo(
                        $"Assets/Art/Sprites/Dialogue/Thanatos/{expression}/Thanatos_{expression}_{i:00000}.png"));
                }
                Assert.That(clip.GetFrame(8d), Is.SameAs(clip.FirstFrame));
                Assert.That(clip.GetFrame(7.99d), Is.SameAs(frames.GetArrayElementAtIndex(239).objectReferenceValue));
            }
            Assert.That(normal, Is.SameAs(clips["normal"]));

            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var apply = typeof(DialogueView).GetMethod("ApplySpeaker", flags);
            var advance = typeof(DialogueView).GetMethod("AdvancePortraitAnimations", flags);
            var left = (Image)serialized.FindProperty("leftPortraitImage").objectReferenceValue;
            var right = (Image)serialized.FindProperty("rightPortraitImage").objectReferenceValue;
            void Speak(string speaker, string face) => apply.Invoke(view, new object[] { speaker, face });
            void Advance(float seconds) => advance.Invoke(view, new object[] { seconds });
            view.OnDialogueStartedAsync();
            Speak("イリス", "normal");
            Speak("ノクス", "normal");
            var nox = (Image)typeof(DialogueView).GetField("_noxPortraitImage", flags).GetValue(view);
            var aliases = new Dictionary<string, string> {
                { "normal", "normal" }, { "default", "normal" }, { "", "normal" }, { "通常", "normal" },
                { "doubt", "doubt" }, { "疑う", "doubt" }, { "angry", "angry" }, { "怒り", "angry" },
                { "relieved", "relieved" }, { "安心", "relieved" }
            };
            foreach (var alias in aliases)
            {
                // Switch to a different clip first so each alias must start at frame zero.
                Speak("タナトス", alias.Value == "normal" ? "angry" : "normal");
                Speak("タナトス", alias.Key);
                var clip = clips[alias.Value];
                Assert.That(left.sprite, Is.SameAs(clip.FirstFrame), alias.Key);
                Assert.That(left.color, Is.EqualTo(Color.white));
                Advance(.15f);
                Assert.That(left.sprite, Is.SameAs(clip.GetFrame(.15d)));
                Speak("タナトス", alias.Key);
                Assert.That(left.sprite, Is.SameAs(clip.GetFrame(.15d)), "Repeated lines must continue playing.");
                Speak("イリス", "normal");
                Advance(.1f);
                Assert.That(left.sprite, Is.SameAs(clip.GetFrame(.25d)), "Inactive portraits must keep animating.");
                Assert.That(left.color, Is.EqualTo(new Color(.32f, .32f, .32f, 1f)));
                Assert.That(right.gameObject.activeSelf, Is.True);
                Assert.That(nox.gameObject.activeSelf, Is.True);
                Assert.That(nox.rectTransform.anchorMin.x, Is.EqualTo(1));
            }
            var lastFrame = left.sprite;
            view.OnDialogueCompleteAsync();
            Advance(.2f);
            Assert.That(left.sprite, Is.SameAs(lastFrame));
            Assert.That(left.gameObject.activeSelf, Is.False);
            view.OnDialogueStartedAsync();
            Speak("タナトス", "default");
            Assert.That(left.sprite, Is.SameAs(normal.FirstFrame));
            view.OnDialogueCompleteAsync();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
