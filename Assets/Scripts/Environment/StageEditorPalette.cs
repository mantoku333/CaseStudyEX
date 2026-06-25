using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EditorTools
{
    public enum StageBlockFace
    {
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I
    }

    [Serializable]
    public class StageBlockTileSet
    {
        [SerializeField] private string displayName;
        [SerializeField] private TileBase a;
        [SerializeField] private TileBase b;
        [SerializeField] private TileBase c;
        [SerializeField] private TileBase d;
        [SerializeField] private TileBase e;
        [SerializeField] private TileBase f;
        [SerializeField] private TileBase g;
        [SerializeField] private TileBase h;
        [SerializeField] private TileBase i;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Stage" : displayName;

        public TileBase GetTile(StageBlockFace face)
        {
            switch (face)
            {
                case StageBlockFace.A:
                    return a;
                case StageBlockFace.B:
                    return b;
                case StageBlockFace.C:
                    return c;
                case StageBlockFace.D:
                    return d;
                case StageBlockFace.E:
                    return e;
                case StageBlockFace.F:
                    return f;
                case StageBlockFace.G:
                    return g;
                case StageBlockFace.H:
                    return h;
                case StageBlockFace.I:
                    return i;
                default:
                    return null;
            }
        }

        public bool Contains(TileBase tile)
        {
            if (tile == null)
            {
                return false;
            }

            return tile == a ||
                   tile == b ||
                   tile == c ||
                   tile == d ||
                   tile == e ||
                   tile == f ||
                   tile == g ||
                   tile == h ||
                   tile == i;
        }

        public bool HasTile(StageBlockFace face)
        {
            return GetTile(face) != null;
        }

        public bool HasAllTiles()
        {
            return a != null &&
                   b != null &&
                   c != null &&
                   d != null &&
                   e != null &&
                   f != null &&
                   g != null &&
                   h != null &&
                   i != null;
        }

        public TileBase FirstAvailableTile()
        {
            for (int index = 0; index < 9; index++)
            {
                TileBase tile = GetTile((StageBlockFace)index);
                if (tile != null)
                {
                    return tile;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Data used by the Stage Editor for prefabs and block tiles.
    /// </summary>
    [CreateAssetMenu(fileName = "StageEditorPalette", menuName = "Editor/Stage Editor Palette")]
    public class StageEditorPalette : ScriptableObject
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private TileBase stageTile;
        [SerializeField] private TileBase slopeTile;
        [SerializeField] private StageBlockTileSet stage1Blocks;
        [SerializeField] private StageBlockTileSet stage2Blocks;
        [SerializeField] private StageBlockTileSet stage3Blocks;

        /// <summary>Prefab used for player placement.</summary>
        public GameObject PlayerPrefab => playerPrefab;

        /// <summary>Prefab used for enemy placement.</summary>
        public GameObject EnemyPrefab => enemyPrefab;

        /// <summary>Legacy temporary stage block tile.</summary>
        public TileBase StageTile => stageTile;

        /// <summary>Tile used to recognize existing temporary block maps.</summary>
        public TileBase LegacyStageTile => stageTile;

        /// <summary>Deprecated slope tile kept for serialized compatibility.</summary>
        public TileBase SlopeTile => slopeTile;

        public StageBlockTileSet Stage1Blocks => stage1Blocks;

        public StageBlockTileSet Stage2Blocks => stage2Blocks;

        public StageBlockTileSet Stage3Blocks => stage3Blocks;

        public StageBlockTileSet GetBlockTileSet(int index)
        {
            switch (index)
            {
                case 0:
                    return stage1Blocks;
                case 1:
                    return stage2Blocks;
                case 2:
                    return stage3Blocks;
                default:
                    return null;
            }
        }
    }
}
