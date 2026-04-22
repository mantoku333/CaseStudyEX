using System;
using System.Collections.Generic;
using Player;
using UnityEditor;
//using UnityEditor.EditorTools;
//using UnityEditor.Tilemaps;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EditorTools
{
    /// <summary>
    /// プレイヤー、敵の配置と、
    /// Tilemapへのステージタイル配置を行う簡易エディタ
    /// </summary>
    public class StageEditorWindow : EditorWindow
    {
        /// <summary>
        /// 現在の配置モード
        /// </summary>
        private enum PlacementType
        {
            None,
            Player,
            Enemy,
            Stage,
            Erase
        }
        private enum TileEditMode
        {
            Brush,
            Rectangle,
            Select
        }

        private enum StagePaintTileType
        {
            Block,
            Slope
        }

        // 直角があるコーナーを指定することで、4方向の斜面を扱う
        private enum SlopeCorner
        {
            BottomLeft,
            BottomRight,
            TopLeft,
            TopRight
        }

        [SerializeField] private TileEditMode tileEditMode = TileEditMode.Brush;
        [SerializeField] private StagePaintTileType stagePaintTileType = StagePaintTileType.Block;
        [SerializeField] private SlopeCorner slopeCorner = SlopeCorner.BottomLeft;

        private bool isTileDragging;
        private Vector3Int dragStartCell;
        private Vector3Int dragCurrentCell;
        private Vector3Int lastDraggedCell = invalidCell;

        // 選択状態
        private bool hasTileSelection;
        private BoundsInt selectedBounds;

        // 範囲選択中
        private bool isSelectingTiles;

        // 移動中
        private bool isMovingSelection;
        private Vector3Int moveStartCell;
        private Vector3Int currentMoveOffset;
        private BoundsInt moveSourceBounds;

        // コピー用クリップボード
        [Serializable]
        private struct ClipboardTile
        {
            public Vector3Int offset;
            public TileBase tile;
            public Matrix4x4 transform;
        }

        private List<ClipboardTile> clipboardTiles = new List<ClipboardTile>();
        private Vector3Int clipboardSize = Vector3Int.zero;

        // ペースト待機中
        private bool isPastePreview;
        private Vector3Int pastePreviewCell;

        //最小数値の座標を無効座標と指定
        private static readonly Vector3Int invalidCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        [SerializeField] private StageEditorPalette palette;
        [SerializeField] private PlayerStatsData playerStatsData;
        [SerializeField] private global::CameraData cameraData;
        [SerializeField] private Tilemap targetStageTilemap;
        [SerializeField] private PlacementType currentPlacementType = PlacementType.None;
        private UnityEditor.Editor cachedStatsEditor;

        private const string PaletteGuidKey = "StageEditor.PaletteGuid";
        private const string PlayerStatsGuidKey = "StageEditor.PlayerStatsGuid";
        private const string CameraDataGuidKey = "StageEditor.CameraDataGuid";
        private const string CameraPreviewTargetName = "CN_FollowCam";

        [MenuItem("Tools/Stage Editor")]
        public static void Open()
        {
            StageEditorWindow window = GetWindow<StageEditorWindow>("Stage Editor");
            window.minSize = new Vector2(420f, 340f);
        }

        private void OnEnable()
        {
            LoadEditorPrefs();

            if (targetStageTilemap == null)
            {
                targetStageTilemap = FindFirstObjectByType<Tilemap>();
            }

            ApplyCameraPreview(false);
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            SceneView.RepaintAll();
        }

        private void OnDisable()
        {
            SaveEditorPrefs();
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            DestroyCachedEditor();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode &&
                state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            // Entering Play can reinitialize camera components; apply once on the next editor tick.
            EditorApplication.delayCall += ApplyCameraPreviewAfterPlayModeChange;
        }

        private void ApplyCameraPreviewAfterPlayModeChange()
        {
            if (cameraData == null)
            {
                LoadEditorPrefs();
            }

            ApplyCameraPreview(false);
            SceneView.RepaintAll();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Editor Assets", EditorStyles.boldLabel);

            StageEditorPalette newPalette = (StageEditorPalette)EditorGUILayout.ObjectField(
                "Palette",
                palette,
                typeof(StageEditorPalette),
                false);

            if (newPalette != palette)
            {
                palette = newPalette;
                SaveEditorPrefs();
            }

            targetStageTilemap = (Tilemap)EditorGUILayout.ObjectField(
                "Stage Tilemap",
                targetStageTilemap,
                typeof(Tilemap),
                true);

            PlayerStatsData newStatsData = (PlayerStatsData)EditorGUILayout.ObjectField(
                "Player Stats",
                playerStatsData,
                typeof(PlayerStatsData),
                false);

            if (newStatsData != playerStatsData)
            {
                playerStatsData = newStatsData;
                RefreshStatsEditor();
                SaveEditorPrefs();
            }

            global::CameraData newCameraData = (global::CameraData)EditorGUILayout.ObjectField(
                "Camera Data",
                cameraData,
                typeof(global::CameraData),
                false);

            if (newCameraData != cameraData)
            {
                cameraData = newCameraData;
                SaveEditorPrefs();
                ApplyCameraPreview();
                SceneView.RepaintAll();
            }

            DrawCameraSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Placement Mode", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                //プレイヤー配置モード
                if (GUILayout.Button("Player"))
                {
                    CancelTileDrag();
                    ClearTileSelection();
                    currentPlacementType = PlacementType.Player;
                }
                //敵配置モード
                if (GUILayout.Button("Enemy"))
                {
                    CancelTileDrag();
                    currentPlacementType = PlacementType.Enemy;
                }
                //ステージ配置モード
                if (GUILayout.Button("Stage"))
                {
                    CancelTileDrag();
                    currentPlacementType = PlacementType.Stage;
                }
                //削除モード
                if (GUILayout.Button("Erase"))
                {
                    CancelTileDrag();
                    currentPlacementType = PlacementType.Erase;
                }
                //Editorリセットボタン
                if (GUILayout.Button("Reset"))
                {
                    ResetEditorState();
                }
            }

            if (currentPlacementType == PlacementType.Stage)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Stage Tile Type", EditorStyles.boldLabel);
                stagePaintTileType = (StagePaintTileType)GUILayout.Toolbar(
                    (int)stagePaintTileType,
                    new[] { "通常ブロック", "斜面" });

                if (stagePaintTileType == StagePaintTileType.Slope)
                {
                    EditorGUILayout.LabelField("Slope Direction", EditorStyles.boldLabel);
                    slopeCorner = (SlopeCorner)GUILayout.Toolbar(
                        (int)slopeCorner,
                        new[] { "左下", "右下", "左上", "右上" });
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Tile Edit Mode", EditorStyles.boldLabel);
                tileEditMode = (TileEditMode)GUILayout.Toolbar(
                    (int)tileEditMode,
                    new[] { "長押しペイント", "範囲選択", "選択/移動" });

                EditorGUILayout.HelpBox(
                    "Ctrl+C: コピー / Ctrl+V: ペースト / 選択範囲内ドラッグ: 移動",
                    MessageType.None);
            }
            else if (currentPlacementType == PlacementType.Erase)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Tile Edit Mode", EditorStyles.boldLabel);

                int eraseMode = Mathf.Min((int)tileEditMode, 1);
                eraseMode = GUILayout.Toolbar(eraseMode, new[] { "長押し削除", "範囲削除" });
                tileEditMode = (TileEditMode)eraseMode;
            }

            EditorGUILayout.HelpBox(
                $"現在の配置モード: {currentPlacementType}" +
                ((currentPlacementType == PlacementType.Stage || currentPlacementType == PlacementType.Erase)
                    ? $" / {tileEditMode}"
                    : ""),
                MessageType.Info);

            if ((currentPlacementType == PlacementType.Stage || currentPlacementType == PlacementType.Erase) &&
                targetStageTilemap == null)
            {
                EditorGUILayout.HelpBox("Stage または Erase モードを使うには Stage Tilemap の設定が必要です", MessageType.Warning);
            }

            if (currentPlacementType == PlacementType.Stage &&
                !HasValidStagePaintTile())
            {
                string requiredTileName = stagePaintTileType == StagePaintTileType.Block ? "Stage Tile" : "Slope Tile";
                EditorGUILayout.HelpBox($"Stage モードを使うには Palette に {requiredTileName} の設定が必要です", MessageType.Warning);
            }

            if (cachedStatsEditor == null)
            {
                RefreshStatsEditor();
            }

            if (cachedStatsEditor != null)
            {
                cachedStatsEditor.OnInspectorGUI();
            }

            if (currentPlacementType == PlacementType.Player)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Player Utility", EditorStyles.boldLabel);

                if (GUILayout.Button("StartPointへリセット"))
                {
                    ResetPlayerToStartPoint();
                }
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            // 何も配置モードが選ばれていない場合、Scene上では何もしない
            if (currentPlacementType == PlacementType.None)
            {
                return;
            }

            // Player / Enemy 配置時は、配置に使う prefab 情報を持つ palette が必要
            // palette が未設定なら処理できないため終了
            if ((currentPlacementType == PlacementType.Player ||
                 currentPlacementType == PlacementType.Enemy) &&
                palette == null)
            {
                return;
            }

            // 現在のイベント情報（マウス操作、キー入力など）を取得
            Event e = Event.current;

            // SceneView 上にGUIを描画開始
            Handles.BeginGUI();
            //Rect内で描画の範囲を指定  (左上x座標、左上y座標、幅の大きさ、縦の大きさ)
            GUILayout.BeginArea(new Rect(30f, 30f, 320f, 130f), GUI.skin.window);
            GUILayout.Label($"配置モード: {currentPlacementType}");

            // Stage / Erase の場合は、タイル編集用の説明を表示
            if (currentPlacementType == PlacementType.Stage || currentPlacementType == PlacementType.Erase)
            {
                GUILayout.Label($"編集方式: {tileEditMode}");

                if (currentPlacementType == PlacementType.Stage)
                {
                    GUILayout.Label($"配置タイル: {GetStagePaintTileLabel()}");
                }

                //編集方式ごとの操作説明を表示
                if (currentPlacementType == PlacementType.Stage && tileEditMode == TileEditMode.Select)
                {
                    GUILayout.Label("左ドラッグで範囲選択");
                    GUILayout.Label("選択範囲内ドラッグで移動 / Ctrl+C / Ctrl+V");
                }
                else if (tileEditMode == TileEditMode.Brush)
                {
                    GUILayout.Label("左ドラッグで連続配置 / 削除");
                }
                else
                {
                    GUILayout.Label("左ドラッグで範囲選択して配置 / 削除");
                }

                GUILayout.Label("Escで終了");
            }
            else
            {
                // Player / Enemy 配置時の操作説明を表示
                GUILayout.Label("左クリックで配置 / Escで終了");
                GUILayout.Label("Tilemapはセル単位で配置されます");
            }

            // GUI描画終了
            GUILayout.EndArea();
            Handles.EndGUI();

            // Escキーが押されたら現在の配置モードを終了する
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                //ペーストだった場合はキャンセル
                if (isPastePreview)
                {
                    isPastePreview = false;
                    SceneView.RepaintAll();
                    e.Use();
                    return;
                }
                // タイルドラッグ中の状態をリセット
                CancelTileDrag();

                //タイルの選択を解除
                ClearTileSelection();

                // 配置モードを解除
                currentPlacementType = PlacementType.None;

                // Inspector / EditorWindow を再描画
                Repaint();

                // このイベントを消費して他に渡さない

                e.Use();
                return;
            }

            // Stage / Erase の場合はタイル編集専用処理に分岐
            if (currentPlacementType == PlacementType.Stage || currentPlacementType == PlacementType.Erase)
            {
                HandleTileEditing(sceneView, e);
                return;
            }

            //PlayerとEnemyはクリックしたマウス位置に合わせて配置
            if (e.type == EventType.MouseDown)
            {
                // マウス位置からワールド座標を取得
                Vector3 worldPosition = GetMouseWorldPosition(e.mousePosition);

                // 現在の配置モードに応じて prefab を配置
                switch (currentPlacementType)
                {
                    case PlacementType.Player:
                        PlacePrefabOnGrid(palette.PlayerPrefab, worldPosition);
                        break;

                    case PlacementType.Enemy:
                        PlacePrefabOnGrid(palette.EnemyPrefab, worldPosition);
                        break;

                }

                // このクリックイベントを消費
                e.Use();
            }
        }

        /// <summary>
        /// PrefabをTilemapのセル中央にスナップして配置
        /// </summary>
        private void PlacePrefabOnGrid(GameObject prefab, Vector3 worldPosition)
        {
            if (prefab == null)
            {
                Debug.LogWarning("対応する Prefab が設定されていません");
                return;
            }

            Vector3 placePosition = worldPosition;

            if (targetStageTilemap != null)
            {
                Vector3Int cell = targetStageTilemap.WorldToCell(worldPosition);
                placePosition = targetStageTilemap.GetCellCenterWorld(cell);
                placePosition.z = 0f;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, $"Place {prefab.name}");
            instance.transform.position = placePosition;
            Selection.activeGameObject = instance;
        }

        /// <summary>
        /// SceneView上のマウス位置をワールド座標へ変換
        /// </summary>
        private static Vector3 GetMouseWorldPosition(Vector2 mousePosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 point = ray.GetPoint(enter);
                point.z = 0f;
                return point;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// PlayerStatsData用のInspectorを再生成
        /// </summary>
        private void RefreshStatsEditor()
        {
            DestroyCachedEditor();

            if (playerStatsData != null)
            {
                cachedStatsEditor = UnityEditor.Editor.CreateEditor(playerStatsData);
            }
        }

        private void DrawCameraSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);

            if (cameraData == null)
            {
                EditorGUILayout.HelpBox("Camera Data を設定すると、Y と Zoom をスライダーで調整できます。", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();

            float followOffsetY = EditorGUILayout.Slider(
                "Follow Offset Y",
                cameraData.FollowOffsetY,
                global::CameraData.MinFollowOffsetY,
                global::CameraData.MaxFollowOffsetY);

            float orthographicSize = EditorGUILayout.Slider(
                "Orthographic Size",
                cameraData.OrthographicSize,
                global::CameraData.MinOrthographicSize,
                global::CameraData.MaxOrthographicSize);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(cameraData, "Adjust Camera Data");
            cameraData.SetFollowOffsetY(followOffsetY);
            cameraData.SetOrthographicSize(orthographicSize);
            ApplyCameraPreview();
            SceneView.RepaintAll();
        }

        private void ApplyCameraPreview(bool recordUndo = true)
        {
            if (cameraData == null)
            {
                return;
            }

            Unity.Cinemachine.CinemachineCamera followCamera = null;
            Unity.Cinemachine.CinemachinePositionComposer followComposer = null;

            Unity.Cinemachine.CinemachineCamera[] cameras = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);

            foreach (Unity.Cinemachine.CinemachineCamera camera in cameras)
            {
                if (camera == null || camera.gameObject.name != CameraPreviewTargetName)
                {
                    continue;
                }

                Unity.Cinemachine.CinemachinePositionComposer positionComposer =
                    camera.GetComponent<Unity.Cinemachine.CinemachinePositionComposer>();
                if (positionComposer == null)
                {
                    return;
                }

                followCamera = camera;
                followComposer = positionComposer;
                break;
            }

            if (followCamera == null || followComposer == null)
            {
                return;
            }

            Vector3 targetOffset = followComposer.TargetOffset;
            if (!Mathf.Approximately(targetOffset.y, cameraData.FollowOffsetY))
            {
                if (recordUndo)
                {
                    Undo.RecordObject(followComposer, "Sync Follow Camera Target Offset");
                }

                targetOffset.y = cameraData.FollowOffsetY;
                followComposer.TargetOffset = targetOffset;
            }

            var lens = followCamera.Lens;
            bool zoomChanged = !Mathf.Approximately(lens.OrthographicSize, cameraData.OrthographicSize);

            if (zoomChanged)
            {
                if (recordUndo)
                {
                    Undo.RecordObject(followCamera, "Sync Follow Camera Zoom");
                }

                lens.OrthographicSize = cameraData.OrthographicSize;
                followCamera.Lens = lens;
            }
        }

        /// <summary>
        /// 一時生成したInspectorを破棄
        /// </summary>
        private void DestroyCachedEditor()
        {
            if (cachedStatsEditor != null)
            {
                DestroyImmediate(cachedStatsEditor);
                cachedStatsEditor = null;
            }
        }

        /// <summary>
        /// EditorWindowで選択したアセット参照を保存
        /// </summary>
        private void SaveEditorPrefs()
        {
            SaveAssetGuid(PaletteGuidKey, palette);
            SaveAssetGuid(PlayerStatsGuidKey, playerStatsData);
            SaveAssetGuid(CameraDataGuidKey, cameraData);
        }

        /// <summary>
        /// 保存していたアセット参照を復元
        /// </summary>
        private void LoadEditorPrefs()
        {
            palette = LoadAssetFromGuid<StageEditorPalette>(PaletteGuidKey);
            playerStatsData = LoadAssetFromGuid<PlayerStatsData>(PlayerStatsGuidKey);
            cameraData = LoadAssetFromGuid<global::CameraData>(CameraDataGuidKey);

            if (playerStatsData != null)
            {
                RefreshStatsEditor();
            }
        }

        /// <summary>
        /// アセット参照をGUIDとして保存
        /// </summary>
        private static void SaveAssetGuid<T>(string key, T asset) where T : UnityEngine.Object
        {
            if (asset == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            EditorPrefs.SetString(key, guid);
        }

        /// <summary>
        /// GUIDからアセット参照を復元
        /// </summary>
        private static T LoadAssetFromGuid<T>(string key) where T : UnityEngine.Object
        {
            if (!EditorPrefs.HasKey(key))
            {
                return null;
            }

            string guid = EditorPrefs.GetString(key);
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        /// <summary>
        /// 配置されているeditorやtilemapをリセット
        /// </summary>
        private void ResetEditorState()
        {
            CancelTileDrag();
            ClearTileSelection();
            currentPlacementType = PlacementType.None;
            stagePaintTileType = StagePaintTileType.Block;
            slopeCorner = SlopeCorner.BottomLeft;

            palette = null;
            playerStatsData = null;
            cameraData = null;
            targetStageTilemap = null;

            DestroyCachedEditor();

            EditorPrefs.DeleteKey(PaletteGuidKey);
            EditorPrefs.DeleteKey(PlayerStatsGuidKey);
            EditorPrefs.DeleteKey(CameraDataGuidKey);

            Repaint();
        }

        /// <summary>
        /// Stage / Erase モード時のタイル編集入力を処理
        /// </summary>
        private void HandleTileEditing(SceneView sceneView, Event e)
        {
            // 編集対象の Tilemap が無ければ何もしない
            if (targetStageTilemap == null)
            {
                return;
            }

            if (currentPlacementType == PlacementType.Stage && tileEditMode == TileEditMode.Select)
            {
                HandleSelectionMode(sceneView, e);
                return;
            }

            // Stage配置時は、配置する Tile が設定されていないと処理できないのでreturn
            if (currentPlacementType == PlacementType.Stage &&
                !HasValidStagePaintTile())
            {
                return;
            }

            // Rectangle モードでドラッグ中なら、現在の範囲プレビューを描画する
            if (tileEditMode == TileEditMode.Rectangle && isTileDragging)
            {
                DrawRectanglePreview(dragStartCell, dragCurrentCell);
            }

            if (currentPlacementType == PlacementType.Stage &&
                TryGetCellFromMouse(e.mousePosition, out Vector3Int hoverCell))
            {
                DrawCurrentStagePaintPreview(hoverCell);
            }

            // 左クリック以外は、この編集処理の対象外
            if (e.button != 0)
            {
                return;
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    {
                        // マウス位置からセル座標を取得できなければreturn
                        if (!TryGetCellFromMouse(e.mousePosition, out Vector3Int cell))
                        {
                            return;
                        }

                        // ドラッグ開始状態を記録
                        isTileDragging = true;
                        dragStartCell = cell;
                        dragCurrentCell = cell;
                        lastDraggedCell = invalidCell;

                        // Brush モードなら、押した瞬間に1マス編集する
                        if (tileEditMode == TileEditMode.Brush)
                        {
                            Undo.RegisterCompleteObjectUndo(
                                targetStageTilemap,
                                currentPlacementType == PlacementType.Stage ? "Paint Stage Tiles" : "Erase Stage Tiles");

                            ApplyTileEdit(cell);
                            lastDraggedCell = cell;
                        }
                        // このイベントはここで処理済みにする
                        e.Use();
                        break;
                    }

                case EventType.MouseDrag:
                    {
                        // ドラッグ中でなければ何もしない
                        if (!isTileDragging)
                        {
                            return;
                        }

                        // 現在のマウス位置からセル座標を取得
                        if (!TryGetCellFromMouse(e.mousePosition, out Vector3Int cell))
                        {
                            return;
                        }

                        // 現在のドラッグ位置を更新
                        dragCurrentCell = cell;

                        if (tileEditMode == TileEditMode.Brush)
                        {
                            // Brush モードでは、前回と違うセルに入ったときだけ編集する
                            if (cell != lastDraggedCell)
                            {
                                ApplyTileEdit(cell);
                                lastDraggedCell = cell;
                            }
                        }
                        else
                        {
                            // Rectangle モードでは範囲プレビュー更新のため再描画する
                            sceneView.Repaint();
                        }

                        e.Use();
                        break;
                    }

                case EventType.MouseUp:
                    {
                        // ドラッグ中でなければ何もしない
                        if (!isTileDragging)
                        {
                            return;
                        }

                        // マウスを離した地点のセルを取得できれば終点として更新する
                        if (TryGetCellFromMouse(e.mousePosition, out Vector3Int cell))
                        {
                            dragCurrentCell = cell;
                        }

                        // Rectangle モードなら、開始セルから終了セルまでを一括編集する
                        if (tileEditMode == TileEditMode.Rectangle)
                        {
                            ApplyTileEditRectangle(dragStartCell, dragCurrentCell);
                        }
                        // ドラッグ状態をリセット
                        CancelTileDrag();
                        e.Use();
                        break;
                    }
            }
        }

        /// <summary>
        /// マウス座標から Tilemap 上のセル座標を取得
        /// </summary>
        private bool TryGetCellFromMouse(Vector2 mousePosition, out Vector3Int cell)
        {
            cell = Vector3Int.zero;

            if (targetStageTilemap == null)
            {
                return false;
            }
            // マウス位置をワールド座標へ変換
            Vector3 worldPosition = GetMouseWorldPosition(mousePosition);

            // ワールド座標を Tilemap のセル座標へ変換
            cell = targetStageTilemap.WorldToCell(worldPosition);
            return true;
        }

        private struct StagePaintData
        {
            public TileBase tile;
            public Matrix4x4 transform;
        }

        private bool HasValidStagePaintTile()
        {
            if (palette == null)
            {
                return false;
            }

            return stagePaintTileType == StagePaintTileType.Block
                ? palette.StageTile != null
                : palette.SlopeTile != null;
        }

        private bool TryGetStagePaintData(out StagePaintData paintData)
        {
            paintData = default;

            if (palette == null)
            {
                return false;
            }

            if (stagePaintTileType == StagePaintTileType.Block)
            {
                if (palette.StageTile == null)
                {
                    return false;
                }

                paintData.tile = palette.StageTile;
                paintData.transform = Matrix4x4.identity;
                return true;
            }

            if (palette.SlopeTile == null)
            {
                return false;
            }

            paintData.tile = palette.SlopeTile;
            paintData.transform = GetSlopeTransformMatrix(slopeCorner);
            return true;
        }

        // SlopeTile は「左下が直角」の向きを基準にし、回転で向きを切り替える
        private static Matrix4x4 GetSlopeTransformMatrix(SlopeCorner corner)
        {
            switch (corner)
            {
                case SlopeCorner.BottomLeft:
                    return Matrix4x4.identity;
                case SlopeCorner.BottomRight:
                    return Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f));
                case SlopeCorner.TopLeft:
                    return Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -90f));
                case SlopeCorner.TopRight:
                    return Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 180f));
                default:
                    return Matrix4x4.identity;
            }
        }

        private void SetTileWithTransform(Vector3Int cell, TileBase tile, Matrix4x4 transform)
        {
            if (targetStageTilemap == null)
            {
                return;
            }

            targetStageTilemap.SetTile(cell, tile);
            if (tile == null)
            {
                return;
            }

            targetStageTilemap.RemoveTileFlags(cell, TileFlags.LockTransform);
            targetStageTilemap.SetTransformMatrix(cell, transform);
        }

        private string GetStagePaintTileLabel()
        {
            if (stagePaintTileType == StagePaintTileType.Block)
            {
                return "通常ブロック";
            }

            return $"斜面({GetSlopeCornerLabel(slopeCorner)})";
        }

        private static string GetSlopeCornerLabel(SlopeCorner corner)
        {
            switch (corner)
            {
                case SlopeCorner.BottomLeft:
                    return "左下";
                case SlopeCorner.BottomRight:
                    return "右下";
                case SlopeCorner.TopLeft:
                    return "左上";
                case SlopeCorner.TopRight:
                    return "右上";
                default:
                    return string.Empty;
            }
        }

        private void DrawCurrentStagePaintPreview(Vector3Int cell)
        {
            Color fillColor = new Color(0f, 1f, 0f, 0.18f);
            Color outlineColor = new Color(0f, 1f, 0f, 0.95f);

            if (stagePaintTileType == StagePaintTileType.Block)
            {
                DrawFilledCellPreview(cell, fillColor, outlineColor);
                return;
            }

            DrawSlopeCellPreview(cell, slopeCorner, fillColor, outlineColor);
        }

        /// <summary>
        /// 指定範囲のタイルをまとめて配置または削除
        /// </summary>
        private void ApplyTileEdit(Vector3Int cell)
        {
            switch (currentPlacementType)
            {
                // Stage モードならタイルを配置
                case PlacementType.Stage:
                    PaintTile(cell);
                    break;

                // Erase モードならタイルを削除
                case PlacementType.Erase:
                    EraseTile(cell);
                    break;
            }
        }

        /// <summary>
        /// 指定セルにステージ用タイルを配置
        /// </summary>
        private void ApplyTileEditRectangle(Vector3Int startCell, Vector3Int endCell)
        {
            if (targetStageTilemap == null)
            {
                Debug.LogWarning("Stage Tilemap が設定されていません");
                return;
            }

            StagePaintData paintData = default;

            if (currentPlacementType == PlacementType.Stage &&
                !TryGetStagePaintData(out paintData))
            {
                string requiredTileName = stagePaintTileType == StagePaintTileType.Block ? "Stage Tile" : "Slope Tile";
                Debug.LogWarning($"Palette に {requiredTileName} が設定されていません");
                return;
            }

            // 開始セルと終了セルから、左下～右上の範囲を求める
            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            // Undo に記録して、Ctrl+Z で戻せるようにする
            Undo.RegisterCompleteObjectUndo(
                targetStageTilemap,
                currentPlacementType == PlacementType.Stage ? "Paint Stage Tiles" : "Erase Stage Tiles");

            // 範囲内の全セルに対してタイルを設定する
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);

                    if (currentPlacementType == PlacementType.Stage)
                    {
                        SetTileWithTransform(cell, paintData.tile, paintData.transform);
                    }
                    else
                    {
                        targetStageTilemap.SetTile(cell, null);
                    }
                }
            }
        }

        /// <summary>
        /// 指定セルのタイルを削除
        /// </summary>
        private void PaintTile(Vector3Int cell)
        {
            if (targetStageTilemap == null)
            {
                Debug.LogWarning("Stage Tilemap が設定されていません");
                return;
            }

            if (!TryGetStagePaintData(out StagePaintData paintData))
            {
                string requiredTileName = stagePaintTileType == StagePaintTileType.Block ? "Stage Tile" : "Slope Tile";
                Debug.LogWarning($"Palette に {requiredTileName} が設定されていません");
                return;
            }

            SetTileWithTransform(cell, paintData.tile, paintData.transform);
        }

        /// <summary>
        /// 範囲選択中のタイル矩形プレビューを描画
        /// </summary>
        private void EraseTile(Vector3Int cell)
        {
            if (targetStageTilemap == null)
            {
                Debug.LogWarning("Stage Tilemap が設定されていません");
                return;
            }

            targetStageTilemap.SetTile(cell, null);
        }

        /// <summary>
        /// タイルドラッグ中の状態をリセット
        /// </summary>
        private void DrawRectanglePreview(Vector3Int startCell, Vector3Int endCell)
        {
            if (targetStageTilemap == null)
            {
                return;
            }

            // ドラッグ範囲の最小セル座標を求める
            Vector3Int minCell = new Vector3Int(
                Mathf.Min(startCell.x, endCell.x),
                Mathf.Min(startCell.y, endCell.y),
                0);

            // ドラッグ範囲の最大セル座標を求める
            Vector3Int maxCell = new Vector3Int(
                Mathf.Max(startCell.x, endCell.x),
                Mathf.Max(startCell.y, endCell.y),
                0);

            // 範囲の左下ワールド座標を取得
            Vector3 worldMin = targetStageTilemap.CellToWorld(minCell);

            // 右上端は1セル分広げた位置を取得して、範囲全体を囲めるようにする
            Vector3 worldMax = targetStageTilemap.CellToWorld(new Vector3Int(maxCell.x + 1, maxCell.y + 1, 0));

            // ワイヤーキューブの中心座標を計算
            Vector3 center = (worldMin + worldMax) * 0.5f;
            center.z = 0f;

            // ワイヤーキューブのサイズを計算
            Vector3 size = worldMax - worldMin;
            size.z = 0.01f;

            // 元の Handles 色を退避
            Color oldColor = Handles.color;

            // Stage は緑、Erase は赤系で表示
            Handles.color = currentPlacementType == PlacementType.Stage
                ? new Color(0f, 1f, 0f, 1f)
                : new Color(1f, 0.3f, 0.3f, 1f);

            // 範囲プレビューをワイヤーキューブで描画
            Handles.DrawWireCube(center, size);

            // Handles 色を元に戻す
            Handles.color = oldColor;
        }

        /// <summary>
        /// タイルドラッグ中の状態をリセット
        /// </summary>
        private void CancelTileDrag()
        {
            //ドラッグ状態を解除し、初期化
            isTileDragging = false;
            dragStartCell = Vector3Int.zero;
            dragCurrentCell = Vector3Int.zero;

            // 最後に編集したセルを無効値に戻す
            lastDraggedCell = invalidCell;
        }

        /// <summary>
        /// プレイヤーをStartPointへ移動し、残っている速度もリセットする
        /// </summary>
        private void ResetPlayerToStartPoint()
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogWarning("PlayerController を持つオブジェクトが見つかりません。");
                return;
            }

            GameObject startPoint = GameObject.Find("StartPoint");
            if (startPoint == null)
            {
                Debug.LogWarning("Hierarchy に StartPoint が見つかりません。");
                return;
            }

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

            Vector3 targetPosition = startPoint.transform.position;
            targetPosition.z = player.transform.position.z;

            Undo.RecordObject(player.transform, "Reset Player To StartPoint");
            if (rb != null)
            {
                Undo.RecordObject(rb, "Reset Player Velocity");
            }

            if (rb != null)
            {
                // まず速度を止める
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;

                // 見た目側を即座に移動
                player.transform.position = targetPosition;

                // Transform変更を2D物理へ同期
                Physics2D.SyncTransforms();

                // 物理側の位置も合わせる
                rb.position = new Vector2(targetPosition.x, targetPosition.y);

                // 念のためもう一度速度を止める
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.Sleep();
            }
            else
            {
                player.transform.position = targetPosition;
            }

            Selection.activeGameObject = player.gameObject;

            // GameView / PlayerLoop の更新を促す
            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
            SceneView.RepaintAll();
            FocusGameView();
        }

        /// <summary>
        /// フォーカスをGameビューに維持する
        /// </summary>
        private static void FocusGameView()
        {
            EditorApplication.delayCall += () =>
            {
                Type gameViewType = Type.GetType("UnityEditor.GameView, UnityEditor");
                if (gameViewType != null)
                {
                    EditorWindow.FocusWindowIfItsOpen(gameViewType);
                }
            };
        }

        /// <summary>
        /// 指定したタイル範囲をSceneView上にワイヤー矩形でプレビュー表示する
        /// </summary>
        private void DrawBoundsPreview(BoundsInt bounds, Color color)
        {
            if (targetStageTilemap == null)
            {
                return;
            }

            Vector3 worldMin = targetStageTilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMin, 0));
            Vector3 worldMax = targetStageTilemap.CellToWorld(new Vector3Int(bounds.xMax, bounds.yMax, 0));

            Vector3 center = (worldMin + worldMax) * 0.5f;
            center.z = 0f;

            Vector3 size = worldMax - worldMin;
            size.z = 0.01f;

            Color oldColor = Handles.color;
            Handles.color = color;
            Handles.DrawWireCube(center, size);
            Handles.color = oldColor;
        }

        /// <summary>
        /// 指定セル1マスを半透明で塗ってプレビュー表示する
        /// </summary>
        private void DrawFilledCellPreview(Vector3Int cell, Color fillColor, Color outlineColor)
        {
            if (targetStageTilemap == null)
            {
                return;
            }

            Vector3 worldMin = targetStageTilemap.CellToWorld(cell);
            Vector3 worldMax = targetStageTilemap.CellToWorld(cell + new Vector3Int(1, 1, 0));

            Vector3[] verts = new Vector3[4]
            {
                 new Vector3(worldMin.x, worldMin.y, 0f),
                 new Vector3(worldMin.x, worldMax.y, 0f),
                 new Vector3(worldMax.x, worldMax.y, 0f),
                 new Vector3(worldMax.x, worldMin.y, 0f),
            };

            Color oldColor = Handles.color;
            Handles.DrawSolidRectangleWithOutline(verts, fillColor, outlineColor);
            Handles.color = oldColor;
        }

        private void DrawSlopeCellPreview(Vector3Int cell, SlopeCorner corner, Color fillColor, Color outlineColor)
        {
            if (targetStageTilemap == null)
            {
                return;
            }

            Vector3 worldMin = targetStageTilemap.CellToWorld(cell);
            Vector3 worldMax = targetStageTilemap.CellToWorld(cell + new Vector3Int(1, 1, 0));

            Vector3 bottomLeft = new Vector3(worldMin.x, worldMin.y, 0f);
            Vector3 bottomRight = new Vector3(worldMax.x, worldMin.y, 0f);
            Vector3 topLeft = new Vector3(worldMin.x, worldMax.y, 0f);
            Vector3 topRight = new Vector3(worldMax.x, worldMax.y, 0f);

            Vector3[] triangle = BuildSlopeTriangleVertices(corner, bottomLeft, bottomRight, topLeft, topRight);

            Color oldColor = Handles.color;

            Handles.color = fillColor;
            Handles.DrawAAConvexPolygon(triangle);

            Handles.color = outlineColor;
            Handles.DrawAAPolyLine(2f, triangle[0], triangle[1], triangle[2], triangle[0]);

            Handles.color = oldColor;
        }

        private static Vector3[] BuildSlopeTriangleVertices(
            SlopeCorner corner,
            Vector3 bottomLeft,
            Vector3 bottomRight,
            Vector3 topLeft,
            Vector3 topRight)
        {
            switch (corner)
            {
                case SlopeCorner.BottomLeft:
                    return new[] { bottomLeft, topLeft, bottomRight };
                case SlopeCorner.BottomRight:
                    return new[] { bottomRight, topRight, bottomLeft };
                case SlopeCorner.TopLeft:
                    return new[] { topLeft, bottomLeft, topRight };
                case SlopeCorner.TopRight:
                    return new[] { topRight, bottomRight, topLeft };
                default:
                    return new[] { bottomLeft, topLeft, bottomRight };
            }
        }

        /// <summary>
        /// クリップボード内の実タイルだけを、指定位置基準で半透明表示する
        /// </summary>
        private void DrawClipboardTilePreview(Vector3Int anchorCell, Color fillColor, Color outlineColor)
        {
            if (targetStageTilemap == null || clipboardTiles == null || clipboardTiles.Count == 0)
            {
                return;
            }

            foreach (ClipboardTile data in clipboardTiles)
            {
                Vector3Int previewCell = anchorCell + data.offset;
                DrawFilledCellPreview(previewCell, fillColor, outlineColor);
            }
        }

        /// <summary>
        /// 移動元範囲内に存在する実タイルだけを、移動先位置に半透明表示する
        /// </summary>
        private void DrawMoveTilePreview(BoundsInt sourceBounds, Vector3Int moveOffset, Color fillColor, Color outlineColor)
        {
            if (targetStageTilemap == null)
            {
                return;
            }

            for (int y = sourceBounds.yMin; y < sourceBounds.yMax; y++)
            {
                for (int x = sourceBounds.xMin; x < sourceBounds.xMax; x++)
                {
                    Vector3Int sourceCell = new Vector3Int(x, y, 0);
                    TileBase tile = targetStageTilemap.GetTile(sourceCell);

                    // 空白は描かない
                    if (tile == null)
                    {
                        continue;
                    }

                    Vector3Int previewCell = sourceCell + moveOffset;
                    DrawFilledCellPreview(previewCell, fillColor, outlineColor);
                }
            }
        }

        /// <summary>
        /// 選択モード時の入力処理を行う
        /// タイル範囲の選択、選択範囲の移動、コピー後の貼り付けプレビューと確定を担当
        /// </summary>
        private void HandleSelectionMode(SceneView sceneView, Event e)
        {
            // 選択モード用のショートカットキー入力を処理
            HandleSelectionShortcuts(e);

            // すでに選択済みの範囲がある場合は、その範囲を黄色で表示
            if (hasTileSelection)
            {
                DrawBoundsPreview(selectedBounds, Color.yellow);
            }

            // 現在ドラッグして範囲選択中なら、
            // 開始セルと現在セルからプレビュー範囲を作ってシアン色で表示
            if (isSelectingTiles)
            {
                BoundsInt previewBounds = CreateBoundsFromCells(dragStartCell, dragCurrentCell);
                DrawBoundsPreview(previewBounds, Color.cyan);
            }

            // 選択範囲を移動中なら、
            // 元の選択範囲に現在の移動量を加えた位置を緑色で表示
            if (isMovingSelection)
            {
                BoundsInt previewBounds = OffsetBounds(moveSourceBounds, currentMoveOffset);

                Color fillColor = new Color(0.2f, 1f, 0.2f, 0.22f);
                Color outlineColor = new Color(0.2f, 1f, 0.2f, 0.9f);

                // 実タイル形状を半透明で表示
                DrawMoveTilePreview(moveSourceBounds, currentMoveOffset, fillColor, outlineColor);

                // 外枠も残しておく
                DrawBoundsPreview(previewBounds, new Color(0.2f, 1f, 0.2f, 1f));
            }

            // 貼り付けプレビュー中の処理
            if (isPastePreview)
            {
                // マウス位置から現在の貼り付け先セルを取得できた場合、
                // その位置を基準に貼り付け範囲をプレビュー表示する
                if (TryGetCellFromMouse(e.mousePosition, out Vector3Int pasteCell))
                {
                    pastePreviewCell = pasteCell;

                    BoundsInt previewBounds = new BoundsInt(pasteCell.x, pasteCell.y, 0, clipboardSize.x, clipboardSize.y, 1);

                    Color fillColor = new Color(1f, 0.7f, 0.2f, 0.22f);
                    Color outlineColor = new Color(1f, 0.7f, 0.2f, 0.9f);

                    // コピー済みの実タイルだけ半透明で表示
                    DrawClipboardTilePreview(pasteCell, fillColor, outlineColor);

                    // 範囲枠も表示
                    DrawBoundsPreview(previewBounds, new Color(1f, 0.7f, 0.2f, 1f));
                }

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PasteClipboard(pastePreviewCell);
                    isPastePreview = false;
                    e.Use();
                }

                return;
            }

            // 左クリック以外はこのモードでは処理しない
            if (e.button != 0)
            {
                return;
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    {
                        // マウス位置からセル座標を取得できなければ何もしない
                        if (!TryGetCellFromMouse(e.mousePosition, out Vector3Int cell))
                        {
                            return;
                        }

                        // すでに選択範囲があり、クリック位置がその範囲内なら
                        // 新しい範囲選択ではなく「移動開始」とみなす
                        if (hasTileSelection && IsCellInsideBounds(cell, selectedBounds))
                        {
                            isMovingSelection = true;

                            // 移動開始時の基準セルを記録
                            moveStartCell = cell;

                            // 現在の移動量を初期化
                            currentMoveOffset = Vector3Int.zero;

                            // 元の選択範囲を保持
                            moveSourceBounds = selectedBounds;
                        }
                        else
                        {
                            // 選択範囲外をクリックした場合は新しい範囲選択を開始
                            isSelectingTiles = true;
                            dragStartCell = cell;
                            dragCurrentCell = cell;
                        }

                        // このクリックイベントは処理済み
                        e.Use();
                        break;
                    }

                case EventType.MouseDrag:
                    {
                        // マウス位置からセル座標を取得できなければ何もしない
                        if (!TryGetCellFromMouse(e.mousePosition, out Vector3Int cell))
                        {
                            return;
                        }

                        // 移動中なら、開始セルから現在セルまでの差分を移動量として更新
                        if (isMovingSelection)
                        {
                            currentMoveOffset = cell - moveStartCell;

                            // プレビューを更新するため再描画
                            sceneView.Repaint();
                            e.Use();
                        }
                        // 範囲選択中なら、現在のドラッグ位置を更新
                        else if (isSelectingTiles)
                        {
                            dragCurrentCell = cell;

                            // 選択範囲プレビューを更新するため再描画
                            sceneView.Repaint();
                            e.Use();
                        }

                        break;
                    }

                case EventType.MouseUp:
                    {
                        // マウス位置からセル座標を取得できなければ何もしない
                        if (!TryGetCellFromMouse(e.mousePosition, out Vector3Int cell))
                        {
                            return;
                        }

                        // 移動中だった場合は最終移動量を確定して移動処理を完了
                        if (isMovingSelection)
                        {
                            currentMoveOffset = cell - moveStartCell;
                            FinishMoveSelection();
                            e.Use();
                        }
                        // 範囲選択中だった場合は、開始セルと終了セルから選択範囲を確定
                        else if (isSelectingTiles)
                        {
                            dragCurrentCell = cell;
                            selectedBounds = CreateBoundsFromCells(dragStartCell, dragCurrentCell);

                            // 選択範囲を保持
                            hasTileSelection = true;

                            // 範囲選択状態を終了
                            isSelectingTiles = false;

                            // Scene全体を再描画して見た目を更新
                            SceneView.RepaintAll();
                            e.Use();
                        }

                        break;
                    }
            }
        }

        /// <summary>
        /// 選択モード中のショートカットキー入力を処理する
        /// コピー、貼り付け、貼り付けプレビューのキャンセルを担当
        /// </summary>
        private void HandleSelectionShortcuts(Event e)
        {
            // 文字入力中はショートカットを受け付けない
            // コピー・貼り付け処理が誤作動しないようにする
            if (EditorGUIUtility.editingTextField)
            {
                return;
            }

            bool actionKey = e.control;

            // Ctrl + Cで現在の選択範囲をコピー
            if (e.type == EventType.KeyDown && actionKey && e.keyCode == KeyCode.C)
            {
                // 選択範囲が存在する場合のみコピーを実行
                if (hasTileSelection)
                {
                    CopySelectionToClipboard();

                    // このキー入力は処理済みとして他へ渡さない
                    e.Use();
                }
                return;
            }

            // Ctrl + V / Command + V でクリップボード内容の貼り付けプレビューを開始
            if (e.type == EventType.KeyDown && actionKey && e.keyCode == KeyCode.V)
            {
                // クリップボードに有効なタイルデータがある場合のみ開始
                if (clipboardTiles != null && clipboardTiles.Count > 0)
                {
                    BeginPastePreview();

                    // SceneView を再描画して貼り付けプレビューを表示更新
                    SceneView.RepaintAll();

                    // このキー入力は処理済みとして他へ渡さない
                    e.Use();
                }
                return;
            }

            // Escape キーで貼り付けプレビューをキャンセル
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                // 貼り付けプレビュー中のみキャンセル処理を行う
                if (isPastePreview)
                {
                    isPastePreview = false;

                    // プレビュー表示を消すため再描画
                    SceneView.RepaintAll();

                    // このキー入力は処理済みとして他へ渡さない
                    e.Use();
                }
            }
        }

        /// <summary>
        /// 現在の選択範囲のタイルをクリップボード用配列にコピーする
        /// </summary>
        private void CopySelectionToClipboard()
        {
            // 選択範囲が存在しない、または対象 Tilemap が無い場合は何もしない
            if (!hasTileSelection || targetStageTilemap == null)
            {
                return;
            }

            clipboardTiles.Clear();

            bool hasAnyTile = false;
            int minTileX = int.MaxValue;
            int minTileY = int.MaxValue;
            int maxTileX = int.MinValue;
            int maxTileY = int.MinValue;

            // まずは存在するタイルだけ探して、実際の使用範囲を求める
            for (int y = selectedBounds.yMin; y < selectedBounds.yMax; y++)
            {
                for (int x = selectedBounds.xMin; x < selectedBounds.xMax; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    TileBase tile = targetStageTilemap.GetTile(cell);

                    if (tile == null)
                    {
                        continue;
                    }

                    hasAnyTile = true;

                    minTileX = Mathf.Min(minTileX, x);
                    minTileY = Mathf.Min(minTileY, y);
                    maxTileX = Mathf.Max(maxTileX, x);
                    maxTileY = Mathf.Max(maxTileY, y);
                }
            }

            if (!hasAnyTile)
            {
                clipboardSize = Vector3Int.zero;
                Debug.LogWarning("選択範囲内にコピーできるタイルがありません");
                return;
            }

            // 実際に存在するタイルだけを、最小座標基準の相対位置で保存
            for (int y = selectedBounds.yMin; y < selectedBounds.yMax; y++)
            {
                for (int x = selectedBounds.xMin; x < selectedBounds.xMax; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    TileBase tile = targetStageTilemap.GetTile(cell);

                    if (tile == null)
                    {
                        continue;
                    }

                    clipboardTiles.Add(new ClipboardTile
                    {
                        offset = new Vector3Int(x - minTileX, y - minTileY, 0),
                        tile = tile,
                        transform = targetStageTilemap.GetTransformMatrix(cell)
                    });
                }
            }

            clipboardSize = new Vector3Int(
                maxTileX - minTileX + 1,
                maxTileY - minTileY + 1,
                1);

        }

        /// <summary>
        /// クリップボード内のタイルを貼り付けるためのプレビュー状態を開始する
        /// </summary>
        private void BeginPastePreview()
        {
            // コピー済みのタイルが存在しない場合は貼り付けできない
            if (clipboardTiles == null || clipboardTiles.Count == 0)
            {
                Debug.LogWarning("コピーされたタイルがありません");
                return;
            }

            // 貼り付けプレビュー状態を有効化
            isPastePreview = true;
        }

        /// <summary>
        /// クリップボードに保持しているタイルを指定セルを基準位置として貼り付け、貼り付け後の範囲を選択状態にする
        /// </summary>
        private void PasteClipboard(Vector3Int anchorCell)
        {
            // 貼り付け先の Tilemap が存在しない、
            // またはクリップボードに貼り付け対象のタイルが無い場合は何もしない
            if (targetStageTilemap == null || clipboardTiles == null || clipboardTiles.Count == 0)
            {
                return;
            }

            // 貼り付け操作を Undo 対象として記録
            Undo.RecordObject(targetStageTilemap, "Paste Packed Stage Tiles");

            // クリップボード内の各タイルを、
            // 基準セルからの相対位置に応じて1枚ずつ貼り付ける
            foreach (ClipboardTile data in clipboardTiles)
            {
                Vector3Int targetCell = anchorCell + data.offset;
                SetTileWithTransform(targetCell, data.tile, data.transform);
            }

            // 貼り付け後の範囲を選択範囲として保持
            selectedBounds = new BoundsInt(
                anchorCell.x,
                anchorCell.y,
                0,
                clipboardSize.x,
                clipboardSize.y,
                1);

            // 選択範囲が存在する状態にする
            hasTileSelection = true;

            // SceneView を再描画して貼り付け結果を反映する
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 選択中の実タイルだけを移動先へ移し、移動後の範囲を選択状態として確定する
        /// </summary>
        private struct MovedTileData
        {
            public Vector3Int sourceCell;
            public Vector3Int destinationCell;
            public TileBase tile;
            public Matrix4x4 transform;
        }

        private void FinishMoveSelection()
        {
            if (targetStageTilemap == null)
            {
                return;
            }

            // 移動量が 0 なら何もしない
            if (currentMoveOffset == Vector3Int.zero)
            {
                isMovingSelection = false;
                currentMoveOffset = Vector3Int.zero;
                return;
            }

            List<MovedTileData> movedTiles = new List<MovedTileData>();

            // 選択範囲内の「実際に存在しているタイルだけ」を集める
            for (int y = moveSourceBounds.yMin; y < moveSourceBounds.yMax; y++)
            {
                for (int x = moveSourceBounds.xMin; x < moveSourceBounds.xMax; x++)
                {
                    Vector3Int sourceCell = new Vector3Int(x, y, 0);
                    TileBase tile = targetStageTilemap.GetTile(sourceCell);

                    if (tile == null)
                    {
                        continue;
                    }

                    movedTiles.Add(new MovedTileData
                    {
                        sourceCell = sourceCell,
                        destinationCell = sourceCell + currentMoveOffset,
                        tile = tile,
                        transform = targetStageTilemap.GetTransformMatrix(sourceCell)
                    });
                }
            }

            if (movedTiles.Count == 0)
            {
                isMovingSelection = false;
                currentMoveOffset = Vector3Int.zero;
                return;
            }

            Undo.RecordObject(targetStageTilemap, "Move Stage Tiles");

            // 元位置にあった実タイルだけ消す
            foreach (MovedTileData data in movedTiles)
            {
                targetStageTilemap.SetTile(data.sourceCell, null);
            }

            // 移動先に実タイルだけ置く
            foreach (MovedTileData data in movedTiles)
            {
                SetTileWithTransform(data.destinationCell, data.tile, data.transform);
            }

            selectedBounds = OffsetBounds(moveSourceBounds, currentMoveOffset);
            hasTileSelection = true;

            isMovingSelection = false;
            currentMoveOffset = Vector3Int.zero;

            SceneView.RepaintAll();
        }

        /// <summary>
        /// タイル選択を解除し初期化
        /// </summary>
        private void ClearTileSelection()
        {
            hasTileSelection = false;
            isSelectingTiles = false;
            isMovingSelection = false;
            isPastePreview = false;
            currentMoveOffset = Vector3Int.zero;
        }

        /// <summary>
        /// 2つのセル座標から、それらを含む矩形範囲を BoundsInt として作成する
        /// </summary>
        private static BoundsInt CreateBoundsFromCells(Vector3Int a, Vector3Int b)
        {
            // 2点のうち左端と右端のX座標を求める
            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);

            // 2点のうち下端と上端のY座標を求める
            int minY = Mathf.Min(a.y, b.y);
            int maxY = Mathf.Max(a.y, b.y);

            // min ～ max を両端含みで扱うため、サイズは +1 する
            return new BoundsInt(
                minX, minY, 0,
                maxX - minX + 1,
                maxY - minY + 1,
                1);
        }

        /// <summary>
        /// 指定した BoundsInt をオフセット分だけ平行移動した新しい範囲を返す
        /// </summary>
        private static BoundsInt OffsetBounds(BoundsInt bounds, Vector3Int offset)
        {
            // 左下座標にオフセットを加え、サイズはそのまま維持する
            return new BoundsInt(
                bounds.xMin + offset.x,
                bounds.yMin + offset.y,
                0,
                bounds.size.x,
                bounds.size.y,
                1);
        }

        /// <summary>
        /// 指定したセル座標が BoundsInt の範囲内に含まれているかを判定する
        /// </summary>
        private static bool IsCellInsideBounds(Vector3Int cell, BoundsInt bounds)
        {
            // xMin, yMin は含み、xMax, yMax は含まない範囲として判定
            return cell.x >= bounds.xMin && cell.x < bounds.xMax &&
                   cell.y >= bounds.yMin && cell.y < bounds.yMax;
        }

        //Tile Paletteを直で使いたくなった時用に残す
#if false
        /// <summary>
        /// Tile Paletteでステージを配置するモードに
        /// </summary>
        private void OpenAndSyncTilePaletteAsPaint()
        {
            OpenAndSyncTilePalette();
            ToolManager.SetActiveTool<PaintTool>();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Tile Paletteでステージを削除するモードに
        /// </summary>
        private void OpenAndSyncTilePaletteAsErase()
        {
            OpenAndSyncTilePalette();
            ToolManager.SetActiveTool<EraseTool>();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Tile Palette を開いて対象 Tilemap を同期する
        /// </summary>
        private void OpenAndSyncTilePalette()
        {
            if (targetStageTilemap == null)
            {
                Debug.LogWarning("Stage Tilemap が設定されていません");
                return;
            }

            Selection.activeGameObject = targetStageTilemap.gameObject;

            // Tile Palette を開く
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");

            // ペイント対象をこの Tilemap に同期
            GridPaintingState.scenePaintTarget = targetStageTilemap.gameObject;
        }
#endif 


    }
}
