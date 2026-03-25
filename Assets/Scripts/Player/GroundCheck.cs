using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    [Header("地面のレイヤー")]
    [SerializeField] private LayerMask groundLayer;     //地面のレイヤー

    private bool isGround = false;  //接地しているかどうか
    private bool isGroundEnter, isGroundStay, isGroundExit;　//接地判定のフラグ

    /// <summary>
    ///接地判定を返すメソッド
    ///物理判定の更新毎に呼ぶ必要がある
    /// </summary>
    /// <returns></returns>
    public bool IsGround()
    {
        if (isGroundEnter || isGroundStay)
        {
            isGround = true;
        }
        else if (isGroundExit)
        {
            isGround = false;
        }

        isGroundEnter = false;
        isGroundStay = false;
        isGroundExit = false;

        return isGround;
    }

    /// <summary>
    /// 地面に接触しているかどうかをレイヤーで判定する関数
    /// </summary>
    /// <param name="layer"></param>
    /// <returns></returns>
    private bool IsInLayer(int layer)
    {
        return (groundLayer & (1 << layer)) != 0;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsInLayer(collision.gameObject.layer))
        {
            isGroundEnter = true;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (IsInLayer(collision.gameObject.layer))
        {
            isGroundStay = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (IsInLayer(collision.gameObject.layer))
        {
            isGroundExit = true;
        }
    }
}
