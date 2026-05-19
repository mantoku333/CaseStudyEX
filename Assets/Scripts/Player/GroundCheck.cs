using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    private const string FallThroughFloorLayerName = "FallThroughFloor";

    [Header("地面のレイヤー")]
    [SerializeField] private LayerMask groundLayer;     //地面のレイヤー
    [SerializeField] private bool includeFallThroughFloorLayer = true;

    private readonly HashSet<Collider2D> groundColliders = new HashSet<Collider2D>();
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private Collider2D groundCheckCollider;
    private int fallThroughFloorLayer = -1;

    private void Awake()
    {
        fallThroughFloorLayer = LayerMask.NameToLayer(FallThroughFloorLayerName);
        groundCheckCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    ///接地判定を返すメソッド
    ///物理判定の更新毎に呼ぶ必要がある
    /// </summary>
    /// <returns></returns>
    public bool IsGround()
    {
        // Trigger の Enter/Exit が抜けても接地状態がずれないよう、問い合わせ時に現在の重なりを取り直す。
        RefreshGroundColliders();
        return groundColliders.Count > 0;
    }

    /// <summary>
    /// 地面に接触しているかどうかをレイヤーで判定する関数
    /// </summary>
    /// <param name="layer"></param>
    /// <returns></returns>
    private bool IsInLayer(int layer)
    {
        if ((groundLayer & (1 << layer)) != 0)
        {
            return true;
        }

        if (!includeFallThroughFloorLayer)
        {
            return false;
        }

        return fallThroughFloorLayer >= 0 && layer == fallThroughFloorLayer;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsInLayer(collision.gameObject.layer))
        {
            groundColliders.Add(collision);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (IsInLayer(collision.gameObject.layer))
        {
            groundColliders.Add(collision);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (IsInLayer(collision.gameObject.layer))
        {
            groundColliders.Remove(collision);
        }
    }

    private void OnDisable()
    {
        groundColliders.Clear();
    }

    private void RefreshGroundColliders()
    {
        groundColliders.Clear();

        if (groundCheckCollider == null)
        {
            groundCheckCollider = GetComponent<Collider2D>();
        }

        if (groundCheckCollider == null || !groundCheckCollider.enabled)
        {
            return;
        }

        ContactFilter2D contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        contactFilter.SetLayerMask(BuildGroundMask());

        // GroundCheck 自身のトリガー形状で、Ground と FallThroughFloor の現在の重なりを直接確認する。
        int overlapCount = groundCheckCollider.Overlap(contactFilter, overlapResults);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D overlap = overlapResults[i];
            if (overlap != null && IsInLayer(overlap.gameObject.layer))
            {
                groundColliders.Add(overlap);
            }

            overlapResults[i] = null;
        }
    }

    private LayerMask BuildGroundMask()
    {
        int mask = groundLayer.value;
        if (includeFallThroughFloorLayer && fallThroughFloorLayer >= 0)
        {
            // すり抜け床も通常の地面と同じ接地判定として扱う。
            mask |= 1 << fallThroughFloorLayer;
        }

        return mask;
    }
}
