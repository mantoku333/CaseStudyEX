using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [Header("銃の設定")]
    [SerializeField] private float coolTime = 3.0f;           //銃のクールタイム
    [SerializeField] private float airRecoilPower  = 25.0f;   //空中にいる時の弾の反動
    [SerializeField] private float jumpRecoilPower = 45.0f;   //Eキーで飛び上がる時の反動量

    [Header("地面判定")]
    [SerializeField] private GroundCheck groundCheck;   //地面判定のスクリプト

    private bool isRecoiling = false;   　//反動が起きているかどうか

    private float currentCoolTime = 0.0f; //クールタイムの残り時間    

    private Rigidbody2D rigidBody2d;      //反動を加えるためのRigidbody2D

    void Start()
    {
        //Rigidbody2Dの取得
        rigidBody2d = GetComponentInParent<Rigidbody2D>();
    }

    void Update()
    {
        //クールタイムの更新
        if (currentCoolTime > 0)
        {
            currentCoolTime -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 銃の発射処理を行う関数
    /// </summary>
    /// <param name="direction">銃の発射方向</param>
    public void Shoot(Vector2 direction)
    {
        if (currentCoolTime > 0) { return; }

        //銃の反動を適用
        ApplyRecoil(direction);

        currentCoolTime = coolTime;

        //Debug.Log("Shoot! Cooldown started.");
    }

    /// <summary>
    /// 銃の反動を適用する関数
    /// </summary>
    /// <param name="direction">反動の方向</param>
    void ApplyRecoil(Vector2 direction)
    {
        if (rigidBody2d == null)
        {
            return;
        }

        isRecoiling = true;

        //反動中は空気抵抗を増やす
        rigidBody2d.linearDamping = 2.0f;

        //現在の速度を取得
        Vector2 velocity = rigidBody2d.linearVelocity;
        Vector2 recoil = -direction.normalized * airRecoilPower;
        rigidBody2d.AddForce(recoil, ForceMode2D.Impulse);

        Invoke(nameof(EndRecoil), 0.1f);
    }

    /// <summary>
    /// 地面からの跳ね上がりの反動を適用する関数
    /// </summary>
    public void JumpRecoil()
    {
        if (currentCoolTime > 0)
        {
            return;
        }

        if (rigidBody2d == null)
        {
            return;
        }

        isRecoiling = true;

        rigidBody2d.linearDamping = 5.0f;

        Vector2 velocity = rigidBody2d.linearVelocity;

        //縦速度リセット
        velocity.y = 0;
        rigidBody2d.linearVelocity = velocity;

        Vector2 jumpVelocity = new Vector2(velocity.x, jumpRecoilPower);
        rigidBody2d.linearVelocity = jumpVelocity;

        currentCoolTime = coolTime;

        Invoke(nameof(EndRecoil), 0.1f);

        //Debug.Log("Jump Recoil!");
    }

    /// <summary>
    /// 反動終了の処理を行う関数
    /// </summary>
    void EndRecoil()
    {
        isRecoiling = false;
        rigidBody2d.linearDamping = 0.0f;
    }

    /// <summary>
    /// 反動が起きているかどうかを返すGet関数
    /// </summary>
    /// <returns>反動が起きているかのbool値</returns>
    public bool GetRecoiling()
    {
        return isRecoiling;
    }
}
