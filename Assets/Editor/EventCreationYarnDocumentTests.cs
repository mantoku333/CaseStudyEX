using System.Linq;
using NUnit.Framework;

namespace CaseStudy.EditorTools
{
    public sealed class EventCreationYarnDocumentTests
    {
        [Test]
        public void ParseAndSerialize_WithoutChanges_PreservesSource()
        {
            const string source =
                "title: Event01\r\n" +
                "tags: test\r\n" +
                "---\r\n" +
                "イリス: こんにちは #face:smile\r\n" +
                "<<wait 1>>\r\n" +
                "===\r\n";

            EventCreationYarnDocument document = EventCreationYarnDocument.Parse(source);

            Assert.That(document.Nodes.Count, Is.EqualTo(1));
            Assert.That(document.ToText(), Is.EqualTo(source));
        }

        [Test]
        public void DialogueLine_ParsesSupportedPresentationSettings()
        {
            const string source =
                "title: Event01\n---\n" +
                "イリス: テスト #face:soft_smile #illustration:sample #shake #size:1.5\n" +
                "===\n";

            EventCreationYarnDocument document = EventCreationYarnDocument.Parse(source);
            EventCreationDialogueLine line = document.GetDialogueLines(0).Single();

            Assert.That(line.Speaker, Is.EqualTo("イリス"));
            Assert.That(line.Text, Is.EqualTo("テスト"));
            Assert.That(line.Face, Is.EqualTo("soft_smile"));
            Assert.That(line.Illustration, Is.EqualTo("sample"));
            Assert.That(line.Shake, Is.True);
            Assert.That(line.HasFontScale, Is.True);
            Assert.That(line.FontScale, Is.EqualTo(1.5f));
        }

        [Test]
        public void UpdateDialogueLine_RebuildsSettingsWithoutExposingThemInText()
        {
            const string source =
                "title: Event01\n---\n" +
                "イリス: 元の文 #custom:value #face:sad\n" +
                "===\n";
            EventCreationYarnDocument document = EventCreationYarnDocument.Parse(source);
            EventCreationDialogueLine line = document.GetDialogueLines(0).Single();

            line.Text = "変更後";
            line.Face = "smile";
            line.Shake = true;
            document.UpdateDialogueLine(line);

            Assert.That(
                document.ToText(),
                Does.Contain("イリス: 変更後 #custom:value #face:smile #shake"));
            Assert.That(document.ToText(), Does.Not.Contain("#face:sad"));
        }

        [Test]
        public void Commands_AreProtectedAndBlockCrossingMove()
        {
            const string source =
                "title: Event01\n---\n" +
                "イリス: 前\n" +
                "<<wait 1>>\n" +
                "イリス: 後\n" +
                "===\n";
            EventCreationYarnDocument document = EventCreationYarnDocument.Parse(source);
            EventCreationDialogueLine first = document.GetDialogueLines(0).First();

            Assert.That(document.CountProtectedBodyLines(0), Is.EqualTo(1));
            Assert.That(document.CanMoveDialogueLine(0, first.SourceLineIndex, 1), Is.False);
            Assert.That(document.ToText(), Does.Contain("<<wait 1>>"));
        }

        [Test]
        public void AppendNode_CreatesEditableValidNode()
        {
            EventCreationYarnDocument document = EventCreationYarnDocument.Parse(
                "title: Event01\n---\n===\n");

            int addedIndex = document.AppendNode("Event02");
            int sourceLine = document.InsertDialogueLine(addedIndex, -1);

            Assert.That(addedIndex, Is.EqualTo(1));
            Assert.That(sourceLine, Is.GreaterThan(0));
            Assert.That(document.Nodes.Select(node => node.Title),
                Is.EqualTo(new[] { "Event01", "Event02" }));
            Assert.That(document.Validate(), Is.Empty);
        }

        [Test]
        public void DuplicateNodeTitle_IsRejected()
        {
            EventCreationYarnDocument document = EventCreationYarnDocument.Parse(
                "title: Event01\n---\n===\n");

            Assert.That(
                () => document.AppendNode("event01"),
                Throws.ArgumentException);
        }
    }
}
