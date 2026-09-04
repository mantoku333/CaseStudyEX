using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Metroidvania.UI;
using UnityEditor;
using UnityEngine;

namespace CaseStudy.EditorTools
{
    public sealed class EventCreationWindow : EditorWindow
    {
        private const string WindowTitle = "イベント作成くん";
        private const string LastAssetPathKey = "CaseStudy.EventCreationKun.LastAssetPath";
        private const float LineListWidth = 340f;

        private static readonly float[] FontScaleValues = { 0.75f, 1f, 1.5f };
        private static readonly string[] FontScaleLabels =
        {
            "小さめ（0.75倍）",
            "普通（1倍）",
            "大きめ（1.5倍）"
        };

        private static readonly Dictionary<string, string> ExpressionLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "default", "通常" },
                { "soft_smile", "控えめな笑顔" },
                { "sad", "悲しい" },
                { "surprised", "驚き" },
                { "jitome", "ジト目" },
                { "smile", "笑顔" },
                { "assertive", "主張" }
            };

        [SerializeField] private string selectedAssetPath = string.Empty;
        [SerializeField] private int selectedNodeIndex;
        [SerializeField] private int selectedSourceLine = -1;
        [SerializeField] private Vector2 lineListScroll;
        [SerializeField] private Vector2 detailScroll;
        [SerializeField] private string pendingNodeTitle = string.Empty;
        [SerializeField] private bool isDirty;
        [SerializeField] private string loadedText = string.Empty;
        [SerializeField] private string workingText = string.Empty;
        [SerializeField] private bool workingTextHasUtf8Bom;

        private readonly List<string> yarnAssetPaths = new List<string>();
        private readonly List<EventCreationDialogueLine> dialogueLines = new List<EventCreationDialogueLine>();
        private readonly HashSet<string> speakerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> expressionNamesBySpeaker =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> illustrationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private EventCreationYarnDocument document;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.None;
        private bool skipDestroyPrompt;

        [MenuItem("Tools/CaseStudy/イベント作成くん")]
        public static void Open()
        {
            EventCreationWindow window = GetWindow<EventCreationWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(940f, 560f);
            window.Show();
            window.Focus();
        }

        [MenuItem("Assets/イベント作成くんで開く", false, 2000)]
        private static void OpenSelectedYarn()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            EventCreationWindow window = GetWindow<EventCreationWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(940f, 560f);
            window.Show();
            window.SwitchFile(path, askToSave: true);
            window.Focus();
        }

        [MenuItem("Assets/イベント作成くんで開く", true)]
        private static bool ValidateOpenSelectedYarn()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return IsYarnAssetPath(path);
        }

        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(940f, 560f);
            RefreshYarnFiles();
            RefreshPresentationOptions();

            if (isDirty && IsYarnAssetPath(selectedAssetPath) && workingText != null)
            {
                document = EventCreationYarnDocument.Parse(workingText, workingTextHasUtf8Bom);
                selectedNodeIndex = Mathf.Clamp(
                    selectedNodeIndex,
                    0,
                    Math.Max(0, document.Nodes.Count - 1));
                RefreshDialogueLines(selectedSourceLine);
                SetStatus("再コンパイル前の未保存内容を復元しました。", MessageType.Info);
                return;
            }

            string selectionPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string initialPath = IsYarnAssetPath(selectionPath)
                ? selectionPath
                : selectedAssetPath;
            if (!IsYarnAssetPath(initialPath))
            {
                initialPath = EditorPrefs.GetString(LastAssetPathKey, string.Empty);
            }

            if (!IsYarnAssetPath(initialPath) && yarnAssetPaths.Count > 0)
            {
                initialPath = yarnAssetPaths[0];
            }

            if (IsYarnAssetPath(initialPath))
            {
                LoadFile(initialPath);
            }
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private void OnDestroy()
        {
            if (skipDestroyPrompt || !isDirty || document == null)
            {
                return;
            }

            if (EditorUtility.DisplayDialog(
                    WindowTitle,
                    "ウィンドウを閉じる前に、未保存の変更を保存しますか？",
                    "保存",
                    "変更を破棄"))
            {
                SaveDocument();
            }
        }

        private void OnProjectChange()
        {
            RefreshYarnFiles();
            Repaint();
        }

        private void OnGUI()
        {
            HandleSaveShortcut();
            DrawHeader();
            DrawFileToolbar();

            if (document == null)
            {
                EditorGUILayout.HelpBox(
                    "編集するYarnファイルを選んでください。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            DrawNodeToolbar();
            DrawValidationSummary();
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLineList();
                DrawSelectedLine();
            }

            DrawStatus();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (isDirty)
                {
                    GUILayout.Label("● 未保存", EditorStyles.miniBoldLabel);
                }
                else if (document != null)
                {
                    GUILayout.Label("保存済み", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.HelpBox(
                "イベントダイアログを項目から編集",
                MessageType.Info);
        }

        private void DrawFileToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("ファイル", GUILayout.Width(48f));
                int currentIndex = yarnAssetPaths.FindIndex(path =>
                    string.Equals(path, selectedAssetPath, StringComparison.OrdinalIgnoreCase));
                string[] labels = yarnAssetPaths.Select(BuildAssetLabel).ToArray();

                using (new EditorGUI.DisabledScope(labels.Length == 0))
                {
                    int shownIndex = Mathf.Max(0, currentIndex);
                    int nextIndex = EditorGUILayout.Popup(shownIndex, labels, EditorStyles.toolbarPopup);
                    if (labels.Length > 0 && nextIndex != currentIndex)
                    {
                        SwitchFile(yarnAssetPaths[nextIndex], askToSave: true);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("再読込", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                {
                    ReloadCurrentFile();
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(document == null || !isDirty))
                {
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                    {
                        SaveDocument();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("候補更新", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    RefreshPresentationOptions();
                    RefreshDialogueLines();
                    SetStatus("話者・表情・一枚絵の候補を更新しました。", MessageType.Info);
                }
            }

            if (!string.IsNullOrEmpty(selectedAssetPath))
            {
                EditorGUILayout.LabelField(selectedAssetPath, EditorStyles.miniLabel);
            }
        }

        private void DrawNodeToolbar()
        {
            IReadOnlyList<EventCreationYarnNode> nodes = document.Nodes;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("イベント", GUILayout.Width(58f));
                string[] nodeLabels = nodes
                    .Select((node, index) => $"{index + 1:00}  {node.Title}")
                    .ToArray();
                using (new EditorGUI.DisabledScope(nodeLabels.Length == 0))
                {
                    int safeIndex = Mathf.Clamp(selectedNodeIndex, 0, Math.Max(0, nodeLabels.Length - 1));
                    int nextIndex = EditorGUILayout.Popup(safeIndex, nodeLabels);
                    if (nextIndex != selectedNodeIndex)
                    {
                        selectedNodeIndex = nextIndex;
                        selectedSourceLine = -1;
                        RefreshDialogueLines();
                        GUI.FocusControl(null);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("＋ 新しいイベントを追加", GUILayout.Width(180f), GUILayout.Height(26f)))
                {
                    NewEventNameWindow.Open(this, pendingNodeTitle);
                }
            }
        }

        private void DrawLineList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LineListWidth)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"会話一覧  {dialogueLines.Count}行",
                        EditorStyles.boldLabel);
                    int protectedCount = document.CountProtectedBodyLines(selectedNodeIndex);
                    if (protectedCount > 0)
                    {
                        GUILayout.Label($"制御 {protectedCount}行を保護", EditorStyles.miniLabel);
                    }
                }

                lineListScroll = EditorGUILayout.BeginScrollView(
                    lineListScroll,
                    GUI.skin.box,
                    GUILayout.Width(LineListWidth),
                    GUILayout.ExpandHeight(true));

                if (dialogueLines.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "会話行がありません。下のボタンから追加できます。",
                        MessageType.None);
                }

                for (int index = 0; index < dialogueLines.Count; index++)
                {
                    EventCreationDialogueLine line = dialogueLines[index];
                    bool selected = line.SourceLineIndex == selectedSourceLine;
                    string summary = BuildLineSummary(index, line);
                    bool nextSelected = GUILayout.Toggle(
                        selected,
                        summary,
                        "Button",
                        GUILayout.MinHeight(38f));
                    if (nextSelected && !selected)
                    {
                        selectedSourceLine = line.SourceLineIndex;
                        GUI.FocusControl(null);
                    }
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("＋ 会話を末尾に追加", GUILayout.Height(28f)))
                {
                    int sourceLine = document.InsertDialogueLine(selectedNodeIndex, -1);
                    MarkDirtyAndRefresh(sourceLine);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawSelectedLine()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EventCreationDialogueLine line = FindSelectedLine();
                if (line == null)
                {
                    EditorGUILayout.HelpBox(
                        "左の一覧から会話を選んでください。",
                        MessageType.Info);
                    return;
                }

                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                EditorGUILayout.LabelField("会話の内容", EditorStyles.boldLabel);
                EditorGUILayout.Space(4f);

                EditorGUI.BeginChangeCheck();
                string previousSpeaker = line.Speaker;
                DrawSpeakerField(line);
                ClearUnsupportedFaceAfterSpeakerChange(line, previousSpeaker);
                EditorGUILayout.LabelField("本文");
                line.Text = EditorGUILayout.TextArea(
                    line.Text ?? string.Empty,
                    GUILayout.MinHeight(92f));
                EditorGUILayout.LabelField(
                    "※ 会話を分ける場合は改行ではなく「下に追加」を使います。",
                    EditorStyles.miniLabel);

                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("ADV演出", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(line.Speaker)))
                {
                    line.Face = DrawChoicePopup(
                        "表情",
                        line.Face,
                        BuildExpressionChoices(line.Speaker, line.Face),
                        "指定なし（通常）",
                        GetExpressionLabel);
                }
                line.Illustration = DrawChoicePopup(
                    "一枚絵",
                    line.Illustration,
                    BuildIllustrationChoices(line.Illustration),
                    "変更しない",
                    GetIllustrationLabel);
                line.Shake = EditorGUILayout.Toggle("文字を揺らす", line.Shake);
                int fontScaleIndex = GetFontScaleIndex(line);
                fontScaleIndex = EditorGUILayout.Popup(
                    "文字サイズ",
                    fontScaleIndex,
                    FontScaleLabels);
                line.FontScale = FontScaleValues[fontScaleIndex];
                line.HasFontScale = fontScaleIndex != 1;

                if (line.PreservedMetadata.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"この行には、この画面が扱わない追加設定が {line.PreservedMetadata.Count} 件あります。内容は保存時も維持されます。",
                        MessageType.None);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    document.UpdateDialogueLine(line);
                    CaptureDirtyWorkingText();
                    SetStatus("変更があります。ファイルを切り替える前に保存してください。", MessageType.Info);
                }

                EditorGUILayout.Space(14f);
                DrawLineActions(line);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSpeakerField(EventCreationDialogueLine line)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                line.Speaker = EditorGUILayout.TextField("話者（空欄は地の文）", line.Speaker ?? string.Empty);
                if (GUILayout.Button("候補", GUILayout.Width(54f)))
                {
                    ShowSpeakerMenu(line.SourceLineIndex);
                }
            }
        }

        private void DrawLineActions(EventCreationDialogueLine line)
        {
            EditorGUILayout.LabelField("行の操作", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("下に追加"))
                {
                    var addedLine = new EventCreationDialogueLine { Speaker = line.Speaker, Text = "新しいセリフ" };
                    int sourceLine = document.InsertDialogueLine(
                        selectedNodeIndex,
                        line.SourceLineIndex,
                        addedLine);
                    MarkDirtyAndRefresh(sourceLine);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("複製"))
                {
                    int sourceLine = document.InsertDialogueLine(
                        selectedNodeIndex,
                        line.SourceLineIndex,
                        line);
                    MarkDirtyAndRefresh(sourceLine);
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(
                           !document.CanMoveDialogueLine(selectedNodeIndex, line.SourceLineIndex, -1)))
                {
                    if (GUILayout.Button("↑"))
                    {
                        int sourceLine = document.MoveDialogueLine(
                            selectedNodeIndex,
                            line.SourceLineIndex,
                            -1);
                        MarkDirtyAndRefresh(sourceLine);
                        GUIUtility.ExitGUI();
                    }
                }

                using (new EditorGUI.DisabledScope(
                           !document.CanMoveDialogueLine(selectedNodeIndex, line.SourceLineIndex, 1)))
                {
                    if (GUILayout.Button("↓"))
                    {
                        int sourceLine = document.MoveDialogueLine(
                            selectedNodeIndex,
                            line.SourceLineIndex,
                            1);
                        MarkDirtyAndRefresh(sourceLine);
                        GUIUtility.ExitGUI();
                    }
                }

                GUI.backgroundColor = new Color(1f, 0.72f, 0.72f);
                if (GUILayout.Button("削除", GUILayout.Width(64f)) &&
                    EditorUtility.DisplayDialog(
                        WindowTitle,
                        "選択中の会話を削除しますか？\n保存するまでは「再読込」で元に戻せます。",
                        "削除",
                        "キャンセル"))
                {
                    int previousSourceLine = line.SourceLineIndex;
                    document.RemoveDialogueLine(previousSourceLine);
                    CaptureDirtyWorkingText();
                    RefreshDialogueLines();
                    SelectClosestLine(previousSourceLine);
                    SetStatus("会話を削除しました（未保存）。", MessageType.Warning);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.HelpBox(
                "↑↓は隣り合う会話だけ移動できます。制御行をまたぐ移動は、イベント順の事故を防ぐため無効です。",
                MessageType.None);
        }

        private void DrawValidationSummary()
        {
            List<EventCreationValidationMessage> validation = document.Validate();
            if (validation.Count == 0)
            {
                return;
            }

            int shown = Math.Min(3, validation.Count);
            for (int index = 0; index < shown; index++)
            {
                EditorGUILayout.HelpBox(validation[index].Text, MessageType.Error);
            }

            if (validation.Count > shown)
            {
                EditorGUILayout.LabelField(
                    $"ほか {validation.Count - shown} 件の問題があります。",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawStatus()
        {
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
        }

        private string DrawChoicePopup(
            string label,
            string currentValue,
            IReadOnlyList<string> choices,
            string emptyLabel,
            Func<string, string> labelFactory)
        {
            var values = new List<string> { string.Empty };
            values.AddRange(choices.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(currentValue) &&
                !values.Any(value => string.Equals(value, currentValue, StringComparison.OrdinalIgnoreCase)))
            {
                values.Add(currentValue);
            }

            int currentIndex = values.FindIndex(value =>
                string.Equals(value, currentValue ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            currentIndex = Mathf.Max(0, currentIndex);
            string[] labels = values
                .Select(value => string.IsNullOrEmpty(value) ? emptyLabel : labelFactory(value))
                .ToArray();
            int nextIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            return values[Mathf.Clamp(nextIndex, 0, values.Count - 1)];
        }

        private List<string> BuildExpressionChoices(string speaker, string currentValue)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "default"
            };
            if (!string.IsNullOrWhiteSpace(speaker) &&
                expressionNamesBySpeaker.TryGetValue(speaker.Trim(), out HashSet<string> configuredNames))
            {
                values.UnionWith(configuredNames);
            }
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                values.Add(currentValue);
            }

            return values
                .OrderBy(value => value.Equals("default", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(GetExpressionLabel, StringComparer.CurrentCulture)
                .ToList();
        }

        private List<string> BuildIllustrationChoices(string currentValue)
        {
            var values = new HashSet<string>(illustrationNames, StringComparer.OrdinalIgnoreCase)
            {
                "hide"
            };
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                values.Add(currentValue);
            }

            return values
                .OrderBy(value => value.Equals("hide", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(GetIllustrationLabel, StringComparer.CurrentCulture)
                .ToList();
        }

        private static string GetExpressionLabel(string value)
        {
            return ExpressionLabels.TryGetValue(value ?? string.Empty, out string label)
                ? label
                : ObjectNames.NicifyVariableName(value ?? string.Empty);
        }

        private static string GetIllustrationLabel(string value)
        {
            if (string.Equals(value, "hide", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                return "一枚絵を隠す";
            }

            return ObjectNames.NicifyVariableName(value ?? string.Empty);
        }

        private static string BuildLineSummary(int index, EventCreationDialogueLine line)
        {
            string speaker = string.IsNullOrWhiteSpace(line.Speaker) ? "地の文" : line.Speaker.Trim();
            string text = (line.Text ?? string.Empty).Trim();
            if (text.Length > 24)
            {
                text = text.Substring(0, 24) + "…";
            }

            var effects = new List<string>();
            if (!string.IsNullOrWhiteSpace(line.Face))
            {
                effects.Add(GetExpressionLabel(line.Face));
            }
            if (!string.IsNullOrWhiteSpace(line.Illustration))
            {
                effects.Add(GetIllustrationLabel(line.Illustration));
            }
            if (line.Shake)
            {
                effects.Add("揺れ");
            }
            if (line.HasFontScale)
            {
                effects.Add($"{line.FontScale:0.##}倍");
            }

            string effectText = effects.Count > 0 ? $"\n    演出: {string.Join("・", effects)}" : string.Empty;
            return $"{index + 1:000}  {speaker}「{text}」{effectText}";
        }

        private static int GetFontScaleIndex(EventCreationDialogueLine line)
        {
            if (line == null || !line.HasFontScale || Mathf.Approximately(line.FontScale, 1f))
            {
                return 1;
            }

            if (Mathf.Approximately(line.FontScale, 0.75f))
            {
                return 0;
            }

            return Mathf.Approximately(line.FontScale, 1.5f) ? 2 : 1;
        }

        private static string BuildAssetLabel(string assetPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
            if (folder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                folder = folder.Substring("Assets/".Length);
            }

            return string.IsNullOrEmpty(folder) ? fileName : $"{fileName}  —  {folder}";
        }

        private bool AddNode()
        {
            try
            {
                int nodeIndex = document.AppendNode(pendingNodeTitle);
                selectedNodeIndex = nodeIndex;
                pendingNodeTitle = string.Empty;
                selectedSourceLine = -1;
                CaptureDirtyWorkingText();
                RefreshDialogueLines();
                SetStatus("新しいイベントを追加しました。会話を追加して保存してください。", MessageType.Info);
                return true;
            }
            catch (ArgumentException exception)
            {
                EditorUtility.DisplayDialog(WindowTitle, exception.Message, "OK");
                return false;
            }
        }

        private bool AddNode(string nodeTitle)
        {
            pendingNodeTitle = nodeTitle ?? string.Empty;
            bool added = AddNode();
            Repaint();
            return added;
        }

        private void ShowSpeakerMenu(int sourceLineIndex)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent("地の文（話者なし）"),
                false,
                () => SetSpeaker(sourceLineIndex, string.Empty));
            menu.AddSeparator(string.Empty);
            foreach (string speaker in speakerNames.OrderBy(value => value, StringComparer.CurrentCulture))
            {
                string capturedSpeaker = speaker;
                menu.AddItem(
                    new GUIContent(capturedSpeaker),
                    false,
                    () => SetSpeaker(sourceLineIndex, capturedSpeaker));
            }
            menu.ShowAsContext();
        }

        private void SetSpeaker(int sourceLineIndex, string speaker)
        {
            EventCreationDialogueLine line = dialogueLines.FirstOrDefault(candidate =>
                candidate.SourceLineIndex == sourceLineIndex);
            if (line == null)
            {
                return;
            }

            string previousSpeaker = line.Speaker;
            line.Speaker = speaker ?? string.Empty;
            ClearUnsupportedFaceAfterSpeakerChange(line, previousSpeaker);
            document.UpdateDialogueLine(line);
            CaptureDirtyWorkingText();
            RefreshDialogueLines(sourceLineIndex);
            Repaint();
        }

        private EventCreationDialogueLine FindSelectedLine()
        {
            EventCreationDialogueLine selected = dialogueLines.FirstOrDefault(line =>
                line.SourceLineIndex == selectedSourceLine);
            if (selected == null && dialogueLines.Count > 0)
            {
                selected = dialogueLines[0];
                selectedSourceLine = selected.SourceLineIndex;
            }

            return selected;
        }

        private void SelectClosestLine(int previousSourceLine)
        {
            EventCreationDialogueLine closest = dialogueLines
                .OrderBy(line => Math.Abs(line.SourceLineIndex - previousSourceLine))
                .FirstOrDefault();
            selectedSourceLine = closest?.SourceLineIndex ?? -1;
        }

        private void MarkDirtyAndRefresh(int selectedLine)
        {
            CaptureDirtyWorkingText();
            RefreshDialogueLines(selectedLine);
            SetStatus("変更があります。ファイルを切り替える前に保存してください。", MessageType.Info);
        }

        private void RefreshDialogueLines(int preferredSourceLine = -1)
        {
            dialogueLines.Clear();
            if (document == null || document.Nodes.Count == 0)
            {
                selectedSourceLine = -1;
                return;
            }

            selectedNodeIndex = Mathf.Clamp(selectedNodeIndex, 0, document.Nodes.Count - 1);
            CollectDocumentSpeakerNames();
            dialogueLines.AddRange(document.GetDialogueLines(selectedNodeIndex));
            foreach (EventCreationDialogueLine line in dialogueLines)
            {
                if (!string.IsNullOrWhiteSpace(line.Speaker))
                {
                    speakerNames.Add(line.Speaker.Trim());
                }
                if (!string.IsNullOrWhiteSpace(line.Face))
                {
                    AddExpressionName(line.Speaker, line.Face);
                }
                if (!string.IsNullOrWhiteSpace(line.Illustration) &&
                    !string.Equals(line.Illustration, "hide", StringComparison.OrdinalIgnoreCase))
                {
                    illustrationNames.Add(line.Illustration.Trim());
                }
            }

            int requestedLine = preferredSourceLine >= 0 ? preferredSourceLine : selectedSourceLine;
            if (dialogueLines.All(line => line.SourceLineIndex != requestedLine))
            {
                requestedLine = dialogueLines.Count > 0 ? dialogueLines[0].SourceLineIndex : -1;
            }
            selectedSourceLine = requestedLine;
        }

        private void CollectDocumentSpeakerNames()
        {
            if (document == null)
            {
                return;
            }

            for (int nodeIndex = 0; nodeIndex < document.Nodes.Count; nodeIndex++)
            {
                List<EventCreationDialogueLine> nodeLines = document.GetDialogueLines(nodeIndex);
                for (int lineIndex = 0; lineIndex < nodeLines.Count; lineIndex++)
                {
                    string speaker = nodeLines[lineIndex].Speaker?.Trim();
                    if (!string.IsNullOrEmpty(speaker))
                    {
                        speakerNames.Add(speaker);
                    }
                }
            }
        }

        private void RefreshYarnFiles()
        {
            yarnAssetPaths.Clear();
            yarnAssetPaths.AddRange(AssetDatabase.GetAllAssetPaths()
                .Where(IsYarnAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        private void RefreshPresentationOptions()
        {
            speakerNames.Clear();
            expressionNamesBySpeaker.Clear();
            illustrationNames.Clear();

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    continue;
                }

                DialogueView[] views = prefab.GetComponentsInChildren<DialogueView>(true);
                for (int viewIndex = 0; viewIndex < views.Length; viewIndex++)
                {
                    CollectOptions(views[viewIndex]);
                }
            }
        }

        private void CollectOptions(DialogueView view)
        {
            var serializedObject = new SerializedObject(view);
            SerializedProperty portraits = serializedObject.FindProperty("characterPortraits");
            if (portraits != null && portraits.isArray)
            {
                for (int index = 0; index < portraits.arraySize; index++)
                {
                    SerializedProperty portrait = portraits.GetArrayElementAtIndex(index);
                    string speaker = portrait.FindPropertyRelative("characterName")?.stringValue?.Trim();
                    if (!string.IsNullOrEmpty(speaker))
                    {
                        speakerNames.Add(speaker);
                    }
                    SerializedProperty expressions = portrait.FindPropertyRelative("expressionPortraits");
                    if (expressions == null || !expressions.isArray)
                    {
                        continue;
                    }

                    for (int expressionIndex = 0; expressionIndex < expressions.arraySize; expressionIndex++)
                    {
                        string expression = expressions.GetArrayElementAtIndex(expressionIndex)
                            .FindPropertyRelative("expressionName")?.stringValue?.Trim();
                        AddExpressionName(speaker, expression);
                    }
                }
            }

            SerializedProperty illustrations = serializedObject.FindProperty("centerIllustrations");
            if (illustrations == null || !illustrations.isArray)
            {
                return;
            }

            for (int index = 0; index < illustrations.arraySize; index++)
            {
                AddPropertyString(
                    illustrationNames,
                    illustrations.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("illustrationName"));
            }
        }

        private static void AddPropertyString(ISet<string> target, SerializedProperty property)
        {
            string value = property?.stringValue?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                target.Add(value);
            }
        }

        private void AddExpressionName(string speaker, string expression)
        {
            string trimmedSpeaker = speaker?.Trim();
            string trimmedExpression = expression?.Trim();
            if (string.IsNullOrEmpty(trimmedSpeaker) || string.IsNullOrEmpty(trimmedExpression))
            {
                return;
            }

            if (!expressionNamesBySpeaker.TryGetValue(trimmedSpeaker, out HashSet<string> names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                expressionNamesBySpeaker.Add(trimmedSpeaker, names);
            }

            names.Add(trimmedExpression);
        }

        private void ClearUnsupportedFaceAfterSpeakerChange(
            EventCreationDialogueLine line,
            string previousSpeaker)
        {
            if (line == null ||
                string.Equals(line.Speaker, previousSpeaker, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(line.Face))
            {
                return;
            }

            string nextSpeaker = line.Speaker?.Trim();
            if (string.IsNullOrEmpty(nextSpeaker) ||
                (!line.Face.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                 (!expressionNamesBySpeaker.TryGetValue(nextSpeaker, out HashSet<string> names) ||
                  !names.Contains(line.Face))))
            {
                line.Face = string.Empty;
            }
        }

        private void ReloadCurrentFile()
        {
            if (document == null || string.IsNullOrEmpty(selectedAssetPath))
            {
                return;
            }

            if (isDirty && !EditorUtility.DisplayDialog(
                    WindowTitle,
                    "未保存の変更を破棄して、ファイルを読み直しますか？",
                    "再読込",
                    "キャンセル"))
            {
                return;
            }

            LoadFile(selectedAssetPath);
        }

        private bool SwitchFile(string assetPath, bool askToSave)
        {
            if (!IsYarnAssetPath(assetPath))
            {
                return false;
            }

            if (string.Equals(assetPath, selectedAssetPath, StringComparison.OrdinalIgnoreCase) &&
                document != null)
            {
                return true;
            }

            if (askToSave && isDirty && !ResolveUnsavedChanges())
            {
                return false;
            }

            return LoadFile(assetPath);
        }

        private bool ResolveUnsavedChanges()
        {
            int choice = EditorUtility.DisplayDialogComplex(
                WindowTitle,
                "未保存の変更があります。",
                "保存して続ける",
                "キャンセル",
                "変更を破棄");
            if (choice == 0)
            {
                return SaveDocument();
            }

            return choice == 2;
        }

        private bool LoadFile(string assetPath)
        {
            try
            {
                string absolutePath = GetAbsolutePath(assetPath);
                byte[] bytes = File.ReadAllBytes(absolutePath);
                bool hasBom = bytes.Length >= 3 &&
                              bytes[0] == 0xEF &&
                              bytes[1] == 0xBB &&
                              bytes[2] == 0xBF;
                int offset = hasBom ? 3 : 0;
                string text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);

                document = EventCreationYarnDocument.Parse(text, hasBom);
                loadedText = text;
                workingText = text;
                workingTextHasUtf8Bom = hasBom;
                selectedAssetPath = assetPath.Replace('\\', '/');
                EditorPrefs.SetString(LastAssetPathKey, selectedAssetPath);
                selectedNodeIndex = Mathf.Clamp(
                    selectedNodeIndex,
                    0,
                    Math.Max(0, document.Nodes.Count - 1));
                selectedSourceLine = -1;
                isDirty = false;
                statusMessage = string.Empty;
                RefreshDialogueLines();
                Repaint();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    WindowTitle,
                    $"Yarnファイルを読み込めませんでした。\n\n{exception.Message}",
                    "OK");
                return false;
            }
        }

        private bool SaveDocument()
        {
            if (document == null || string.IsNullOrEmpty(selectedAssetPath))
            {
                return false;
            }

            List<EventCreationValidationMessage> validation = document.Validate();
            EventCreationValidationMessage firstError = validation.FirstOrDefault(message => message.IsError);
            if (firstError.IsError)
            {
                EditorUtility.DisplayDialog(
                    WindowTitle,
                    $"保存前に問題を直してください。\n\n{firstError.Text}",
                    "OK");
                return false;
            }

            try
            {
                string absolutePath = GetAbsolutePath(selectedAssetPath);
                string diskText = ReadUtf8Text(absolutePath);
                if (!string.Equals(diskText, loadedText, StringComparison.Ordinal))
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        WindowTitle,
                        "読み込み後に、別の場所からファイルが変更されています。",
                        "現在の編集内容で上書き",
                        "キャンセル",
                        "外部の変更を再読込");
                    if (choice == 1)
                    {
                        return false;
                    }
                    if (choice == 2)
                    {
                        LoadFile(selectedAssetPath);
                        return false;
                    }
                }

                string backupPath = CreateBackup(absolutePath);
                string nextText = document.ToText();
                File.WriteAllText(
                    absolutePath,
                    nextText,
                    new UTF8Encoding(document.HasUtf8Bom));
                AssetDatabase.ImportAsset(
                    selectedAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                loadedText = nextText;
                workingText = nextText;
                workingTextHasUtf8Bom = document.HasUtf8Bom;
                isDirty = false;
                SetStatus(
                    $"保存しました。バックアップ: {backupPath.Replace('\\', '/')}",
                    MessageType.Info);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    WindowTitle,
                    $"保存できませんでした。\n\n{exception.Message}",
                    "OK");
                return false;
            }
        }

        private static string CreateBackup(string sourceAbsolutePath)
        {
            string backupDirectory = Path.GetFullPath(
                Path.Combine("Library", "EventCreationKunBackups"));
            Directory.CreateDirectory(backupDirectory);
            string fileName = Path.GetFileNameWithoutExtension(sourceAbsolutePath);
            string backupName = $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.yarn";
            string backupPath = Path.Combine(backupDirectory, backupName);
            File.Copy(sourceAbsolutePath, backupPath, overwrite: false);
            return GetProjectRelativeDisplayPath(backupPath);
        }

        private static string ReadUtf8Text(string absolutePath)
        {
            byte[] bytes = File.ReadAllBytes(absolutePath);
            int offset = bytes.Length >= 3 &&
                         bytes[0] == 0xEF &&
                         bytes[1] == 0xBB &&
                         bytes[2] == 0xBF
                ? 3
                : 0;
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }

        private static string GetAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetProjectRelativeDisplayPath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar) +
                                 Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(absolutePath);
            return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(projectRoot.Length)
                : fullPath;
        }

        private static bool IsYarnAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                   path.EndsWith(".yarn", StringComparison.OrdinalIgnoreCase);
        }

        private void HandleSaveShortcut()
        {
            Event currentEvent = Event.current;
            if (currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode != KeyCode.S ||
                (!currentEvent.control && !currentEvent.command))
            {
                return;
            }

            if (isDirty)
            {
                SaveDocument();
            }
            currentEvent.Use();
        }

        private void SetStatus(string message, MessageType type)
        {
            statusMessage = message ?? string.Empty;
            statusType = type;
            Repaint();
        }

        private void OnBeforeAssemblyReload()
        {
            skipDestroyPrompt = true;
        }

        private void CaptureDirtyWorkingText()
        {
            if (document == null)
            {
                return;
            }

            workingText = document.ToText();
            workingTextHasUtf8Bom = document.HasUtf8Bom;
            isDirty = true;
        }

        private sealed class NewEventNameWindow : EditorWindow
        {
            private const string NameControl = "EventCreationKun.NewEventName";

            private EventCreationWindow owner;
            private string eventName = string.Empty;
            private bool focusApplied;

            public static void Open(EventCreationWindow owner, string currentName)
            {
                NewEventNameWindow window = CreateInstance<NewEventNameWindow>();
                window.owner = owner;
                window.eventName = currentName ?? string.Empty;
                window.titleContent = new GUIContent("新しいイベント");
                window.minSize = new Vector2(420f, 125f);
                window.maxSize = new Vector2(420f, 125f);
                window.ShowUtility();
                window.Focus();
            }

            private void OnGUI()
            {
                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("新しいイベント名を入力してください。", EditorStyles.boldLabel);
                EditorGUILayout.Space(4f);

                GUI.SetNextControlName(NameControl);
                eventName = EditorGUILayout.TextField("イベント名", eventName);
                if (!focusApplied)
                {
                    EditorGUI.FocusTextInControl(NameControl);
                    focusApplied = true;
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("キャンセル", GUILayout.Width(90f)))
                    {
                        Close();
                    }

                    if (GUILayout.Button("追加", GUILayout.Width(90f)))
                    {
                        TryAdd();
                    }
                }

                Event currentEvent = Event.current;
                if (currentEvent != null &&
                    currentEvent.type == EventType.KeyDown &&
                    (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter))
                {
                    currentEvent.Use();
                    TryAdd();
                }
            }

            private void TryAdd()
            {
                if (string.IsNullOrWhiteSpace(eventName))
                {
                    EditorUtility.DisplayDialog(
                        WindowTitle,
                        "イベント名を入力してください。",
                        "OK");
                    Focus();
                    EditorGUI.FocusTextInControl(NameControl);
                    return;
                }

                if (owner != null && owner.AddNode(eventName))
                {
                    Close();
                }
            }
        }
    }
}
