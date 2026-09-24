using System.Reflection;
using System;
using System.Linq;
using System.Threading;
using Metroidvania.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueArtworkTests
{
    [Test]
    public void DialogueText_UsesThirtyByTwoOrTwentyByOneWithoutShrinking()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/StoryEventCanvas.prefab");
        try
        {
            var view = root.GetComponentInChildren<DialogueView>(true);
            var serialized = new SerializedObject(view);
            ((GameObject)serialized.FindProperty("presentationRoot").objectReferenceValue).SetActive(true);
            var text = (TextMeshProUGUI)serialized.FindProperty("dialogueText").objectReferenceValue;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var effects = typeof(DialogueView).GetMethod("ApplyLineTextEffects", flags);
            var prepare = typeof(DialogueView).GetMethod("PrepareTypewriterText", flags);
            typeof(DialogueView).GetMethod("ApplySpeakerArtwork", flags).Invoke(view, new object[] { "イリス" });
            foreach (bool emphasized in new[] { false, true })
            {
                int columns = emphasized ? 20 : 30;
                int capacity = emphasized ? 20 : 60;
                foreach (int length in new[] { columns, columns + 1, capacity, capacity + 1 })
                {
                    effects.Invoke(view, new object[] { emphasized ? new[] { "size:1.5" } : Array.Empty<string>() });
                    prepare.Invoke(view, new object[] { "<color=#FFE832>" + new string('あ', length) + "</color>" });
                    text.maxVisibleCharacters = int.MaxValue;
                    text.ForceMeshUpdate(true);
                    var glyphs = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.character == 'あ').ToArray();
                    Assert.That(glyphs.Length, Is.EqualTo(length));
                    Assert.That(text.fontSize, Is.EqualTo(emphasized ? 57f : 38f));
                    Assert.That(text.enableAutoSizing, Is.False);
                    Assert.That(glyphs.Max(c => c.xAdvance), Is.LessThanOrEqualTo(text.rectTransform.rect.width),
                        "Thirty normal or twenty enlarged characters must fit inside the panel.");
                    Assert.That(text.textInfo.pageCount, Is.EqualTo((length + capacity - 1) / capacity));
                    foreach (var page in glyphs.GroupBy(c => c.pageNumber))
                    {
                        Assert.That(page.Select(c => c.lineNumber).Distinct().Count(), Is.LessThanOrEqualTo(emphasized ? 1 : 2));
                        foreach (var line in page.GroupBy(c => c.lineNumber))
                            Assert.That(line.Count(), Is.LessThanOrEqualTo(columns));
                    }
                }
            }
            effects.Invoke(view, new object[] { new[] { "shake" } });
            Assert.That(text.fontSize, Is.EqualTo(57f), "Shake always uses the enlarged text treatment.");
            effects.Invoke(view, new object[] { Array.Empty<string>() });
            prepare.Invoke(view, new object[] { new string('あ', 30) + "\nい" });
            Assert.That(text.text, Is.EqualTo(new string('あ', 30) + "\nい"), "An authored newline must not double-wrap.");

            void Set(string field, object value) => typeof(DialogueView).GetField(field, flags).SetValue(view, value);
            var state = typeof(DialogueView).GetField("_lineState", flags);
            using var cancellation = new CancellationTokenSource();
            Set("_currentLineCts", cancellation);
            Set("_currentDialoguePage", 1);
            Set("_dialoguePageCount", 2);
            state.SetValue(view, Enum.Parse(state.FieldType, "Typing"));
            Set("_lastAdvanceFrame", -1);
            view.OnContinueClicked();
            Assert.That(text.maxVisibleCharacters, Is.EqualTo(int.MaxValue));
            Assert.That(cancellation.IsCancellationRequested, Is.False);
            state.SetValue(view, Enum.Parse(state.FieldType, "WaitingForAdvance"));
            Set("_lastAdvanceFrame", -1);
            view.OnContinueClicked();
            Assert.That((bool)typeof(DialogueView).GetField("_pageAdvanceRequested", flags).GetValue(view), Is.True);
            Assert.That(cancellation.IsCancellationRequested, Is.False, "An intermediate page must not finish the Yarn line.");
            Set("_currentDialoguePage", 2);
            Set("_lastAdvanceFrame", -1);
            view.OnContinueClicked();
            Assert.That(cancellation.IsCancellationRequested, Is.True);
            Set("_currentLineCts", null);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void SharedPrefab_SwitchesArtworkAndKeepsUnknownNamesHidden()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/StoryEventCanvas.prefab");
        try
        {
            var view = root.GetComponentInChildren<DialogueView>(true);
            var serialized = new SerializedObject(view);
            var background = (Image)serialized.FindProperty("dialogueBackground").objectReferenceValue;
            var name = (Image)serialized.FindProperty("speakerNameArtwork").objectReferenceValue;
            var apply = typeof(DialogueView).GetMethod("ApplySpeakerArtwork", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(background, Is.Not.Null);
            Assert.That(name, Is.Not.Null);
            foreach (var field in new[] { "leftDialogueSprite", "rightDialogueSprite", "irisNameSprite", "noxLeftNameSprite", "noxRightNameSprite", "thanatosNameSprite" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

            apply.Invoke(view, new object[] { "イリス" });
            Assert.That(background.rectTransform.pivot.x, Is.EqualTo(1));
            Assert.That(name.sprite, Is.EqualTo(serialized.FindProperty("irisNameSprite").objectReferenceValue));
            apply.Invoke(view, new object[] { "ノクス" });
            Assert.That(background.rectTransform.pivot.x, Is.EqualTo(0));
            Assert.That(name.sprite, Is.EqualTo(serialized.FindProperty("noxLeftNameSprite").objectReferenceValue));

            // Scene-specific portrait placement must also switch the authored name strip.
            serialized.FindProperty("characterPortraits").GetArrayElementAtIndex(1).FindPropertyRelative("slot").enumValueIndex = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            apply.Invoke(view, new object[] { "ノクス" });
            Assert.That(background.rectTransform.pivot.x, Is.EqualTo(1));
            Assert.That(name.sprite, Is.EqualTo(serialized.FindProperty("noxRightNameSprite").objectReferenceValue));
            apply.Invoke(view, new object[] { "タナトス" });
            Assert.That(name.sprite, Is.EqualTo(serialized.FindProperty("thanatosNameSprite").objectReferenceValue));
            foreach (var speaker in new[] { "???", "" })
            {
                apply.Invoke(view, new object[] { speaker });
                Assert.That(name.gameObject.activeSelf, Is.False, speaker);
                Assert.That(name.sprite, Is.Null, speaker);
            }

            foreach (var field in new[] { "logButton", "skipButton", "skipConfirmYesButton", "skipConfirmNoButton" })
            {
                var button = (Button)serialized.FindProperty(field).objectReferenceValue;
                Assert.That(button.image.sprite, Is.Not.Null, field);
                Assert.That(button.onClick.GetPersistentEventCount(), Is.GreaterThan(0), field);
                Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False, field);
            }
            var marker = (GameObject)serialized.FindProperty("nextIndicator").objectReferenceValue;
            Assert.That(marker.GetComponent<Image>().sprite, Is.Not.Null);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
