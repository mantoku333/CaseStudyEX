using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameName.Audio
{
    [DisallowMultipleComponent]
    public sealed class SurfaceAudioSource : MonoBehaviour
    {
        [SerializeField] private SurfaceAudioProfile profile;
        [SerializeField] private TileSurfaceProfile[] tileProfiles = new TileSurfaceProfile[0];

        public SurfaceAudioProfile Profile => profile;

        private Tilemap tilemap;

        private void Awake()
        {
            tilemap = GetComponent<Tilemap>();
        }

        public SurfaceAudioProfile ResolveProfile(Vector2 worldPosition)
        {
            SurfaceAudioProfile tileProfile = ResolveTileProfile(worldPosition);
            if (tileProfile != null)
            {
                return tileProfile;
            }

            return profile;
        }

        private SurfaceAudioProfile ResolveTileProfile(Vector2 worldPosition)
        {
            if (tileProfiles == null || tileProfiles.Length == 0)
            {
                return null;
            }

            if (tilemap == null)
            {
                tilemap = GetComponent<Tilemap>();
            }

            if (tilemap == null)
            {
                return null;
            }

            Vector3Int cellPosition = tilemap.WorldToCell(worldPosition);
            TileBase tile = tilemap.GetTile(cellPosition);
            if (tile == null)
            {
                return null;
            }

            for (int i = 0; i < tileProfiles.Length; i++)
            {
                TileSurfaceProfile tileProfile = tileProfiles[i];
                if (tileProfile.Tile == tile)
                {
                    return tileProfile.Profile;
                }
            }

            return null;
        }

        [System.Serializable]
        private sealed class TileSurfaceProfile
        {
            [SerializeField] private TileBase tile;
            [SerializeField] private SurfaceAudioProfile profile;

            public TileBase Tile => tile;
            public SurfaceAudioProfile Profile => profile;
        }
    }
}
