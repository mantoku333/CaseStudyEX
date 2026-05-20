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
    private int fallThroughFloorLayer = -1;

    private void Awake()
    {
        fallThroughFloorLayer = LayerMask.NameToLayer(FallThroughFloorLayerName);
    }

    /// <summary>
    ///接地判定を返すメソッド
    ///物理判定の更新毎に呼ぶ必要がある
    /// </summary>
    /// <returns></returns>
    public bool IsGround()
    {
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
}
