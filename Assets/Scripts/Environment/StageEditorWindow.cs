using GameName.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameName.EditorTools
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

        private StageEditorPalette palette;
        private PlayerStatsData playerStatsData;
        private Tilemap targetStageTilemap;
        private PlacementType currentPlacementType = PlacementType.None;
        private UnityEditor.Editor cachedStatsEditor;

        [MenuItem("Tools/GameName/Stage Editor")]
        public static void Open()
        {
            StageEditorWindow window = GetWindow<StageEditorWindow>("Stage Editor");
            window.minSize = new Vector2(420f, 340f);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
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
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Placement Mode", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Player"))
                {
                    currentPlacementType = PlacementType.Player;
                }

                if (GUILayout.Button("Enemy"))
                {
                    currentPlacementType = PlacementType.Enemy;
                }

                if (GUILayout.Button("Stage"))
                {
                    currentPlacementType = PlacementType.Stage;
                }

                if (GUILayout.Button("Erase"))
                {
                    currentPlacementType = PlacementType.Erase;
                }

                if (GUILayout.Button("Stop"))
                {
                    currentPlacementType = PlacementType.None;
                }
            }

            EditorGUILayout.HelpBox($"現在の配置モード: {currentPlacementType}", MessageType.Info);

            if ((currentPlacementType == PlacementType.Stage || currentPlacementType == PlacementType.Erase) &&
                targetStageTilemap == null)
            {
                EditorGUILayout.HelpBox("Stage または Erase モードを使うには Stage Tilemap の設定が必要です", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player Status", EditorStyles.boldLabel);

            if (playerStatsData == null)
            {
                EditorGUILayout.HelpBox("PlayerStatsData を割り当てると、ここでステータス編集ができます", MessageType.Warning);
                return;
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
            if (currentPlacementType == PlacementType.None || palette == null)
            {
                return;
            }

            Event e = Event.current;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 300f, 70f), GUI.skin.window);
            GUILayout.Label($"配置モード: {currentPlacementType}");
            GUILayout.Label("左クリックで配置 / Escで終了");
            GUILayout.Label("Tilemapはセル単位で配置されます");
            GUILayout.EndArea();
            Handles.EndGUI();

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                currentPlacementType = PlacementType.None;
                Repaint();
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Vector3 worldPosition = GetMouseWorldPosition(e.mousePosition);

                switch (currentPlacementType)
                {
                    case PlacementType.Player:
                        PlacePrefabOnGrid(palette.PlayerPrefab, worldPosition);
                        break;

                    case PlacementType.Enemy:
                        PlacePrefabOnGrid(palette.EnemyPrefab, worldPosition);
                        break;

                    case PlacementType.Stage:
                        PaintTile(worldPosition);
                        break;

                    case PlacementType.Erase:
                        EraseTile(worldPosition);
                        break;
                }

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
        /// Tilemapのセルにタイルを配置
        /// </summary>
        private void PaintTile(Vector3 worldPosition)
        {
            if (targetStageTilemap == null)
            {
                Debug.LogWarning("Stage Tilemap が設定されていません");
                return;
            }

            if (palette.StageTile == null)
            {
                Debug.LogWarning("Palette に Stage Tile が設定されていません");
                return;
            }

            Vector3Int cell = targetStageTilemap.WorldToCell(worldPosition);

            Undo.RecordObject(targetStageTilemap, "Paint Stage Tile");
            targetStageTilemap.SetTile(cell, palette.StageTile);
            EditorUtility.SetDirty(targetStageTilemap);
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
    }
}
