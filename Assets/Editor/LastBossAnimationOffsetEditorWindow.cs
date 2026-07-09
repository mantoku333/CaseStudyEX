using System.Collections.Generic;
using GameName.Enemy;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EditorTools
{
    public sealed class LastBossAnimationOffsetEditorWindow : EditorWindow
    {
        private const string LastBossPrefabPath = "Assets/Prefabs/Enemies/LastBoss.prefab";
        private const float WindowMinWidth = 980f;
        private const float WindowMinHeight = 480f;
        private const float InspectorWidth = 560f;
        private const float InspectorMinWidth = 480f;
        private const float PreviewMinWidth = 260f;
        private const float FrameGoWidth = 32f;
        private const float MotionOrderWidth = 58f;
        private const float MotionOrderButtonWidth = 27f;
        private const float FrameTimeWidth = 78f;
        private const float FrameSpriteWidth = 174f;
        private const float FrameAxisLabelWidth = 12f;
        private const float FrameFloatWidth = 58f;
        private const int PreviewGridLineCount = 96;

        private static readonly StateBinding[] StateBindings =
        {
            new StateBinding(LastBossSpriteAnimator.AnimationState.Idle, "Idle", "idleOffset", "idleStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.Move, "Move", "moveOffset", "moveStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.NormalAttack, "Normal Attack", "normalAttackOffset", "normalAttackStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.HorizontalStart, "Horizontal Start", "horizontalStartOffset", "horizontalStartStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.HorizontalEnd, "Horizontal End", "horizontalEndOffset", "horizontalEndStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.VerticalStart, "Vertical Start", "verticalStartOffset", "verticalStartStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.VerticalEnd, "Vertical End", "verticalEndOffset", "verticalEndStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.DownStart, "Down Start", "downStartOffset", "downStartStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.DownHold, "Down Hold", "downHoldOffset", "downHoldStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.DownEnd, "Down End", "downEndOffset", "downEndStateName"),
            new StateBinding(LastBossSpriteAnimator.AnimationState.Dead, "Dead", "deadOffset", null)
        };

        [SerializeField] private GameObject targetPrefab;
        [SerializeField] private LastBossSpriteAnimator.AnimationState previewState = LastBossSpriteAnimator.AnimationState.Idle;
        [SerializeField] private float previewTime;
        [SerializeField] private float previewPixelsPerUnit = 1f;
        [SerializeField] private float previewSpeed = 1f;
        [SerializeField] private bool animatePreview = true;
        [SerializeField] private bool showFrameOffsets;

        private LastBossSpriteAnimator targetAnimator;
        private SerializedObject serializedTarget;
        private Vector2 scrollPosition;
        private double lastEditorTime;
        private PreviewRenderUtility previewUtility;
        private GameObject previewInstance;
        private Animator previewAnimator;
        private LastBossSpriteAnimator previewSpriteAnimator;
        private Transform previewSpriteTransform;
        private Vector3 previewSpriteBaseLocalPosition;
        private GameObject previewGridRoot;
        private LineRenderer[] previewGridLines;
        private Material previewGridMaterial;

        [MenuItem("Tools/CaseStudy/Last Boss/Animation Offset Editor")]
        public static void Open()
        {
            LastBossAnimationOffsetEditorWindow window =
                GetWindow<LastBossAnimationOffsetEditorWindow>("Last Boss Offsets");
            window.minSize = new Vector2(WindowMinWidth, WindowMinHeight);
            window.LoadDefaultPrefabIfNeeded();
        }

        private void OnEnable()
        {
            minSize = new Vector2(WindowMinWidth, WindowMinHeight);

            if (previewPixelsPerUnit > 3f)
            {
                previewPixelsPerUnit = 1f;
            }

            previewSpeed = Mathf.Clamp(previewSpeed, 0f, 3f);
            LoadDefaultPrefabIfNeeded();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            lastEditorTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DestroyPreviewInstance();
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void OnGUI()
        {
            LoadDefaultPrefabIfNeeded();

            using (new EditorGUILayout.HorizontalScope())
            {
                Rect previewPanelRect = EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawPreviewPanel(previewPanelRect);
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6f);

                float maxRightWidth = Mathf.Max(InspectorMinWidth, position.width - PreviewMinWidth - 6f);
                float rightWidth = Mathf.Min(InspectorWidth, maxRightWidth);
                EditorGUILayout.BeginVertical(GUILayout.Width(rightWidth), GUILayout.ExpandHeight(true));
                DrawInspectorPanel();
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawTargetControls()
        {
            EditorGUILayout.LabelField("Prefab Target", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab",
                targetPrefab,
                typeof(GameObject),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                SetTargetPrefab(newPrefab);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use LastBoss Prefab"))
                {
                    SetTargetPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LastBossPrefabPath));
                }

                using (new EditorGUI.DisabledScope(targetAnimator == null))
                {
                    if (GUILayout.Button("Ping Prefab"))
                    {
                        EditorGUIUtility.PingObject(targetPrefab);
                    }
                }
            }
        }

        private void DrawPreviewPanel(Rect panelRect)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (targetAnimator == null)
            {
                EditorGUILayout.HelpBox("LastBoss prefab with LastBossSpriteAnimator is required.", MessageType.Warning);
                return;
            }

            RefreshSerializedTarget();
            serializedTarget.Update();
            AnimationClip clip = ResolveClip(previewState);
            float clipLength = clip != null ? Mathf.Max(clip.length, 0.001f) : 0f;

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                previewState = (LastBossSpriteAnimator.AnimationState)EditorGUILayout.EnumPopup("State", previewState);
                animatePreview = EditorGUILayout.ToggleLeft("Animate", animatePreview, GUILayout.Width(78f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                previewPixelsPerUnit = EditorGUILayout.Slider("Zoom", previewPixelsPerUnit, 0.25f, 3f);
                if (GUILayout.Button("Fit", GUILayout.Width(46f)))
                {
                    FitPreviewZoom(clip, panelRect);
                }
            }

            previewSpeed = EditorGUILayout.Slider("Speed", previewSpeed, 0f, 3f);

            using (new EditorGUI.DisabledScope(clipLength <= 0f || animatePreview))
            {
                int currentFrame = TimeToDisplayFrame(previewTime, clip);
                int nextFrame = EditorGUILayout.IntSlider("Frame", currentFrame, 1, GetClipFrameCount(clip));
                if (nextFrame != currentFrame)
                {
                    previewTime = DisplayFrameToTime(nextFrame, clip);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                previewTime = clipLength > 0f ? Mathf.Repeat(previewTime, clipLength) : 0f;
                Repaint();
            }

            Rect previewRect = GUILayoutUtility.GetRect(
                10f,
                Mathf.Max(220f, position.height - 126f),
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            DrawPreview(previewRect, clip);

            string clipName = clip != null ? clip.name : "(no animation clip)";
            EditorGUILayout.LabelField("Clip", clipName);
        }

        private void DrawInspectorPanel()
        {
            DrawTargetControls();

            if (targetAnimator == null)
            {
                return;
            }

            RefreshSerializedTarget();
            serializedTarget.Update();

            EditorGUILayout.LabelField("Animation Offsets", EditorStyles.boldLabel);
            AnimationClip clip = ResolveClip(previewState);
            string clipName = clip != null ? clip.name : "(no animation clip)";
            EditorGUILayout.LabelField("State", previewState.ToString());
            EditorGUILayout.LabelField("Clip", clipName);

            EditorGUI.BeginChangeCheck();
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 118f;

            StateBinding binding = GetBinding(previewState);
            SerializedProperty stateBaseOffsetProperty = serializedTarget.FindProperty(binding.OffsetPropertyName);
            if (stateBaseOffsetProperty != null)
            {
                DrawVector3Property("Base Offset", stateBaseOffsetProperty);
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            DrawMotionKeySection(previewState, clip);

            EditorGUILayout.Space(8f);
            showFrameOffsets = EditorGUILayout.Foldout(showFrameOffsets, "Per-Frame Offsets (Advanced)", true);
            SerializedProperty frameSetProperty = FindFrameOffsetSetProperty(previewState);
            if (showFrameOffsets)
            {
                if (frameSetProperty == null)
                {
                    EditorGUILayout.HelpBox("No frame offset table for this state. Sync frames from the animation clip to start editing per-frame offsets.", MessageType.Info);
                }
                else
                {
                    DrawFrameOffsetTable(frameSetProperty);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUIUtility.labelWidth = previousLabelWidth;

            if (EditorGUI.EndChangeCheck())
            {
                serializedTarget.ApplyModifiedProperties();
                MarkPrefabDirty();
                Repaint();
            }
            else
            {
                serializedTarget.ApplyModifiedProperties();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Frames From Clip"))
                {
                    SyncFrameOffsetsFromClip(previewState, clip);
                }

                if (GUILayout.Button("Reset State Frames"))
                {
                    ResetStateFrameOffsets(previewState);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset All Offsets"))
                {
                    ResetAllOffsets();
                }

                if (GUILayout.Button("Save Prefab"))
                {
                    SavePrefab();
                }
            }
        }

        private void DrawPreview(Rect rect, AnimationClip clip)
        {
            if (Event.current.type != EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewInstance();
            if (previewUtility == null || previewInstance == null || previewSpriteAnimator == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));
                GUI.Label(rect, "Preview instance could not be created.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            SamplePreviewInstance(clip);
            Bounds bounds = CalculatePreviewBounds();
            ConfigurePreviewCamera(bounds, rect);
            UpdatePreviewGrid(rect, previewUtility.camera);

            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.camera.Render();
            Texture previewTexture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, previewTexture, ScaleMode.StretchToFill, false);

            Rect labelRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 18f);
            string label = $"Base {FormatVector(previewSpriteBaseLocalPosition)}  Offset {FormatVector(GetPreviewOffset(previewState, previewTime))}";
            EditorGUI.LabelField(labelRect, label, EditorStyles.whiteMiniLabel);
        }

        private void UpdatePreviewGrid(Rect rect, Camera camera)
        {
            if (camera == null || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            EnsurePreviewGrid();
            if (previewGridLines == null)
            {
                return;
            }

            float aspect = rect.width / Mathf.Max(1f, rect.height);
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * aspect;
            Vector3 center = camera.transform.position;
            float minX = center.x - halfWidth;
            float maxX = center.x + halfWidth;
            float minY = center.y - halfHeight;
            float maxY = center.y + halfHeight;
            float gridStep = CalculateGridStep(Mathf.Max(maxX - minX, maxY - minY));
            float lineZ = center.z + 10f;
            float worldLineWidth = Mathf.Max(0.01f, halfHeight * 2f / Mathf.Max(1f, rect.height));
            int lineIndex = 0;

            Color gridColor = new Color(1f, 1f, 1f, 0.11f);
            Color axisColor = new Color(0.35f, 0.75f, 1f, 0.82f);

            float firstX = Mathf.Floor(minX / gridStep) * gridStep;
            for (float worldX = firstX; worldX <= maxX; worldX += gridStep)
            {
                SetPreviewGridLine(lineIndex++, new Vector3(worldX, minY, lineZ), new Vector3(worldX, maxY, lineZ), gridColor, worldLineWidth, -10000);
            }

            float firstY = Mathf.Floor(minY / gridStep) * gridStep;
            for (float worldY = firstY; worldY <= maxY; worldY += gridStep)
            {
                SetPreviewGridLine(lineIndex++, new Vector3(minX, worldY, lineZ), new Vector3(maxX, worldY, lineZ), gridColor, worldLineWidth, -10000);
            }

            if (minX <= 0f && maxX >= 0f)
            {
                SetPreviewGridLine(lineIndex++, new Vector3(0f, minY, lineZ), new Vector3(0f, maxY, lineZ), axisColor, worldLineWidth * 2f, -9999);
            }

            for (int i = lineIndex; i < previewGridLines.Length; i++)
            {
                previewGridLines[i].enabled = false;
            }
        }

        private void EnsurePreviewGrid()
        {
            if (previewGridRoot != null && previewGridLines != null)
            {
                return;
            }

            EnsurePreviewUtility();
            if (previewUtility == null)
            {
                return;
            }

            previewGridRoot = new GameObject("LastBossPreviewGrid");
            previewGridRoot.hideFlags = HideFlags.HideAndDontSave;
            previewGridLines = new LineRenderer[PreviewGridLineCount];
            previewGridMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int i = 0; i < previewGridLines.Length; i++)
            {
                GameObject lineObject = new GameObject("Line");
                lineObject.hideFlags = HideFlags.HideAndDontSave;
                lineObject.transform.SetParent(previewGridRoot.transform, false);

                LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
                lineRenderer.hideFlags = HideFlags.HideAndDontSave;
                lineRenderer.useWorldSpace = true;
                lineRenderer.positionCount = 2;
                lineRenderer.material = previewGridMaterial;
                lineRenderer.numCapVertices = 0;
                lineRenderer.numCornerVertices = 0;
                lineRenderer.sortingOrder = -10000;
                lineRenderer.enabled = false;
                previewGridLines[i] = lineRenderer;
            }

            previewUtility.AddSingleGO(previewGridRoot);
        }

        private void SetPreviewGridLine(int index, Vector3 start, Vector3 end, Color color, float width, int sortingOrder)
        {
            if (previewGridLines == null || index < 0 || index >= previewGridLines.Length)
            {
                return;
            }

            LineRenderer lineRenderer = previewGridLines[index];
            lineRenderer.enabled = true;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.sortingOrder = sortingOrder;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        private static float CalculateGridStep(float visibleWorldSize)
        {
            float rawStep = Mathf.Max(0.01f, visibleWorldSize / 12f);
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rawStep)));
            float normalized = rawStep / magnitude;

            if (normalized <= 1f)
            {
                return magnitude;
            }

            if (normalized <= 2f)
            {
                return 2f * magnitude;
            }

            if (normalized <= 5f)
            {
                return 5f * magnitude;
            }

            return 10f * magnitude;
        }

        private static float WorldToPreviewX(Rect rect, float minX, float maxX, float worldX)
        {
            float t = Mathf.InverseLerp(minX, maxX, worldX);
            return Mathf.Lerp(rect.xMin, rect.xMax, t);
        }

        private static float WorldToPreviewY(Rect rect, float minY, float maxY, float worldY)
        {
            float t = Mathf.InverseLerp(minY, maxY, worldY);
            return Mathf.Lerp(rect.yMax, rect.yMin, t);
        }

        private static void DrawCross(Rect rect, Vector2 origin, Color color)
        {
            DrawLine(new Rect(rect.xMin, origin.y, rect.width, 1f), color);
            DrawLine(new Rect(origin.x, rect.yMin, 1f, rect.height), color);
        }

        private static void DrawLine(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
        }

        private void DrawSprite(Sprite sprite, Vector2 origin, Vector3 localPosition, Color color)
        {
            Rect textureRect = sprite.textureRect;
            Rect drawRect = GetSpriteDrawRect(sprite, origin, localPosition);
            Rect texCoords = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);

            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, texCoords, true);
            GUI.color = previousColor;
        }

        private Rect GetSpriteDrawRect(Sprite sprite, Vector2 origin, Vector3 localPosition)
        {
            float pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
            Vector2 pivot = sprite.pivot;
            Rect spriteRect = sprite.rect;

            Vector2 pivotScreen = new Vector2(
                origin.x + localPosition.x * previewPixelsPerUnit,
                origin.y - localPosition.y * previewPixelsPerUnit);

            float width = spriteRect.width / pixelsPerUnit * previewPixelsPerUnit;
            float height = spriteRect.height / pixelsPerUnit * previewPixelsPerUnit;
            float x = pivotScreen.x - pivot.x / pixelsPerUnit * previewPixelsPerUnit;
            float y = pivotScreen.y - (spriteRect.height - pivot.y) / pixelsPerUnit * previewPixelsPerUnit;
            return new Rect(x, y, width, height);
        }

        private void FitPreviewZoom(AnimationClip clip, Rect panelRect)
        {
            previewPixelsPerUnit = 1f;
            Repaint();
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.orthographic = true;
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
            previewUtility.camera.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 1f);
            previewUtility.camera.nearClipPlane = 0.01f;
            previewUtility.camera.farClipPlane = 100f;
            previewUtility.lights[0].intensity = 1f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            previewUtility.lights[1].intensity = 0.5f;
        }

        private void EnsurePreviewInstance()
        {
            if (previewInstance != null || targetPrefab == null)
            {
                return;
            }

            EnsurePreviewUtility();
            previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab);
            if (previewInstance == null)
            {
                previewInstance = Instantiate(targetPrefab);
            }

            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            previewInstance.transform.position = Vector3.zero;
            previewInstance.transform.rotation = Quaternion.identity;
            SetHideFlagsRecursive(previewInstance, HideFlags.HideAndDontSave);
            previewUtility.AddSingleGO(previewInstance);

            previewSpriteAnimator = previewInstance.GetComponentInChildren<LastBossSpriteAnimator>(true);
            previewSpriteTransform = previewSpriteAnimator != null ? previewSpriteAnimator.transform : null;
            previewSpriteBaseLocalPosition = previewSpriteTransform != null
                ? previewSpriteTransform.localPosition
                : Vector3.zero;

            previewAnimator = previewInstance.GetComponentInChildren<Animator>(true);
            if (previewAnimator != null)
            {
                previewAnimator.enabled = false;
            }
        }

        private static void SetHideFlagsRecursive(GameObject root, HideFlags hideFlags)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = hideFlags;
            }
        }

        private void DestroyPreviewInstance()
        {
            if (previewInstance == null)
            {
                return;
            }

            DestroyImmediate(previewInstance);
            previewInstance = null;
            previewAnimator = null;
            previewSpriteAnimator = null;
            previewSpriteTransform = null;
            previewSpriteBaseLocalPosition = Vector3.zero;

            if (previewGridRoot != null)
            {
                DestroyImmediate(previewGridRoot);
                previewGridRoot = null;
                previewGridLines = null;
            }

            if (previewGridMaterial != null)
            {
                DestroyImmediate(previewGridMaterial);
                previewGridMaterial = null;
            }
        }

        private void SamplePreviewInstance(AnimationClip clip)
        {
            if (previewInstance == null || previewSpriteTransform == null)
            {
                return;
            }

            if (clip != null)
            {
                GameObject sampleRoot = previewAnimator != null ? previewAnimator.gameObject : previewSpriteTransform.gameObject;
                float sampleTime = clip.length > 0f ? Mathf.Repeat(previewTime, clip.length) : 0f;
                clip.SampleAnimation(sampleRoot, sampleTime);
            }

            previewSpriteTransform.localPosition = previewSpriteBaseLocalPosition + GetPreviewOffset(previewState, previewTime);
        }

        private Bounds CalculatePreviewBounds()
        {
            Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(Vector3.zero, Vector3.one);
            }

            if (bounds.size.sqrMagnitude < 0.0001f)
            {
                bounds.size = Vector3.one;
            }

            return bounds;
        }

        private void ConfigurePreviewCamera(Bounds bounds, Rect rect)
        {
            float aspect = Mathf.Max(0.01f, rect.width / Mathf.Max(1f, rect.height));
            float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect);
            halfHeight = Mathf.Max(0.1f, halfHeight * 1.18f / Mathf.Max(0.1f, previewPixelsPerUnit));

            Camera camera = previewUtility.camera;
            camera.orthographic = true;
            camera.orthographicSize = halfHeight;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 10f);
            camera.transform.rotation = Quaternion.identity;
        }

        private Sprite ResolvePreviewSprite(AnimationClip clip, float time)
        {
            Sprite frameSprite = ResolveSpriteFromClip(clip, time);
            if (frameSprite != null)
            {
                return frameSprite;
            }

            SpriteRenderer renderer = targetAnimator.MainRenderer;
            return renderer != null ? renderer.sprite : null;
        }

        private static Sprite ResolveSpriteFromClip(AnimationClip clip, float time)
        {
            if (clip == null)
            {
                return null;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                {
                    continue;
                }

                ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (frames == null || frames.Length == 0)
                {
                    return null;
                }

                Sprite selectedSprite = null;
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    if (frames[frameIndex].time > time)
                    {
                        break;
                    }

                    selectedSprite = frames[frameIndex].value as Sprite;
                }

                return selectedSprite != null ? selectedSprite : frames[0].value as Sprite;
            }

            return null;
        }

        private void DrawMotionKeySection(LastBossSpriteAnimator.AnimationState state, AnimationClip clip)
        {
            EditorGUILayout.LabelField("Motion Keys", EditorStyles.boldLabel);
            SerializedProperty motionSetProperty = FindMotionOffsetSetProperty(state);
            if (motionSetProperty == null)
            {
                EditorGUILayout.HelpBox("Create a few keys, then edit their offsets. The offset is interpolated between keys.", MessageType.Info);
                if (GUILayout.Button("Create 3 Motion Keys"))
                {
                    CreateMotionKeys(state, clip, 3);
                }

                return;
            }

            SyncMotionSetMetadata(motionSetProperty, state, clip);
            SerializedProperty keysProperty = motionSetProperty.FindPropertyRelative("keys");
            EditorGUILayout.LabelField("Keys", keysProperty != null ? keysProperty.arraySize.ToString() : "0");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Key At Frame"))
                {
                    AddMotionKeyAtFrame(state, clip);
                }

                if (GUILayout.Button("Create 3 Keys"))
                {
                    CreateMotionKeys(state, clip, 3);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sort by Frame"))
                {
                    SortMotionKeys(keysProperty);
                }

                if (GUILayout.Button("Reset State Motion"))
                {
                    ResetStateMotionOffsets(state);
                }
            }

            EditorGUILayout.LabelField("Frame Range", $"1 - {GetClipFrameCount(clip)} ({GetClipFrameRate(clip):0.###} fps)");
            DrawMotionKeyTable(keysProperty, clip);
        }

        private void DrawMotionKeyTable(SerializedProperty keysProperty, AnimationClip clip)
        {
            if (keysProperty == null || !keysProperty.isArray || keysProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No motion keys for this state.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(string.Empty, GUILayout.Width(FrameGoWidth));
                GUILayout.Label("Order", GUILayout.Width(MotionOrderWidth));
                GUILayout.Label("Frame", GUILayout.Width(FrameTimeWidth));
                GUILayout.Label("Ease", GUILayout.Width(96f));
                GUILayout.Label("X", GUILayout.Width(FrameAxisLabelWidth + FrameFloatWidth));
                GUILayout.Label("Y", GUILayout.Width(FrameAxisLabelWidth + FrameFloatWidth));
                GUILayout.Label("Z", GUILayout.Width(FrameAxisLabelWidth + FrameFloatWidth));
                GUILayout.Label(string.Empty, GUILayout.Width(34f));
            }

            bool needsSort = false;
            int removeIndex = -1;
            int moveFromIndex = -1;
            int moveToIndex = -1;
            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                SerializedProperty keyProperty = keysProperty.GetArrayElementAtIndex(i);
                SerializedProperty timeProperty = keyProperty.FindPropertyRelative("time");
                SerializedProperty easeProperty = keyProperty.FindPropertyRelative("easeToNext");
                SerializedProperty offsetProperty = keyProperty.FindPropertyRelative("offset");
                if (timeProperty == null || easeProperty == null || offsetProperty == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Go", GUILayout.Width(FrameGoWidth)))
                    {
                        previewTime = timeProperty.floatValue;
                        animatePreview = false;
                        Repaint();
                    }

                    using (new EditorGUILayout.HorizontalScope(GUILayout.Width(MotionOrderWidth)))
                    {
                        using (new EditorGUI.DisabledScope(i <= 0))
                        {
                            if (GUILayout.Button("Up", GUILayout.Width(MotionOrderButtonWidth)))
                            {
                                moveFromIndex = i;
                                moveToIndex = i - 1;
                            }
                        }

                        using (new EditorGUI.DisabledScope(i >= keysProperty.arraySize - 1))
                        {
                            if (GUILayout.Button("Dn", GUILayout.Width(MotionOrderButtonWidth)))
                            {
                                moveFromIndex = i;
                                moveToIndex = i + 1;
                            }
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    int currentFrame = TimeToDisplayFrame(timeProperty.floatValue, clip);
                    int newFrame = EditorGUILayout.IntField(currentFrame, GUILayout.Width(FrameTimeWidth));
                    if (EditorGUI.EndChangeCheck())
                    {
                        timeProperty.floatValue = DisplayFrameToTime(newFrame, clip);
                        needsSort = true;
                    }

                    EditorGUILayout.PropertyField(easeProperty, GUIContent.none, GUILayout.Width(96f));
                    DrawVector3Fields(offsetProperty);

                    if (GUILayout.Button("Del", GUILayout.Width(34f)))
                    {
                        removeIndex = i;
                    }
                }
            }

            if (moveFromIndex >= 0)
            {
                MoveMotionKey(keysProperty, moveFromIndex, moveToIndex);
            }
            else if (removeIndex >= 0)
            {
                keysProperty.DeleteArrayElementAtIndex(removeIndex);
            }
            else if (needsSort)
            {
                SortMotionKeys(keysProperty);
            }
        }

        private void DrawFrameOffsetTable(SerializedProperty frameSetProperty)
        {
            SerializedProperty framesProperty = frameSetProperty.FindPropertyRelative("frames");
            if (framesProperty == null)
            {
                return;
            }

            int currentFrameIndex = ResolveFrameIndex(framesProperty, previewTime);
            EditorGUILayout.LabelField("Frames", framesProperty.arraySize.ToString());
            if (currentFrameIndex >= 0)
            {
                EditorGUILayout.LabelField("Current Frame", currentFrameIndex.ToString());
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(string.Empty, GUILayout.Width(FrameGoWidth));
                GUILayout.Label("Frame", GUILayout.Width(FrameTimeWidth));
                GUILayout.Label("Sprite", GUILayout.Width(FrameSpriteWidth));
                GUILayout.Label("X", GUILayout.Width(FrameAxisLabelWidth + FrameFloatWidth));
                GUILayout.Label("Y", GUILayout.Width(FrameAxisLabelWidth + FrameFloatWidth));
                GUILayout.Label("Z", GUILayout.Width(FrameAxisLabelWidth + FrameFloatWidth));
            }

            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
                SerializedProperty timeProperty = frameProperty.FindPropertyRelative("time");
                SerializedProperty spriteProperty = frameProperty.FindPropertyRelative("sprite");
                SerializedProperty offsetProperty = frameProperty.FindPropertyRelative("offset");
                if (timeProperty == null || spriteProperty == null || offsetProperty == null)
                {
                    continue;
                }

                Sprite sprite = spriteProperty.objectReferenceValue as Sprite;
                string spriteName = sprite != null ? sprite.name : "(none)";
                string marker = i == currentFrameIndex ? ">" : " ";
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Go", GUILayout.Width(FrameGoWidth)))
                    {
                        previewTime = timeProperty.floatValue;
                        animatePreview = false;
                        Repaint();
                    }

                    EditorGUILayout.LabelField($"{marker}{i:00} {timeProperty.floatValue:0.###}", GUILayout.Width(FrameTimeWidth));
                    EditorGUILayout.LabelField(spriteName, GUILayout.Width(FrameSpriteWidth));
                    DrawVector3Fields(offsetProperty);
                }
            }
        }

        private static void DrawVector3Property(string label, SerializedProperty property)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(118f));
                DrawVector3Fields(property);
            }
        }

        private static void DrawVector3Fields(SerializedProperty property)
        {
            Vector3 value = property.vector3Value;
            EditorGUI.BeginChangeCheck();
            DrawVectorAxis("X", ref value.x);
            DrawVectorAxis("Y", ref value.y);
            DrawVectorAxis("Z", ref value.z);
            if (EditorGUI.EndChangeCheck())
            {
                property.vector3Value = value;
            }
        }

        private static void DrawVectorAxis(string label, ref float value)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(FrameAxisLabelWidth));
            value = EditorGUILayout.FloatField(value, GUILayout.Width(FrameFloatWidth));
        }

        private void SyncFrameOffsetsFromClip(LastBossSpriteAnimator.AnimationState state, AnimationClip clip)
        {
            if (serializedTarget == null)
            {
                return;
            }

            Undo.RecordObject(targetAnimator, "Sync Last Boss Frame Offsets");
            serializedTarget.Update();

            SerializedProperty setProperty = GetOrCreateFrameOffsetSetProperty(state);
            if (setProperty == null)
            {
                return;
            }

            List<StoredFrameOffset> existingOffsets = ReadStoredFrameOffsets(setProperty);
            List<ClipFrame> clipFrames = ResolveClipFrames(clip);

            setProperty.FindPropertyRelative("state").enumValueIndex = (int)state;
            setProperty.FindPropertyRelative("stateName").stringValue = GetStateName(state) ?? state.ToString();
            setProperty.FindPropertyRelative("clipLength").floatValue = clip != null ? Mathf.Max(clip.length, 0f) : 0f;
            setProperty.FindPropertyRelative("loop").boolValue = clip != null && AnimationUtility.GetAnimationClipSettings(clip).loopTime;

            SerializedProperty framesProperty = setProperty.FindPropertyRelative("frames");
            framesProperty.arraySize = clipFrames.Count;
            for (int i = 0; i < clipFrames.Count; i++)
            {
                SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
                frameProperty.FindPropertyRelative("time").floatValue = clipFrames[i].Time;
                frameProperty.FindPropertyRelative("sprite").objectReferenceValue = clipFrames[i].Sprite;
                frameProperty.FindPropertyRelative("offset").vector3Value = FindExistingFrameOffset(existingOffsets, clipFrames[i], i);
            }

            serializedTarget.ApplyModifiedProperties();
            MarkPrefabDirty();
            Repaint();
        }

        private void ResetStateFrameOffsets(LastBossSpriteAnimator.AnimationState state)
        {
            if (serializedTarget == null)
            {
                return;
            }

            Undo.RecordObject(targetAnimator, "Reset Last Boss State Frame Offsets");
            serializedTarget.Update();
            SerializedProperty setProperty = FindFrameOffsetSetProperty(state);
            if (setProperty != null)
            {
                ResetFrameOffsets(setProperty.FindPropertyRelative("frames"));
            }

            serializedTarget.ApplyModifiedProperties();
            MarkPrefabDirty();
            Repaint();
        }

        private void ResetFrameOffsets(SerializedProperty framesProperty)
        {
            if (framesProperty == null)
            {
                return;
            }

            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                SerializedProperty offsetProperty = framesProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("offset");
                if (offsetProperty != null)
                {
                    offsetProperty.vector3Value = Vector3.zero;
                }
            }
        }

        private void CreateMotionKeys(LastBossSpriteAnimator.AnimationState state, AnimationClip clip, int keyCount)
        {
            if (serializedTarget == null)
            {
                return;
            }

            Undo.RecordObject(targetAnimator, "Create Last Boss Motion Keys");
            serializedTarget.Update();

            SerializedProperty setProperty = GetOrCreateMotionOffsetSetProperty(state);
            if (setProperty == null)
            {
                return;
            }

            SyncMotionSetMetadata(setProperty, state, clip);
            SerializedProperty keysProperty = setProperty.FindPropertyRelative("keys");
            keyCount = Mathf.Max(1, keyCount);
            int frameCount = GetClipFrameCount(clip);
            keysProperty.arraySize = keyCount;
            for (int i = 0; i < keyCount; i++)
            {
                int frame = keyCount == 1
                    ? 1
                    : Mathf.RoundToInt(Mathf.Lerp(1f, frameCount, i / (keyCount - 1f)));
                SerializedProperty keyProperty = keysProperty.GetArrayElementAtIndex(i);
                keyProperty.FindPropertyRelative("time").floatValue = DisplayFrameToTime(frame, clip);
                keyProperty.FindPropertyRelative("offset").vector3Value = Vector3.zero;
                keyProperty.FindPropertyRelative("easeToNext").enumValueIndex =
                    (int)(i < keyCount - 1
                        ? LastBossSpriteAnimator.OffsetEase.EaseInOut
                        : LastBossSpriteAnimator.OffsetEase.Linear);
            }

            serializedTarget.ApplyModifiedProperties();
            MarkPrefabDirty();
            Repaint();
        }

        private void AddMotionKeyAtFrame(LastBossSpriteAnimator.AnimationState state, AnimationClip clip)
        {
            if (serializedTarget == null)
            {
                return;
            }

            Undo.RecordObject(targetAnimator, "Add Last Boss Motion Key");
            serializedTarget.Update();

            SerializedProperty setProperty = GetOrCreateMotionOffsetSetProperty(state);
            if (setProperty == null)
            {
                return;
            }

            SyncMotionSetMetadata(setProperty, state, clip);
            SerializedProperty keysProperty = setProperty.FindPropertyRelative("keys");
            int frame = TimeToDisplayFrame(previewTime, clip);
            float keyTime = DisplayFrameToTime(frame, clip);
            Vector3 currentOffset = GetMotionOffset(state, keyTime);
            int newIndex = keysProperty.arraySize;
            keysProperty.InsertArrayElementAtIndex(newIndex);

            SerializedProperty keyProperty = keysProperty.GetArrayElementAtIndex(newIndex);
            keyProperty.FindPropertyRelative("time").floatValue = keyTime;
            keyProperty.FindPropertyRelative("offset").vector3Value = currentOffset;
            keyProperty.FindPropertyRelative("easeToNext").enumValueIndex =
                (int)LastBossSpriteAnimator.OffsetEase.EaseInOut;
            SortMotionKeys(keysProperty);

            serializedTarget.ApplyModifiedProperties();
            MarkPrefabDirty();
            Repaint();
        }

        private void ResetStateMotionOffsets(LastBossSpriteAnimator.AnimationState state)
        {
            if (serializedTarget == null)
            {
                return;
            }

            Undo.RecordObject(targetAnimator, "Reset Last Boss State Motion Offsets");
            serializedTarget.Update();
            SerializedProperty setProperty = FindMotionOffsetSetProperty(state);
            if (setProperty != null)
            {
                ResetMotionOffsets(setProperty.FindPropertyRelative("keys"));
            }

            serializedTarget.ApplyModifiedProperties();
            MarkPrefabDirty();
            Repaint();
        }

        private static void ResetMotionOffsets(SerializedProperty keysProperty)
        {
            if (keysProperty == null)
            {
                return;
            }

            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                SerializedProperty offsetProperty = keysProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("offset");
                if (offsetProperty != null)
                {
                    offsetProperty.vector3Value = Vector3.zero;
                }
            }
        }

        private void SyncMotionSetMetadata(
            SerializedProperty setProperty,
            LastBossSpriteAnimator.AnimationState state,
            AnimationClip clip)
        {
            if (setProperty == null)
            {
                return;
            }

            setProperty.FindPropertyRelative("state").enumValueIndex = (int)state;
            setProperty.FindPropertyRelative("stateName").stringValue = GetStateName(state) ?? state.ToString();
            setProperty.FindPropertyRelative("clipLength").floatValue = clip != null ? Mathf.Max(clip.length, 0f) : 0f;
            setProperty.FindPropertyRelative("loop").boolValue = clip != null && AnimationUtility.GetAnimationClipSettings(clip).loopTime;
        }

        private static void SortMotionKeys(SerializedProperty keysProperty)
        {
            if (keysProperty == null || !keysProperty.isArray || keysProperty.arraySize <= 1)
            {
                return;
            }

            List<MotionKeyDraft> keys = new List<MotionKeyDraft>(keysProperty.arraySize);
            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                SerializedProperty keyProperty = keysProperty.GetArrayElementAtIndex(i);
                keys.Add(new MotionKeyDraft(
                    keyProperty.FindPropertyRelative("time").floatValue,
                    keyProperty.FindPropertyRelative("offset").vector3Value,
                    keyProperty.FindPropertyRelative("easeToNext").enumValueIndex));
            }

            keys.Sort((left, right) => left.Time.CompareTo(right.Time));
            for (int i = 0; i < keys.Count; i++)
            {
                SerializedProperty keyProperty = keysProperty.GetArrayElementAtIndex(i);
                keyProperty.FindPropertyRelative("time").floatValue = keys[i].Time;
                keyProperty.FindPropertyRelative("offset").vector3Value = keys[i].Offset;
                keyProperty.FindPropertyRelative("easeToNext").enumValueIndex = keys[i].EaseIndex;
            }
        }

        private static void MoveMotionKey(SerializedProperty keysProperty, int fromIndex, int toIndex)
        {
            if (keysProperty == null || !keysProperty.isArray)
            {
                return;
            }

            if (fromIndex < 0 ||
                fromIndex >= keysProperty.arraySize ||
                toIndex < 0 ||
                toIndex >= keysProperty.arraySize ||
                fromIndex == toIndex)
            {
                return;
            }

            keysProperty.MoveArrayElement(fromIndex, toIndex);
        }

        private SerializedProperty FindMotionOffsetSetProperty(LastBossSpriteAnimator.AnimationState state)
        {
            SerializedProperty setsProperty = serializedTarget?.FindProperty("motionOffsetSets");
            if (setsProperty == null || !setsProperty.isArray)
            {
                return null;
            }

            for (int i = 0; i < setsProperty.arraySize; i++)
            {
                SerializedProperty setProperty = setsProperty.GetArrayElementAtIndex(i);
                SerializedProperty stateProperty = setProperty.FindPropertyRelative("state");
                if (stateProperty != null && stateProperty.enumValueIndex == (int)state)
                {
                    return setProperty;
                }
            }

            return null;
        }

        private SerializedProperty GetOrCreateMotionOffsetSetProperty(LastBossSpriteAnimator.AnimationState state)
        {
            SerializedProperty existingProperty = FindMotionOffsetSetProperty(state);
            if (existingProperty != null)
            {
                return existingProperty;
            }

            SerializedProperty setsProperty = serializedTarget?.FindProperty("motionOffsetSets");
            if (setsProperty == null || !setsProperty.isArray)
            {
                return null;
            }

            int newIndex = setsProperty.arraySize;
            setsProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty setProperty = setsProperty.GetArrayElementAtIndex(newIndex);
            setProperty.FindPropertyRelative("state").enumValueIndex = (int)state;
            setProperty.FindPropertyRelative("stateName").stringValue = GetStateName(state) ?? state.ToString();
            setProperty.FindPropertyRelative("clipLength").floatValue = 0f;
            setProperty.FindPropertyRelative("loop").boolValue = false;
            setProperty.FindPropertyRelative("keys").arraySize = 0;
            return setProperty;
        }

        private SerializedProperty FindFrameOffsetSetProperty(LastBossSpriteAnimator.AnimationState state)
        {
            SerializedProperty setsProperty = serializedTarget?.FindProperty("frameOffsetSets");
            if (setsProperty == null || !setsProperty.isArray)
            {
                return null;
            }

            for (int i = 0; i < setsProperty.arraySize; i++)
            {
                SerializedProperty setProperty = setsProperty.GetArrayElementAtIndex(i);
                SerializedProperty stateProperty = setProperty.FindPropertyRelative("state");
                if (stateProperty != null && stateProperty.enumValueIndex == (int)state)
                {
                    return setProperty;
                }
            }

            return null;
        }

        private SerializedProperty GetOrCreateFrameOffsetSetProperty(LastBossSpriteAnimator.AnimationState state)
        {
            SerializedProperty existingProperty = FindFrameOffsetSetProperty(state);
            if (existingProperty != null)
            {
                return existingProperty;
            }

            SerializedProperty setsProperty = serializedTarget?.FindProperty("frameOffsetSets");
            if (setsProperty == null || !setsProperty.isArray)
            {
                return null;
            }

            int newIndex = setsProperty.arraySize;
            setsProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty setProperty = setsProperty.GetArrayElementAtIndex(newIndex);
            setProperty.FindPropertyRelative("state").enumValueIndex = (int)state;
            setProperty.FindPropertyRelative("stateName").stringValue = GetStateName(state) ?? state.ToString();
            setProperty.FindPropertyRelative("clipLength").floatValue = 0f;
            setProperty.FindPropertyRelative("loop").boolValue = false;
            setProperty.FindPropertyRelative("frames").arraySize = 0;
            return setProperty;
        }

        private List<ClipFrame> ResolveClipFrames(AnimationClip clip)
        {
            List<ClipFrame> frames = new List<ClipFrame>();
            if (clip != null)
            {
                EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                for (int i = 0; i < bindings.Length; i++)
                {
                    EditorCurveBinding binding = bindings[i];
                    if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                    {
                        continue;
                    }

                    ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (keyframes == null)
                    {
                        continue;
                    }

                    for (int frameIndex = 0; frameIndex < keyframes.Length; frameIndex++)
                    {
                        frames.Add(new ClipFrame(keyframes[frameIndex].time, keyframes[frameIndex].value as Sprite));
                    }
                }
            }

            if (frames.Count == 0)
            {
                SpriteRenderer renderer = targetAnimator != null ? targetAnimator.MainRenderer : null;
                frames.Add(new ClipFrame(0f, renderer != null ? renderer.sprite : null));
            }

            frames.Sort((left, right) => left.Time.CompareTo(right.Time));
            return frames;
        }

        private List<StoredFrameOffset> ReadStoredFrameOffsets(SerializedProperty setProperty)
        {
            List<StoredFrameOffset> offsets = new List<StoredFrameOffset>();
            SerializedProperty framesProperty = setProperty.FindPropertyRelative("frames");
            if (framesProperty == null)
            {
                return offsets;
            }

            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
                offsets.Add(new StoredFrameOffset(
                    frameProperty.FindPropertyRelative("time").floatValue,
                    frameProperty.FindPropertyRelative("sprite").objectReferenceValue as Sprite,
                    frameProperty.FindPropertyRelative("offset").vector3Value));
            }

            return offsets;
        }

        private static Vector3 FindExistingFrameOffset(List<StoredFrameOffset> existingOffsets, ClipFrame clipFrame, int index)
        {
            for (int i = 0; i < existingOffsets.Count; i++)
            {
                StoredFrameOffset existingOffset = existingOffsets[i];
                if (Mathf.Approximately(existingOffset.Time, clipFrame.Time) && existingOffset.Sprite == clipFrame.Sprite)
                {
                    return existingOffset.Offset;
                }
            }

            return index >= 0 && index < existingOffsets.Count ? existingOffsets[index].Offset : Vector3.zero;
        }

        private Vector3 GetPreviewOffset(LastBossSpriteAnimator.AnimationState state, float time)
        {
            return GetOffset(state) + GetMotionOffset(state, time) + GetFrameOffset(state, time);
        }

        private Vector3 GetMotionOffset(LastBossSpriteAnimator.AnimationState state, float time)
        {
            SerializedProperty setProperty = FindMotionOffsetSetProperty(state);
            if (setProperty == null)
            {
                return Vector3.zero;
            }

            return EvaluateMotionOffset(setProperty.FindPropertyRelative("keys"), time);
        }

        private Vector3 GetFrameOffset(LastBossSpriteAnimator.AnimationState state, float time)
        {
            SerializedProperty setProperty = FindFrameOffsetSetProperty(state);
            if (setProperty == null)
            {
                return Vector3.zero;
            }

            SerializedProperty framesProperty = setProperty.FindPropertyRelative("frames");
            int frameIndex = ResolveFrameIndex(framesProperty, time);
            if (frameIndex < 0)
            {
                return Vector3.zero;
            }

            SerializedProperty offsetProperty = framesProperty
                .GetArrayElementAtIndex(frameIndex)
                .FindPropertyRelative("offset");
            return offsetProperty != null ? offsetProperty.vector3Value : Vector3.zero;
        }

        private static Vector3 EvaluateMotionOffset(SerializedProperty keysProperty, float time)
        {
            if (keysProperty == null || keysProperty.arraySize == 0)
            {
                return Vector3.zero;
            }

            if (keysProperty.arraySize == 1)
            {
                return keysProperty.GetArrayElementAtIndex(0).FindPropertyRelative("offset").vector3Value;
            }

            int firstIndex = 0;
            int lastIndex = 0;
            float firstTime = GetMotionKeyTime(keysProperty, 0);
            float lastTime = firstTime;
            for (int i = 1; i < keysProperty.arraySize; i++)
            {
                float keyTime = GetMotionKeyTime(keysProperty, i);
                if (keyTime < firstTime)
                {
                    firstTime = keyTime;
                    firstIndex = i;
                }

                if (keyTime > lastTime)
                {
                    lastTime = keyTime;
                    lastIndex = i;
                }
            }

            if (time <= firstTime)
            {
                return GetMotionKeyOffset(keysProperty, firstIndex);
            }

            if (time >= lastTime)
            {
                return GetMotionKeyOffset(keysProperty, lastIndex);
            }

            int previousIndex = firstIndex;
            int nextIndex = lastIndex;
            float previousTime = float.NegativeInfinity;
            float nextTime = float.PositiveInfinity;
            for (int i = 0; i < keysProperty.arraySize; i++)
            {
                float keyTime = GetMotionKeyTime(keysProperty, i);
                if (keyTime <= time && keyTime >= previousTime)
                {
                    previousTime = keyTime;
                    previousIndex = i;
                }

                if (keyTime >= time && keyTime <= nextTime)
                {
                    nextTime = keyTime;
                    nextIndex = i;
                }
            }

            if (previousIndex == nextIndex || Mathf.Approximately(previousTime, nextTime))
            {
                return GetMotionKeyOffset(keysProperty, previousIndex);
            }

            Vector3 currentOffset = GetMotionKeyOffset(keysProperty, previousIndex);
            Vector3 nextOffset = GetMotionKeyOffset(keysProperty, nextIndex);
            LastBossSpriteAnimator.OffsetEase ease = GetMotionKeyEase(keysProperty, previousIndex);
            float duration = Mathf.Max(0.0001f, nextTime - previousTime);
            float t = Mathf.Clamp01((time - previousTime) / duration);
            return Vector3.LerpUnclamped(currentOffset, nextOffset, ApplyEase(t, ease));
        }

        private static float GetMotionKeyTime(SerializedProperty keysProperty, int index)
        {
            return keysProperty
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("time")
                .floatValue;
        }

        private static Vector3 GetMotionKeyOffset(SerializedProperty keysProperty, int index)
        {
            return keysProperty
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("offset")
                .vector3Value;
        }

        private static LastBossSpriteAnimator.OffsetEase GetMotionKeyEase(SerializedProperty keysProperty, int index)
        {
            int easeIndex = keysProperty
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("easeToNext")
                .enumValueIndex;
            return (LastBossSpriteAnimator.OffsetEase)easeIndex;
        }

        private static float ApplyEase(float t, LastBossSpriteAnimator.OffsetEase ease)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case LastBossSpriteAnimator.OffsetEase.EaseIn:
                    return t * t;
                case LastBossSpriteAnimator.OffsetEase.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case LastBossSpriteAnimator.OffsetEase.EaseInOut:
                    return t * t * (3f - 2f * t);
                default:
                    return t;
            }
        }

        private static int ResolveFrameIndex(SerializedProperty framesProperty, float time)
        {
            if (framesProperty == null || framesProperty.arraySize == 0)
            {
                return -1;
            }

            int selectedIndex = 0;
            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                SerializedProperty timeProperty = framesProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("time");
                if (timeProperty == null || timeProperty.floatValue > time)
                {
                    break;
                }

                selectedIndex = i;
            }

            return selectedIndex;
        }

        private static int TimeToDisplayFrame(float time, AnimationClip clip)
        {
            float frameRate = GetClipFrameRate(clip);
            int frame = Mathf.RoundToInt(Mathf.Max(0f, time) * frameRate) + 1;
            return Mathf.Clamp(frame, 1, GetClipFrameCount(clip));
        }

        private static float DisplayFrameToTime(int frame, AnimationClip clip)
        {
            frame = Mathf.Clamp(frame, 1, GetClipFrameCount(clip));
            return (frame - 1) / GetClipFrameRate(clip);
        }

        private static int GetClipFrameCount(AnimationClip clip)
        {
            if (clip == null || clip.length <= 0f)
            {
                return 1;
            }

            return Mathf.Max(1, Mathf.RoundToInt(clip.length * GetClipFrameRate(clip)));
        }

        private static float GetClipFrameRate(AnimationClip clip)
        {
            return clip != null && clip.frameRate > 0f ? clip.frameRate : 60f;
        }

        private AnimationClip ResolveClip(LastBossSpriteAnimator.AnimationState state)
        {
            string stateName = GetStateName(state);
            if (string.IsNullOrEmpty(stateName))
            {
                return null;
            }

            AnimatorController controller = ResolveAnimatorController();
            if (controller == null)
            {
                return null;
            }

            int layerIndex = Mathf.Clamp(GetIntProperty("animatorLayer"), 0, controller.layers.Length - 1);
            ChildAnimatorState? animatorState = FindState(controller.layers[layerIndex].stateMachine, stateName);
            if (!animatorState.HasValue)
            {
                return null;
            }

            return animatorState.Value.state.motion as AnimationClip;
        }

        private AnimatorController ResolveAnimatorController()
        {
            SerializedProperty animatorProperty = serializedTarget.FindProperty("animator");
            Animator animator = animatorProperty != null
                ? animatorProperty.objectReferenceValue as Animator
                : targetAnimator.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return null;
            }

            return animator.runtimeAnimatorController as AnimatorController;
        }

        private static ChildAnimatorState? FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == stateName)
                {
                    return states[i];
                }
            }

            ChildAnimatorStateMachine[] childMachines = stateMachine.stateMachines;
            for (int i = 0; i < childMachines.Length; i++)
            {
                ChildAnimatorState? foundState = FindState(childMachines[i].stateMachine, stateName);
                if (foundState.HasValue)
                {
                    return foundState;
                }
            }

            return null;
        }

        private string GetStateName(LastBossSpriteAnimator.AnimationState state)
        {
            StateBinding binding = GetBinding(state);
            if (string.IsNullOrEmpty(binding.StateNamePropertyName))
            {
                return null;
            }

            SerializedProperty property = serializedTarget.FindProperty(binding.StateNamePropertyName);
            return property != null ? property.stringValue : null;
        }

        private Vector3 GetOffset(LastBossSpriteAnimator.AnimationState state)
        {
            StateBinding binding = GetBinding(state);
            SerializedProperty property = serializedTarget.FindProperty(binding.OffsetPropertyName);
            return property != null ? property.vector3Value : Vector3.zero;
        }

        private int GetIntProperty(string propertyName)
        {
            SerializedProperty property = serializedTarget.FindProperty(propertyName);
            return property != null ? property.intValue : 0;
        }

        private static StateBinding GetBinding(LastBossSpriteAnimator.AnimationState state)
        {
            for (int i = 0; i < StateBindings.Length; i++)
            {
                if (StateBindings[i].State == state)
                {
                    return StateBindings[i];
                }
            }

            return StateBindings[0];
        }

        private void ResetAllOffsets()
        {
            Undo.RecordObject(targetAnimator, "Reset Last Boss Animation Offsets");
            serializedTarget.Update();
            for (int i = 0; i < StateBindings.Length; i++)
            {
                SerializedProperty property = serializedTarget.FindProperty(StateBindings[i].OffsetPropertyName);
                if (property != null)
                {
                    property.vector3Value = Vector3.zero;
                }
            }

            SerializedProperty setsProperty = serializedTarget.FindProperty("frameOffsetSets");
            if (setsProperty != null && setsProperty.isArray)
            {
                for (int i = 0; i < setsProperty.arraySize; i++)
                {
                    ResetFrameOffsets(setsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("frames"));
                }
            }

            SerializedProperty motionSetsProperty = serializedTarget.FindProperty("motionOffsetSets");
            if (motionSetsProperty != null && motionSetsProperty.isArray)
            {
                for (int i = 0; i < motionSetsProperty.arraySize; i++)
                {
                    ResetMotionOffsets(motionSetsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("keys"));
                }
            }

            serializedTarget.ApplyModifiedProperties();
            MarkPrefabDirty();
            Repaint();
        }

        private void SavePrefab()
        {
            serializedTarget.ApplyModifiedProperties();
            MarkPrefabDirty();
            if (targetPrefab != null)
            {
                PrefabUtility.SavePrefabAsset(targetPrefab);
            }

            AssetDatabase.SaveAssets();
        }

        private void LoadDefaultPrefabIfNeeded()
        {
            if (targetPrefab == null)
            {
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LastBossPrefabPath);
            }

            RefreshTargetAnimator();
        }

        private void SetTargetPrefab(GameObject prefab)
        {
            if (prefab != null && !EditorUtility.IsPersistent(prefab))
            {
                Debug.LogWarning("LastBossAnimationOffsetEditorWindow only edits prefab assets.");
                return;
            }

            DestroyPreviewInstance();
            targetPrefab = prefab;
            RefreshTargetAnimator();
            previewTime = 0f;
            Repaint();
        }

        private void RefreshTargetAnimator()
        {
            targetAnimator = targetPrefab != null
                ? targetPrefab.GetComponentInChildren<LastBossSpriteAnimator>(true)
                : null;
            RefreshSerializedTarget();
        }

        private void RefreshSerializedTarget()
        {
            if (targetAnimator == null)
            {
                serializedTarget = null;
                return;
            }

            if (serializedTarget == null || serializedTarget.targetObject != targetAnimator)
            {
                serializedTarget = new SerializedObject(targetAnimator);
            }
        }

        private void MarkPrefabDirty()
        {
            EditorUtility.SetDirty(targetAnimator);
            EditorUtility.SetDirty(targetPrefab);
        }

        private void OnEditorUpdate()
        {
            if (!animatePreview || targetAnimator == null)
            {
                lastEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            AnimationClip clip = ResolveClip(previewState);
            if (clip == null || clip.length <= 0f)
            {
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - lastEditorTime);
            lastEditorTime = currentTime;
            previewTime = Mathf.Repeat(previewTime + deltaTime * previewSpeed, clip.length);
            Repaint();
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private readonly struct ClipFrame
        {
            public ClipFrame(float time, Sprite sprite)
            {
                Time = time;
                Sprite = sprite;
            }

            public float Time { get; }
            public Sprite Sprite { get; }
        }

        private readonly struct StoredFrameOffset
        {
            public StoredFrameOffset(float time, Sprite sprite, Vector3 offset)
            {
                Time = time;
                Sprite = sprite;
                Offset = offset;
            }

            public float Time { get; }
            public Sprite Sprite { get; }
            public Vector3 Offset { get; }
        }

        private readonly struct MotionKeyDraft
        {
            public MotionKeyDraft(float time, Vector3 offset, int easeIndex)
            {
                Time = time;
                Offset = offset;
                EaseIndex = easeIndex;
            }

            public float Time { get; }
            public Vector3 Offset { get; }
            public int EaseIndex { get; }
        }

        private readonly struct StateBinding
        {
            public StateBinding(
                LastBossSpriteAnimator.AnimationState state,
                string label,
                string offsetPropertyName,
                string stateNamePropertyName)
            {
                State = state;
                Label = label;
                OffsetPropertyName = offsetPropertyName;
                StateNamePropertyName = stateNamePropertyName;
            }

            public LastBossSpriteAnimator.AnimationState State { get; }
            public string Label { get; }
            public string OffsetPropertyName { get; }
            public string StateNamePropertyName { get; }
        }
    }
}
