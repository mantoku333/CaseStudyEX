using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UmbrellaController : MonoBehaviour
{
    
    private Rigidbody2D rigidBody2D; //PlayerのRigidbody2D

    /// <summary>
    /// 傘状態関連
    /// </summary>
    public enum UmbrellaState
    {
        Open,
        Closed
    }

    [Header("滑空関係")]
    [SerializeField] private float glideFallSpeed = -0.3f;   //滑空中の落下速度
    [SerializeField] private float glideMoveSpeed = 3.5f;
    [SerializeField] private GunController gunController;  //銃関連のスクリプト

    private UmbrellaState umbrellaState = UmbrellaState.Closed;  //現在の傘の状態
    private SpriteRenderer spriteRenderer;  //デバッグ用のスプライトレンダラー(傘が出来たら削除)

    [Header("SE")]
    [SerializeField] private AudioClip umbrella_open;       //傘開くSE
    [SerializeField] private AudioClip umbrella_close;      //傘閉じるSE

    private AudioSource audioSource;        //AudioSource

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponentInParent<Rigidbody2D>();
        gunController = GetComponentInParent<GunController>();
        audioSource = GetComponentInParent<AudioSource>();
        UpdateDebugColor();
    }

    private void Update()
    {
        Glide();
    }

    /// <summary>
    /// 傘の状態のSet関数
    /// </summary>
    /// <param name="state">セットする傘の状態</param>
    public void SetUmbrellaState(UmbrellaState state)
    {
        umbrellaState = state;
        UpdateDebugColor();
    }

    /// <summary>
    /// 傘の状態のGet関数
    /// </summary>
    /// <returns>現在の傘の状態</returns>
    public UmbrellaState GetUmbrellaState()
    {
        return umbrellaState;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="glideMoveSpeed"></param>
    public void SetGlideMoveSpeed(float speed)
    {
        glideMoveSpeed = speed;
    }

    public float GetGlideMoveSpeed()
    {
        return glideMoveSpeed;
    }

    public void SetFallSpeed(float speed)
    {
        glideFallSpeed = speed;
    }
    public float GetFallSpeed()
    {
        return glideFallSpeed;
    }


    /// <summary>
    /// 傘の開閉を切り替える関数
    /// </summary>
    public void ToggleUmbrella()
    {
        if (umbrellaState == UmbrellaState.Closed)
        {
            umbrellaState = UmbrellaState.Open;

            //傘開けるSE再生
            PlaySE(umbrella_open);
        }
        else
        {
            umbrellaState = UmbrellaState.Closed;

            //傘閉じるSE再生
            PlaySE(umbrella_close);
        }

        UpdateDebugColor();
    }

    /// <summary>
    /// 傘での滑空を行う関数
    /// </summary>
    private void Glide()
    {
        if (umbrellaState != UmbrellaState.Open) { return; }

        if (gunController != null && gunController.GetRecoiling()) { return; }

        if (rigidBody2D.linearVelocity.y >= 0) { return; }

        if (rigidBody2D == null) { return; }

        float maxFallVelocity = -Mathf.Abs(glideFallSpeed);

        // 落下が速すぎるときだけ補正
        if (rigidBody2D.linearVelocity.y < maxFallVelocity)
        {
            Vector2 velocity = rigidBody2D.linearVelocity;
            velocity.y = maxFallVelocity;
            rigidBody2D.linearVelocity = velocity;
        }
    }

    /// <summary>
    /// 傘の開閉に応じてスプライトの色を変える関数(デバッグ用)
    /// </summary>
    private void UpdateDebugColor()
    {
        if (umbrellaState == UmbrellaState.Open)
        {
            spriteRenderer.color = Color.blue;
        }
        else
        {
            spriteRenderer.color = Color.red;
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
