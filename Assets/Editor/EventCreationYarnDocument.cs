using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CaseStudy.EditorTools
{
    /// <summary>
    /// Small, lossless-enough editor model for the subset of Yarn edited by
    /// EventCreationWindow. Lines that are not dialogue are never rewritten.
    /// </summary>
    internal sealed class EventCreationYarnDocument
    {
        private static readonly Regex MetadataPattern = new Regex(
            @"(^|\s+)#(?<tag>[^\s#]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly List<string> lines = new List<string>();
        private readonly List<EventCreationYarnNode> nodes = new List<EventCreationYarnNode>();

        private EventCreationYarnDocument()
        {
        }

        public IReadOnlyList<EventCreationYarnNode> Nodes => nodes;
        public string LineEnding { get; private set; } = "\n";
        public bool HasUtf8Bom { get; set; }

        public static EventCreationYarnDocument Parse(string source, bool hasUtf8Bom = false)
        {
            var document = new EventCreationYarnDocument
            {
                HasUtf8Bom = hasUtf8Bom,
                LineEnding = source != null && source.Contains("\r\n") ? "\r\n" : "\n"
            };

            string normalized = (source ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            document.lines.AddRange(normalized.Split(new[] { '\n' }, StringSplitOptions.None));
            document.RebuildNodeIndex();
            return document;
        }

        public string ToText()
        {
            return string.Join(LineEnding, lines);
        }

        public List<EventCreationDialogueLine> GetDialogueLines(int nodeIndex)
        {
            var result = new List<EventCreationDialogueLine>();
            if (!TryGetNode(nodeIndex, out EventCreationYarnNode node))
            {
                return result;
            }

            for (int lineIndex = node.BodyStartLine; lineIndex < node.BodyEndLineExclusive; lineIndex++)
            {
                if (TryParseDialogueLine(lines[lineIndex], lineIndex, out EventCreationDialogueLine line))
                {
                    result.Add(line);
                }
            }

            return result;
        }

        public int CountProtectedBodyLines(int nodeIndex)
        {
            if (!TryGetNode(nodeIndex, out EventCreationYarnNode node))
            {
                return 0;
            }

            int count = 0;
            for (int lineIndex = node.BodyStartLine; lineIndex < node.BodyEndLineExclusive; lineIndex++)
            {
                string rawLine = lines[lineIndex];
                if (!string.IsNullOrWhiteSpace(rawLine) &&
                    !TryParseDialogueLine(rawLine, lineIndex, out _))
                {
                    count++;
                }
            }

            return count;
        }

        public void UpdateDialogueLine(EventCreationDialogueLine dialogueLine)
        {
            if (dialogueLine == null ||
                dialogueLine.SourceLineIndex < 0 ||
                dialogueLine.SourceLineIndex >= lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(dialogueLine));
            }

            lines[dialogueLine.SourceLineIndex] = BuildDialogueLine(dialogueLine);
        }

        public int InsertDialogueLine(int nodeIndex, int afterSourceLineIndex, EventCreationDialogueLine template = null)
        {
            if (!TryGetNode(nodeIndex, out EventCreationYarnNode node))
            {
                throw new ArgumentOutOfRangeException(nameof(nodeIndex));
            }

            int insertIndex = node.BodyEndLineExclusive;
            if (afterSourceLineIndex >= node.BodyStartLine &&
                afterSourceLineIndex < node.BodyEndLineExclusive)
            {
                insertIndex = afterSourceLineIndex + 1;
            }

            EventCreationDialogueLine newLine = template != null
                ? template.CloneForInsert()
                : new EventCreationDialogueLine { Text = "新しいセリフ" };
            lines.Insert(insertIndex, BuildDialogueLine(newLine));
            RebuildNodeIndex();
            return insertIndex;
        }

        public void RemoveDialogueLine(int sourceLineIndex)
        {
            if (!IsDialogueSourceLine(sourceLineIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceLineIndex));
            }

            lines.RemoveAt(sourceLineIndex);
            RebuildNodeIndex();
        }

        public bool CanMoveDialogueLine(int nodeIndex, int sourceLineIndex, int direction)
        {
            if (direction != -1 && direction != 1)
            {
                return false;
            }

            if (!TryGetNode(nodeIndex, out EventCreationYarnNode node))
            {
                return false;
            }

            int destination = sourceLineIndex + direction;
            return sourceLineIndex >= node.BodyStartLine &&
                   sourceLineIndex < node.BodyEndLineExclusive &&
                   destination >= node.BodyStartLine &&
                   destination < node.BodyEndLineExclusive &&
                   IsDialogueSourceLine(sourceLineIndex) &&
                   IsDialogueSourceLine(destination);
        }

        public int MoveDialogueLine(int nodeIndex, int sourceLineIndex, int direction)
        {
            if (!CanMoveDialogueLine(nodeIndex, sourceLineIndex, direction))
            {
                return sourceLineIndex;
            }

            int destination = sourceLineIndex + direction;
            (lines[sourceLineIndex], lines[destination]) = (lines[destination], lines[sourceLineIndex]);
            RebuildNodeIndex();
            return destination;
        }

        public int AppendNode(string title)
        {
            string trimmedTitle = (title ?? string.Empty).Trim();
            if (!IsValidNodeTitle(trimmedTitle, out string error))
            {
                throw new ArgumentException(error, nameof(title));
            }

            if (nodes.Any(node => string.Equals(node.Title, trimmedTitle, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"同名のノード「{trimmedTitle}」が既にあります。", nameof(title));
            }

            RemoveRedundantTrailingEmptyLines();
            if (lines.Count > 0 && !string.IsNullOrEmpty(lines[lines.Count - 1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add($"title: {trimmedTitle}");
            lines.Add("---");
            lines.Add("===");
            lines.Add(string.Empty);
            RebuildNodeIndex();
            return nodes.Count - 1;
        }

        public List<EventCreationValidationMessage> Validate()
        {
            var messages = new List<EventCreationValidationMessage>();
            if (nodes.Count == 0)
            {
                messages.Add(EventCreationValidationMessage.Error("Yarnノードが見つかりません。"));
                return messages;
            }

            var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                EventCreationYarnNode node = nodes[nodeIndex];
                if (!IsValidNodeTitle(node.Title, out string titleError))
                {
                    messages.Add(EventCreationValidationMessage.Error(
                        $"ノード {nodeIndex + 1}: {titleError}"));
                }
                else if (!titles.Add(node.Title))
                {
                    messages.Add(EventCreationValidationMessage.Error(
                        $"ノード名「{node.Title}」が重複しています。"));
                }

                if (!node.HasClosingDelimiter)
                {
                    messages.Add(EventCreationValidationMessage.Error(
                        $"ノード「{node.Title}」の終端がありません。"));
                }

                ValidateKnownMetadata(node, messages);
            }

            return messages;
        }

        public static bool IsValidNodeTitle(string title, out string error)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                error = "ノード名を入力してください。";
                return false;
            }

            if (title.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                error = "ノード名に改行は使えません。";
                return false;
            }

            if (title.Contains(":"))
            {
                error = "ノード名にコロンは使えません。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static bool TryParseDialogueLine(
            string rawLine,
            int sourceLineIndex,
            out EventCreationDialogueLine dialogueLine)
        {
            dialogueLine = null;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                return false;
            }

            int contentStart = 0;
            while (contentStart < rawLine.Length && char.IsWhiteSpace(rawLine[contentStart]))
            {
                contentStart++;
            }

            string indent = rawLine.Substring(0, contentStart);
            string content = rawLine.Substring(contentStart);
            if (IsProtectedYarnLine(content))
            {
                return false;
            }

            var parsed = new EventCreationDialogueLine
            {
                SourceLineIndex = sourceLineIndex,
                Indent = indent
            };

            string visibleContent = MetadataPattern.Replace(content, match =>
            {
                string rawTag = match.Groups["tag"].Value;
                ParseMetadata(rawTag, parsed);
                return match.Groups[1].Value;
            }).TrimEnd();

            if (string.IsNullOrWhiteSpace(visibleContent))
            {
                return false;
            }

            int colonIndex = visibleContent.IndexOf(':');
            if (colonIndex > 0)
            {
                string possibleSpeaker = visibleContent.Substring(0, colonIndex).Trim();
                if (IsLikelySpeakerName(possibleSpeaker))
                {
                    parsed.Speaker = possibleSpeaker;
                    parsed.Text = visibleContent.Substring(colonIndex + 1).TrimStart();
                    dialogueLine = parsed;
                    return true;
                }
            }

            parsed.Text = visibleContent;
            dialogueLine = parsed;
            return true;
        }

        internal static string BuildDialogueLine(EventCreationDialogueLine dialogueLine)
        {
            if (dialogueLine == null)
            {
                throw new ArgumentNullException(nameof(dialogueLine));
            }

            string text = SanitizeSingleLine(dialogueLine.Text).TrimEnd();
            string speaker = SanitizeSingleLine(dialogueLine.Speaker)
                .Trim()
                .Replace(':', '：');
            var builder = new StringBuilder();
            builder.Append(dialogueLine.Indent ?? string.Empty);
            if (!string.IsNullOrEmpty(speaker))
            {
                builder.Append(speaker);
                builder.Append(": ");
            }

            builder.Append(text);

            for (int i = 0; i < dialogueLine.PreservedMetadata.Count; i++)
            {
                AppendMetadata(builder, dialogueLine.PreservedMetadata[i]);
            }

            if (!string.IsNullOrWhiteSpace(dialogueLine.Illustration))
            {
                AppendMetadata(builder, $"illustration:{dialogueLine.Illustration.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(dialogueLine.Face))
            {
                AppendMetadata(builder, $"face:{dialogueLine.Face.Trim()}");
            }

            if (dialogueLine.Shake)
            {
                AppendMetadata(builder, "shake");
            }

            if (dialogueLine.HasFontScale)
            {
                float scale = Math.Max(0.1f, dialogueLine.FontScale);
                AppendMetadata(builder, $"size:{scale.ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            return builder.ToString();
        }

        private static void AppendMetadata(StringBuilder builder, string metadata)
        {
            string trimmed = (metadata ?? string.Empty).Trim().TrimStart('#');
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            if (builder.Length > 0 && !char.IsWhiteSpace(builder[builder.Length - 1]))
            {
                builder.Append(' ');
            }

            builder.Append('#');
            builder.Append(trimmed);
        }

        private static void ParseMetadata(string rawTag, EventCreationDialogueLine parsed)
        {
            string tag = (rawTag ?? string.Empty).Trim().TrimStart('#');
            if (tag.Equals("shake", StringComparison.OrdinalIgnoreCase))
            {
                parsed.Shake = true;
                return;
            }

            if (TryReadMetadataValue(tag, "face:", out string face))
            {
                if (string.IsNullOrEmpty(parsed.Face))
                {
                    parsed.Face = string.IsNullOrWhiteSpace(face) ? "default" : face.Trim();
                }
                return;
            }

            if (TryReadMetadataValue(tag, "illustration:", out string illustration))
            {
                if (string.IsNullOrEmpty(parsed.Illustration))
                {
                    parsed.Illustration = string.IsNullOrWhiteSpace(illustration) ? "hide" : illustration.Trim();
                }
                return;
            }

            if (TryReadMetadataValue(tag, "size:", out string sizeValue) &&
                float.TryParse(sizeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale) &&
                scale > 0f)
            {
                if (!parsed.HasFontScale)
                {
                    parsed.HasFontScale = true;
                    parsed.FontScale = scale;
                }
                return;
            }

            parsed.PreservedMetadata.Add(tag);
        }

        private static bool TryReadMetadataValue(string tag, string prefix, out string value)
        {
            if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = tag.Substring(prefix.Length);
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static bool IsProtectedYarnLine(string content)
        {
            string trimmed = content.TrimStart();
            return trimmed.StartsWith("<<", StringComparison.Ordinal) ||
                   trimmed.StartsWith("->", StringComparison.Ordinal) ||
                   trimmed.StartsWith("[[", StringComparison.Ordinal) ||
                   trimmed.StartsWith("//", StringComparison.Ordinal) ||
                   trimmed.StartsWith("---", StringComparison.Ordinal) ||
                   trimmed.StartsWith("===", StringComparison.Ordinal) ||
                   trimmed.StartsWith("title:", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("tags:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelySpeakerName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Length <= 64 &&
                   value.IndexOfAny(new[] { '<', '>', '[', ']', '{', '}', '#' }) < 0;
        }

        private static string SanitizeSingleLine(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }

        private bool TryGetNode(int nodeIndex, out EventCreationYarnNode node)
        {
            if (nodeIndex >= 0 && nodeIndex < nodes.Count)
            {
                node = nodes[nodeIndex];
                return true;
            }

            node = null;
            return false;
        }

        private bool IsDialogueSourceLine(int sourceLineIndex)
        {
            return sourceLineIndex >= 0 &&
                   sourceLineIndex < lines.Count &&
                   TryParseDialogueLine(lines[sourceLineIndex], sourceLineIndex, out _);
        }

        private void RebuildNodeIndex()
        {
            nodes.Clear();
            for (int index = 0; index < lines.Count; index++)
            {
                if (!TryReadTitle(lines[index], out string title))
                {
                    continue;
                }

                int bodyDelimiter = FindLine(index + 1, "---", stopAtNextTitle: true);
                if (bodyDelimiter < 0)
                {
                    continue;
                }

                int closingDelimiter = FindLine(bodyDelimiter + 1, "===", stopAtNextTitle: true);
                int bodyEnd = closingDelimiter >= 0 ? closingDelimiter : FindNextTitleOrEnd(bodyDelimiter + 1);
                nodes.Add(new EventCreationYarnNode(
                    title,
                    index,
                    bodyDelimiter + 1,
                    bodyEnd,
                    closingDelimiter >= 0));

                if (closingDelimiter >= 0)
                {
                    index = closingDelimiter;
                }
            }
        }

        private int FindLine(int startIndex, string expected, bool stopAtNextTitle)
        {
            for (int index = startIndex; index < lines.Count; index++)
            {
                if (string.Equals(lines[index].Trim(), expected, StringComparison.Ordinal))
                {
                    return index;
                }

                if (stopAtNextTitle && TryReadTitle(lines[index], out _))
                {
                    return -1;
                }
            }

            return -1;
        }

        private int FindNextTitleOrEnd(int startIndex)
        {
            for (int index = startIndex; index < lines.Count; index++)
            {
                if (TryReadTitle(lines[index], out _))
                {
                    return index;
                }
            }

            return lines.Count;
        }

        private static bool TryReadTitle(string rawLine, out string title)
        {
            string trimmed = (rawLine ?? string.Empty).Trim();
            const string prefix = "title:";
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                title = trimmed.Substring(prefix.Length).Trim();
                return true;
            }

            title = string.Empty;
            return false;
        }

        private void ValidateKnownMetadata(
            EventCreationYarnNode node,
            ICollection<EventCreationValidationMessage> messages)
        {
            for (int index = node.BodyStartLine; index < node.BodyEndLineExclusive; index++)
            {
                MatchCollection matches = MetadataPattern.Matches(lines[index]);
                foreach (Match match in matches)
                {
                    string tag = match.Groups["tag"].Value;
                    if (!TryReadMetadataValue(tag, "size:", out string sizeValue))
                    {
                        continue;
                    }

                    if (!float.TryParse(
                            sizeValue,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float scale) ||
                        scale <= 0f)
                    {
                        messages.Add(EventCreationValidationMessage.Error(
                            $"ノード「{node.Title}」の {index + 1} 行目に、不正な文字サイズ設定があります。"));
                    }
                }
            }
        }

        private void RemoveRedundantTrailingEmptyLines()
        {
            while (lines.Count > 0 && string.IsNullOrEmpty(lines[lines.Count - 1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }
    }

    internal sealed class EventCreationYarnNode
    {
        public EventCreationYarnNode(
            string title,
            int startLine,
            int bodyStartLine,
            int bodyEndLineExclusive,
            bool hasClosingDelimiter)
        {
            Title = title;
            StartLine = startLine;
            BodyStartLine = bodyStartLine;
            BodyEndLineExclusive = bodyEndLineExclusive;
            HasClosingDelimiter = hasClosingDelimiter;
        }

        public string Title { get; }
        public int StartLine { get; }
        public int BodyStartLine { get; }
        public int BodyEndLineExclusive { get; }
        public bool HasClosingDelimiter { get; }
    }

    internal sealed class EventCreationDialogueLine
    {
        public int SourceLineIndex = -1;
        public string Indent = string.Empty;
        public string Speaker = string.Empty;
        public string Text = string.Empty;
        public string Face = string.Empty;
        public string Illustration = string.Empty;
        public bool Shake;
        public bool HasFontScale;
        public float FontScale = 1f;
        public readonly List<string> PreservedMetadata = new List<string>();

        public EventCreationDialogueLine CloneForInsert()
        {
            var clone = new EventCreationDialogueLine
            {
                Indent = Indent,
                Speaker = Speaker,
                Text = Text,
                Face = Face,
                Illustration = Illustration,
                Shake = Shake,
                HasFontScale = HasFontScale,
                FontScale = FontScale
            };
            clone.PreservedMetadata.AddRange(PreservedMetadata);
            return clone;
        }
    }

    internal readonly struct EventCreationValidationMessage
    {
        private EventCreationValidationMessage(string text, bool isError)
        {
            Text = text;
            IsError = isError;
        }

        public string Text { get; }
        public bool IsError { get; }

        public static EventCreationValidationMessage Error(string text)
        {
            return new EventCreationValidationMessage(text, true);
        }
    }
}
