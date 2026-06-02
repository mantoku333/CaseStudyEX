using System;
using UnityEditor;
using UnityEngine;

namespace CaseStudy.EditorTools
{
    public sealed class EventPlaybackWindow : EditorWindow
    {
        private const string WindowTitle = "イベント再生くん";

        [SerializeField] private StoryEventController selectedController;

        private StoryEventController[] controllers = Array.Empty<StoryEventController>();
        private Vector2 scrollPosition;

        [MenuItem("Tools/CaseStudy/イベント再生くん")]
        public static void Open()
        {
            EventPlaybackWindow window = GetWindow<EventPlaybackWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.RefreshControllers();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            RefreshControllers();
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

            DrawControllerList();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Modeで使えます。中身はInspectorのPlay Event / Stop Eventと同じです。", MessageType.Info);
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
                    RefreshControllers();
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

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

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

            EditorGUILayout.EndScrollView();
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
            RefreshControllers();
            TryUseSelection();
            Repaint();
        }
    }
}
