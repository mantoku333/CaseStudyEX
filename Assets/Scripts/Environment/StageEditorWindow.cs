using Player;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Tilemaps;
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
            Rectangle
        }

        [SerializeField] private TileEditMode tileEditMode = TileEditMode.Brush;

        private bool isTileDragging;
        private Vector3Int dragStartCell;
        private Vector3Int dragCurrentCell;
        private Vector3Int lastDraggedCell = invalidCell;

        //最小数値の座標を無効座標と指定
        private static readonly Vector3Int invalidCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

        [SerializeField] private StageEditorPalette palette;
        [SerializeField] private PlayerStatsData playerStatsData;
        [SerializeField] private Tilemap targetStageTilemap;
        [SerializeField] private PlacementType currentPlacementType = PlacementType.None;
        private UnityEditor.Editor cachedStatsEditor;

        private const string PaletteGuidKey = "StageEditor.PaletteGuid";
        private const string PlayerStatsGuidKey = "StageEditor.PlayerStatsGuid";

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

            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SaveEditorPrefs();
            SceneView.duringSceneGui -= OnSceneGui;
            DestroyCachedEditor();
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Placement Mode", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                //プレイヤー配置モード
                if (GUILayout.Button("Player"))
                {
                    CancelTileDrag();
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

            if (currentPlacementType == PlacementType.Stage || currentPlacementType == PlacementType.Erase)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Tile Edit Mode", EditorStyles.boldLabel);
                tileEditMode = (TileEditMode)GUILayout.Toolbar(
                    (int)tileEditMode,
                    new[] { "長押しペイント", "範囲選択" });
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
                (palette == null || palette.StageTile == null))
            {
                EditorGUILayout.HelpBox("Stage モードを使うには Palette に Stage Tile の設定が必要です", MessageType.Warning);
            }

            if (cachedStatsEditor == null)
            {
                RefreshStatsEditor();
            }

            if (cachedStatsEditor != null)
            {
                cachedStatsEditor.OnInspectorGUI();

                if (GUI.changed)
                {
                    EditorUtility.SetDirty(playerStatsData);
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
            GUILayout.BeginArea(new Rect(10f, 10f, 340f, 95f), GUI.skin.window);
            GUILayout.Label($"配置モード: {currentPlacementType}");

            // Stage / Erase の場合は、タイル編集用の説明を表示
            if (currentPlacementType == PlacementType.Stage || currentPlacementType == PlacementType.Erase)
            {
                GUILayout.Label($"編集方式: {tileEditMode}");

                // 編集方式ごとの操作説明を表示
                if (tileEditMode == TileEditMode.Brush)
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
                // タイルドラッグ中の状態をリセット
                CancelTileDrag();

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
        /// Tilemapのセルのタイルを削除
        /// </summary>
        private void EraseTile(Vector3 worldPosition)
        {
            if (targetStageTilemap == null)
            {
                Debug.LogWarning("Stage Tilemap が設定されていません");
                return;
            }

            Vector3Int cell = targetStageTilemap.WorldToCell(worldPosition);

            Undo.RecordObject(targetStageTilemap, "Erase Stage Tile");
            targetStageTilemap.SetTile(cell, null);
            EditorUtility.SetDirty(targetStageTilemap);
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
        }

        /// <summary>
        /// 保存していたアセット参照を復元
        /// </summary>
        private void LoadEditorPrefs()
        {
            palette = LoadAssetFromGuid<StageEditorPalette>(PaletteGuidKey);
            playerStatsData = LoadAssetFromGuid<PlayerStatsData>(PlayerStatsGuidKey);

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
            currentPlacementType = PlacementType.None;

            palette = null;
            playerStatsData = null;
            targetStageTilemap = null;

            DestroyCachedEditor();

            EditorPrefs.DeleteKey(PaletteGuidKey);
            EditorPrefs.DeleteKey(PlayerStatsGuidKey);

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

            // Stage配置時は、配置する Tile が設定されていないと処理できないのでreturn
            if (currentPlacementType == PlacementType.Stage &&
                (palette == null || palette.StageTile == null))
            {
                return;
            }

            // Rectangle モードでドラッグ中なら、現在の範囲プレビューを描画する
            if (tileEditMode == TileEditMode.Rectangle && isTileDragging)
            {
                DrawRectanglePreview(dragStartCell, dragCurrentCell);
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

            if (currentPlacementType == PlacementType.Stage &&
                (palette == null || palette.StageTile == null))
            {
                Debug.LogWarning("Palette に Stage Tile が設定されていません");
                return;
            }

            // 開始セルと終了セルから、左下～右上の範囲を求める
            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            // Undo に記録して、Ctrl+Z で戻せるようにする
            Undo.RecordObject(
                targetStageTilemap,
                currentPlacementType == PlacementType.Stage ? "Paint Stage Tiles" : "Erase Stage Tiles");

            // Stage なら配置用 Tile、Erase なら null を使う
            TileBase tile = currentPlacementType == PlacementType.Stage ? palette.StageTile : null;

            // 範囲内の全セルに対してタイルを設定する
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    targetStageTilemap.SetTile(new Vector3Int(x, y, 0), tile);
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

            if (palette == null || palette.StageTile == null)
            {
                Debug.LogWarning("Palette に Stage Tile が設定されていません");
                return;
            }

            Undo.RecordObject(targetStageTilemap, "Paint Stage Tile");
            targetStageTilemap.SetTile(cell, palette.StageTile);
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

            Undo.RecordObject(targetStageTilemap, "Erase Stage Tile");
            targetStageTilemap.SetTile(cell, null);
            EditorUtility.SetDirty(targetStageTilemap);
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
