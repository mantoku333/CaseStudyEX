using EditorTools;
using Metroidvania.Player;
using Player;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap))]
[AddComponentMenu("Environment/Water Floor Tilemap Damage")]
public sealed class WaterFloorTilemapDamage : MonoBehaviour
{
    private const int PlayerHitCapacity = 32;
    private const float MinProbePadding = 0.04f;

    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float topContactTolerance = 0.08f;
    [SerializeField] private WaterFloorTileSet waterTiles = new WaterFloorTileSet();

    private readonly Collider2D[] playerHits = new Collider2D[PlayerHitCapacity];
    private ContactFilter2D playerContactFilter;
    private Tilemap tilemap;

    public void SetWaterTiles(WaterFloorTileSet source)
    {
        EnsureWaterTileSet();
        waterTiles.CopyFrom(source);
    }

    private void Awake()
    {
        ResolveTilemap();
        EnsureWaterTileSet();
        BuildPlayerContactFilter();
    }

    private void FixedUpdate()
    {
        DamagePlayersStandingOnWater();
    }

    private void DamagePlayersStandingOnWater()
    {
        if (tilemap == null)
        {
            ResolveTilemap();
        }

        if (tilemap == null || waterTiles == null || !TryGetTilemapWorldBounds(out Bounds bounds))
        {
            return;
        }

        float padding = Mathf.Max(MinProbePadding, topContactTolerance);
        Vector2 probeSize = new Vector2(
            bounds.size.x + (padding * 2f),
            bounds.size.y + (padding * 2f));

        int hitCount = Physics2D.OverlapBox(bounds.center, probeSize, 0f, playerContactFilter, playerHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = playerHits[i];
            playerHits[i] = null;
            TryDamagePlayer(candidate);
        }
    }

    private void TryDamagePlayer(Collider2D candidate)
    {
        if (!TryResolvePlayerCollider(
                candidate,
                out PlayerHealth playerHealth,
                out PlayerDamageFlash playerDamageFlash,
                out Collider2D playerCollider))
        {
            return;
        }

        if (!IsPlayerBodyStandingOnWater(playerCollider))
        {
            return;
        }

        if (playerHealth.TryTakeDamage(damage))
        {
            playerDamageFlash?.PlayFlashForced();
        }
    }

    private bool IsPlayerBodyStandingOnWater(Collider2D playerCollider)
    {
        if (playerCollider == null || tilemap == null || waterTiles == null)
        {
            return false;
        }

        Bounds playerBounds = playerCollider.bounds;
        float tolerance = Mathf.Max(0f, topContactTolerance);
        float allowedGap = Mathf.Max(tolerance, Physics2D.defaultContactOffset * 2f);
        float inset = Mathf.Min(0.001f, playerBounds.size.x * 0.25f);
        float minX = playerBounds.min.x + inset;
        float maxX = playerBounds.max.x - inset;
        if (minX > maxX)
        {
            minX = playerBounds.center.x;
            maxX = playerBounds.center.x;
        }

        Vector3Int minCell = tilemap.WorldToCell(new Vector3(minX, playerBounds.min.y - allowedGap, 0f));
        Vector3Int maxCell = tilemap.WorldToCell(new Vector3(maxX, playerBounds.min.y + tolerance, 0f));
        int xMin = Mathf.Min(minCell.x, maxCell.x);
        int xMax = Mathf.Max(minCell.x, maxCell.x);
        int yMin = Mathf.Min(minCell.y, maxCell.y);
        int yMax = Mathf.Max(minCell.y, maxCell.y);

        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                if (IsPlayerBodyStandingOnWaterCell(new Vector3Int(x, y, 0), playerBounds, tolerance, allowedGap))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPlayerBodyStandingOnWaterCell(
        Vector3Int cell,
        Bounds playerBounds,
        float tolerance,
        float allowedGap)
    {
        if (!waterTiles.Contains(tilemap.GetTile(cell)))
        {
            return false;
        }

        Bounds cellBounds = GetCellWorldBounds(cell);
        float floorTop = cellBounds.max.y;
        float playerBottom = playerBounds.min.y;

        if (playerBottom < floorTop - tolerance)
        {
            return false;
        }

        if (playerBottom > floorTop + allowedGap)
        {
            return false;
        }

        float horizontalOverlap =
            Mathf.Min(playerBounds.max.x, cellBounds.max.x) -
            Mathf.Max(playerBounds.min.x, cellBounds.min.x);

        return horizontalOverlap > 0f;
    }

    private bool TryResolvePlayerCollider(
        Collider2D candidate,
        out PlayerHealth playerHealth,
        out PlayerDamageFlash playerDamageFlash,
        out Collider2D playerCollider)
    {
        playerHealth = null;
        playerDamageFlash = null;
        playerCollider = null;

        if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(candidate, out playerHealth, out playerCollider))
        {
            return false;
        }

        if (!MatchesPlayerTag(playerCollider.gameObject, playerHealth))
        {
            return false;
        }

        playerDamageFlash = ResolvePlayerDamageFlash(playerCollider, playerHealth);
        return true;
    }

    private static PlayerDamageFlash ResolvePlayerDamageFlash(Collider2D playerCollider, PlayerHealth playerHealth)
    {
        PlayerDamageFlash damageFlash = playerCollider.GetComponent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerCollider.GetComponentInParent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerHealth.GetComponent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerHealth.GetComponentInParent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        return playerHealth.GetComponentInChildren<PlayerDamageFlash>(true);
    }

    private bool MatchesPlayerTag(GameObject hitObject, PlayerHealth playerHealth)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return true;
        }

        if (hitObject != null && hitObject.CompareTag(playerTag))
        {
            return true;
        }

        if (playerHealth.CompareTag(playerTag))
        {
            return true;
        }

        return playerHealth.transform.root != null && playerHealth.transform.root.CompareTag(playerTag);
    }

    private bool TryGetTilemapWorldBounds(out Bounds bounds)
    {
        BoundsInt cellBounds = tilemap.cellBounds;
        if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
        {
            bounds = default;
            return false;
        }

        Vector3 min = tilemap.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
        Vector3 max = tilemap.CellToWorld(new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0));
        Vector3 size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 0f);
        if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon)
        {
            bounds = default;
            return false;
        }

        bounds = new Bounds((min + max) * 0.5f, size);
        return true;
    }

    private Bounds GetCellWorldBounds(Vector3Int cell)
    {
        Vector3 min = tilemap.CellToWorld(cell);
        Vector3 max = tilemap.CellToWorld(cell + new Vector3Int(1, 1, 0));
        Vector3 size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 0f);
        return new Bounds((min + max) * 0.5f, size);
    }

    private void ResolveTilemap()
    {
        if (tilemap == null)
        {
            tilemap = GetComponent<Tilemap>();
        }
    }

    private void EnsureWaterTileSet()
    {
        if (waterTiles == null)
        {
            waterTiles = new WaterFloorTileSet();
        }
    }

    private void BuildPlayerContactFilter()
    {
        playerContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        playerContactFilter.SetLayerMask(PlayerBodyColliderUtility.GetPlayerBodyLayerMask());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        damage = Mathf.Max(1, damage);
        topContactTolerance = Mathf.Max(0f, topContactTolerance);
        ResolveTilemap();
        EnsureWaterTileSet();
        BuildPlayerContactFilter();
    }
#endif
}
