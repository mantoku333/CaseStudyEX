using UnityEngine;
using UnityEngine.Tilemaps;

namespace EditorTools
{
    /// <summary>
    /// ステージエディタで使用するPrefabやTileを保持するデータ
    /// </summary>
    [CreateAssetMenu(fileName = "StageEditorPalette", menuName = "GameName/Editor/Stage Editor Palette")]
    public class StageEditorPalette : ScriptableObject
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private TileBase stageTile;

        /// <summary>プレイヤー配置用Prefab</summary>
        public GameObject PlayerPrefab => playerPrefab;

        /// <summary>敵配置用Prefab</summary>
        public GameObject EnemyPrefab => enemyPrefab;

        /// <summary>ステージ描画用Tile</summary>
        public TileBase StageTile => stageTile;
    }
}
