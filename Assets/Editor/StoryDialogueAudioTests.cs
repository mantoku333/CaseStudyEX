using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CaseStudy.EditorTools
{
    public sealed class StoryDialogueAudioTests
    {
        [Test]
        public void AudioSettings_SurviveEditingDuplicationAndReload()
        {
            var document = EventCreationYarnDocument.Parse(
                "title: Audio\r\n---\r\nAlice: Hello #custom:keep #bgm:music #bgmvolume:0.4 #bgmfade:2 #se:effect #sevolume:0.7\r\n<<wait 1>>\r\n===\r\n");
            var line = document.GetDialogueLines(0).Single();
            Assert.That(line.Bgm, Is.EqualTo("music"));
            Assert.That(line.Se, Is.EqualTo("effect"));
            line.Text = "Edited";
            document.UpdateDialogueLine(line);
            document.InsertDialogueLine(0, line.SourceLineIndex, line);
            var reloaded = EventCreationYarnDocument.Parse(document.ToText()).GetDialogueLines(0);
            Assert.That(reloaded.Count, Is.EqualTo(2));
            foreach (var copy in reloaded)
            {
                Assert.That(copy.Bgm, Is.EqualTo("music"));
                Assert.That(copy.BgmVolume, Is.EqualTo(0.4f));
                Assert.That(copy.BgmFade, Is.EqualTo(2f));
                Assert.That(copy.Se, Is.EqualTo("effect"));
                Assert.That(copy.SeVolume, Is.EqualTo(0.7f));
                Assert.That(copy.PreservedMetadata, Does.Contain("custom:keep"));
            }
            Assert.That(document.ToText(), Does.Contain("\r\n<<wait 1>>\r\n"));
        }

        [Test]
        public void ClearingAudio_RemovesTagsAndStopSurvivesReload()
        {
            var document = EventCreationYarnDocument.Parse("title: Audio\n---\nHello #bgm:music #se:effect\n===\n");
            var line = document.GetDialogueLines(0).Single();
            line.Bgm = "stop";
            line.Se = string.Empty;
            line.BgmFade = 0f;
            document.UpdateDialogueLine(line);
            var reloaded = EventCreationYarnDocument.Parse(document.ToText()).GetDialogueLines(0).Single();
            Assert.That(reloaded.Bgm, Is.EqualTo("stop"));
            Assert.That(reloaded.BgmFade, Is.Zero);
            Assert.That(document.ToText(), Does.Not.Contain("#se:"));
            line.Bgm = string.Empty;
            document.UpdateDialogueLine(line);
            Assert.That(document.ToText(), Does.Not.Contain("#bgm"));
        }

        [Test]
        public void Catalog_UsesStableKeysForClipsWithIdenticalNames()
        {
            var catalog = ScriptableObject.CreateInstance<StoryDialogueAudioCatalog>();
            var first = AudioClip.Create("SameName", 8, 1, 8000, false);
            var second = AudioClip.Create("SameName", 8, 1, 8000, false);
            try
            {
                catalog.entries.Add(new StoryDialogueAudioCatalog.Entry { key = "first", clip = first });
                catalog.entries.Add(new StoryDialogueAudioCatalog.Entry { key = "second", clip = second });
                Assert.That(catalog.FindClip("first"), Is.SameAs(first));
                Assert.That(catalog.FindClip("second"), Is.SameAs(second));
                Assert.That(catalog.FindClip("missing"), Is.Null);
                StoryDialogueAudioCatalog.ApplyLineMetadata(new[] { "face:smile" });
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
