using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    [Header("銃の設定")]
    [SerializeField] private float coolTime = 3.0f;           //銃のクールタイム
    [SerializeField] private float airRecoilPower  = 25.0f;   //銃反動/リコイルジャンプ共通の反動量
    [SerializeField] private float recoilDuration = 0.1f;      //反動状態の時間

    [Header("地面判定")]
    [SerializeField] private GroundCheck groundCheck;   //地面判定のスクリプト

    [Header("SE")]
    [SerializeField] private AudioClip player_gun_fire;  //発砲時SE

    private bool isRecoiling = false;   　//反動が起きているかどうか
    private float currentCoolTime = 0.0f; //クールタイムの残り時間    
    private Rigidbody2D rigidBody2d;      //反動を加えるためのRigidbody2D
    private float defaultLinearDamping = 0.0f;

    private AudioSource audioSource;      //AudioSource

    void Start()
    {
        //Rigidbody2Dの取得
        rigidBody2d = GetComponentInParent<Rigidbody2D>();
        if (rigidBody2d != null)
        {
            defaultLinearDamping = rigidBody2d.linearDamping;
        }

        //AudioSourceの取得
        audioSource = GetComponentInParent<AudioSource>();
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
    /// 反動が起きているかどうかを返すGet関数
    /// </summary>
    /// <returns>反動が起きているかのbool値</returns>
    public bool GetRecoiling()
    {
        return isRecoiling;
    }

    public void SetAirRecoilPower(float force)
    {
        airRecoilPower = force;
    }
    public float GetAirRecoilPower()
    {
        return airRecoilPower;
    }

    public void SetRecoilDuration(float duration)
    {
        recoilDuration = Mathf.Max(0.01f, duration);
    }

    public float GetRecoilDuration()
    {
        return recoilDuration;
    }


    public void SetCoolTime(float time)
    {
        coolTime = time;
    }

    public float GetCoolTime()
    {
        return coolTime;
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

        //発砲SE再生
        PlaySE(player_gun_fire);
        //Debug.Log("Shoot! Cooldown started.");
    }

    /// <summary>
    /// 銃の反動を適用する関数
    /// </summary>
    /// <param name="direction">反動の方向</param>
    void ApplyRecoil(Vector2 direction)
    {
        if (rigidBody2d == null){ return; }

        if (direction == Vector2.zero){ return; }

        isRecoiling = true;

        //反動中は空気抵抗を増やす
        rigidBody2d.linearDamping = 2.0f;

        //現在の速度を取得
        Vector2 recoil = -direction.normalized * airRecoilPower;
        rigidBody2d.AddForce(recoil, ForceMode2D.Impulse);

        BeginRecoil(recoilDuration);
    }

    /// <summary>
    /// 地面からの跳ね上がりの反動を適用する関数
    /// </summary>
    public void JumpRecoil()
    {
        if (currentCoolTime > 0){ return; }

        if (rigidBody2d == null){ return; }

        // 一度上方向速度をリセットしてからインパルスを与えると、挙動が安定しやすい
        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.y = 0.0f;
        rigidBody2d.linearVelocity = velocity;
        rigidBody2d.AddForce(Vector2.up * airRecoilPower, ForceMode2D.Impulse);
        BeginRecoil(recoilDuration);

        currentCoolTime = coolTime;

        //Debug.Log("Jump Recoil!");
    }

    private void BeginRecoil(float duration)
    {
        isRecoiling = true;
        if (rigidBody2d != null)
        {
            rigidBody2d.linearDamping = 2.0f;
            rigidBody2d.WakeUp();
        }

        CancelInvoke(nameof(EndRecoil));
        Invoke(nameof(EndRecoil), duration);
    }

    /// <summary>
    /// 反動終了の処理を行う関数
    /// </summary>
    void EndRecoil()
    {
        isRecoiling = false;
        if (rigidBody2d != null)
        {
            rigidBody2d.linearDamping = defaultLinearDamping;
        }
    }


    /// <summary>
    /// SE再生用関数
    /// </summary>
    private void PlaySE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
