using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Metroidvania.Managers;
using UnityEditor;
using UnityEngine;

namespace CaseStudy.EditorTools
{
    public sealed class EventPlaybackWindow : EditorWindow
    {
        private const string WindowTitle = "イベント再生くん";

        [SerializeField] private StoryEventController selectedController;

        private StoryEventController[] controllers = Array.Empty<StoryEventController>();
        private YarnEventEntry[] yarnEvents = Array.Empty<YarnEventEntry>();
        private Vector2 scrollPosition;

        [MenuItem("Tools/CaseStudy/イベント再生くん")]
        public static void Open()
        {
            EventPlaybackWindow window = GetWindow<EventPlaybackWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.RefreshAllEvents();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            RefreshAllEvents();
            TryUseSelection();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnHierarchyChange()
        {
            RefreshControllers();
            Repaint();
        }

        private void OnProjectChange()
        {
            RefreshYarnEvents();
            Repaint();
        }

        private void OnSelectionChange()
        {
            TryUseSelection();
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(6f);

            DrawTargetPicker();
            EditorGUILayout.Space(8f);

            DrawPlaybackButtons();
            EditorGUILayout.Space(8f);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawControllerList();
            EditorGUILayout.Space(12f);
            DrawYarnEventList();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play Modeで使えます。シーンイベントと、イベント作成くんで保存したADV会話を確認できます。",
                    MessageType.Info);
            }
        }

        private void DrawTargetPicker()
        {
            EditorGUI.BeginChangeCheck();
            selectedController = (StoryEventController)EditorGUILayout.ObjectField(
                "Target",
                selectedController,
                typeof(StoryEventController),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("選択中を使う"))
                {
                    TryUseSelection();
                }

                if (GUILayout.Button("一覧更新"))
                {
                    RefreshAllEvents();
                }
            }

            StoryEventController currentController = selectedController;
            if (currentController == null)
            {
                EditorGUILayout.HelpBox("再生するイベントを選んでください。", MessageType.Warning);
                return;
            }

            DrawSelectedControllerInfo(currentController);
        }

        private static void DrawSelectedControllerInfo(StoryEventController currentController)
        {
            bool hasMemoName = !string.IsNullOrWhiteSpace(currentController.MemoName);
            Rect boxRect = EditorGUILayout.GetControlRect(false, hasMemoName ? 44f : 24f);
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(
                boxRect.x + 8f,
                boxRect.y + 6f,
                boxRect.width - 16f,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(lineRect, "Event ID", currentController.EventId);
            lineRect.y += EditorGUIUtility.singleLineHeight + 2f;
            if (hasMemoName)
            {
                EditorGUI.LabelField(lineRect, "メモ", currentController.MemoName);
            }
        }

        private void DrawPlaybackButtons()
        {
            bool canUse = EditorApplication.isPlaying && selectedController != null;

            using (new EditorGUI.DisabledScope(!canUse))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play Event", GUILayout.Height(28f)))
                {
                    bool played = selectedController.PlayEvent();
                    if (!played)
                    {
                        Debug.LogWarning(
                            $"[EventPlaybackWindow] PlayEvent failed. eventId='{selectedController.EventId}'",
                            selectedController);
                    }
                }

                if (GUILayout.Button("Stop Event", GUILayout.Height(28f)))
                {
                    selectedController.StopEvent();
                }
            }

            if (selectedController != null && !selectedController.gameObject.activeInHierarchy)
            {
                EditorGUILayout.HelpBox("TargetのGameObjectが非アクティブです。PlayEventは実行できない可能性があります。", MessageType.Warning);
            }
        }

        private void DrawControllerList()
        {
            EditorGUILayout.LabelField("Scene Events", EditorStyles.boldLabel);

            if (controllers.Length == 0)
            {
                EditorGUILayout.HelpBox("シーン内にStoryEventControllerがありません。", MessageType.None);
                return;
            }

            for (int i = 0; i < controllers.Length; i++)
            {
                StoryEventController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                bool isSelected = controller == selectedController;
                string label = BuildControllerLabel(controller);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(isSelected, label, "Button"))
                    {
                        selectedController = controller;
                    }

                    if (GUILayout.Button("Select", GUILayout.Width(58f)))
                    {
                        selectedController = controller;
                        Selection.activeObject = controller.gameObject;
                        EditorGUIUtility.PingObject(controller.gameObject);
                    }
                }
            }
        }

        private void DrawYarnEventList()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("作成したADVイベント", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("会話を停止", GUILayout.Width(90f)))
                    {
                        StopYarnDialogue();
                    }
                }
            }

            EditorGUILayout.LabelField(
                "イベント作成くんで保存した内容です。ここからはADV会話部分だけを直接確認できます。",
                EditorStyles.miniLabel);

            if (yarnEvents.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "保存済みのYarnイベントがありません。イベント作成くんで保存してから一覧更新してください。",
                    MessageType.None);
                return;
            }

            for (int i = 0; i < yarnEvents.Length; i++)
            {
                YarnEventEntry yarnEvent = yarnEvents[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        yarnEvent.DisplayLabel,
                        GUILayout.MinWidth(240f));
                    using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                    {
                        if (GUILayout.Button("ADV再生", GUILayout.Width(76f)))
                        {
                            PlayYarnEvent(yarnEvent);
                        }
                    }
                }
            }
        }

        private static string BuildControllerLabel(StoryEventController controller)
        {
            string memoName = controller.MemoName;
            return string.IsNullOrWhiteSpace(memoName)
                ? controller.EventId
                : $"{controller.EventId} / {memoName}";
        }

        private void TryUseSelection()
        {
            StoryEventController controller = ResolveSelectionController();
            if (controller == null)
            {
                return;
            }

            selectedController = controller;
        }

        private static StoryEventController ResolveSelectionController()
        {
            if (Selection.activeGameObject == null)
            {
                return null;
            }

            return Selection.activeGameObject.GetComponentInParent<StoryEventController>();
        }

        private void RefreshControllers()
        {
            controllers = FindObjectsByType<StoryEventController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Array.Sort(controllers, CompareControllers);

            if (selectedController == null && controllers.Length == 1)
            {
                selectedController = controllers[0];
            }
        }

        private void RefreshAllEvents()
        {
            RefreshControllers();
            RefreshYarnEvents();
            Repaint();
        }

        private void RefreshYarnEvents()
        {
            var entries = new List<YarnEventEntry>();
            string[] assetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                               path.EndsWith(".yarn", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int pathIndex = 0; pathIndex < assetPaths.Length; pathIndex++)
            {
                string assetPath = assetPaths[pathIndex];
                try
                {
                    string source = File.ReadAllText(
                        Path.GetFullPath(assetPath.Replace('/', Path.DirectorySeparatorChar)),
                        Encoding.UTF8);
                    EventCreationYarnDocument yarnDocument = EventCreationYarnDocument.Parse(source);
                    for (int nodeIndex = 0; nodeIndex < yarnDocument.Nodes.Count; nodeIndex++)
                    {
                        EventCreationYarnNode node = yarnDocument.Nodes[nodeIndex];
                        if (!string.IsNullOrWhiteSpace(node.Title))
                        {
                            entries.Add(new YarnEventEntry(assetPath, node.Title.Trim()));
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[EventPlaybackWindow] Yarnファイルを一覧へ追加できませんでした。" +
                        $" path='{assetPath}', error='{exception.Message}'");
                }
            }

            yarnEvents = entries
                .OrderBy(entry => entry.NodeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.AssetPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void PlayYarnEvent(YarnEventEntry yarnEvent)
        {
            DialogueManager dialogueManager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (dialogueManager == null)
            {
                Debug.LogWarning(
                    $"[EventPlaybackWindow] DialogueManagerがシーンにありません。node='{yarnEvent.NodeName}'");
                return;
            }

            if (dialogueManager.Runner != null &&
                dialogueManager.Runner.Dialogue != null &&
                !dialogueManager.Runner.Dialogue.NodeExists(yarnEvent.NodeName))
            {
                Debug.LogWarning(
                    $"[EventPlaybackWindow] Yarnノードが実行中のYarnProjectにありません。" +
                    $" 保存とインポートを確認してください。node='{yarnEvent.NodeName}'");
                return;
            }

            dialogueManager.StartConversation(yarnEvent.NodeName, DialogueStyle.ADV);
        }

        private static void StopYarnDialogue()
        {
            DialogueManager dialogueManager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (dialogueManager != null &&
                dialogueManager.Runner != null &&
                dialogueManager.Runner.IsDialogueRunning)
            {
                dialogueManager.Runner.Stop();
            }
        }

        private static int CompareControllers(StoryEventController left, StoryEventController right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int eventIdCompare = string.Compare(left.EventId, right.EventId, StringComparison.OrdinalIgnoreCase);
            return eventIdCompare != 0
                ? eventIdCompare
                : string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            RefreshAllEvents();
            TryUseSelection();
            Repaint();
        }

        private sealed class YarnEventEntry
        {
            public YarnEventEntry(string assetPath, string nodeName)
            {
                AssetPath = assetPath;
                NodeName = nodeName;
            }

            public string AssetPath { get; }
            public string NodeName { get; }
            public string DisplayLabel => $"{NodeName}  —  {Path.GetFileNameWithoutExtension(AssetPath)}";
        }
    }
}
