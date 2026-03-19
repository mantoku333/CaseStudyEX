using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UmbrellaController : MonoBehaviour
{
    private Rigidbody2D rigidBody2D;    //プレイヤーのRigidbody2Dを取得して操作するための変数

    /// <summary>
    /// 傘状態関連
    /// </summary>
    public enum UmbrellaState
    {
        Open,
        Closed
    }

    [Header("滑空関係")]
    [SerializeField]
    private float glideFallSpeed = -0.3f;

    private UmbrellaState umbrellaState = UmbrellaState.Closed;    //現在の傘の状態を管理する変数

    private SpriteRenderer spriteRenderer;  //傘のデバッグ用のスプライトを管理するための変数

    private GunController gunController;    //銃関連のスクリプト

    private void Awake()
    {
        gunController = GetComponentInParent<Transform>().GetComponentInChildren<GunController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponentInParent<Rigidbody2D>();
        UpdateDebugColor();
    }

    private void Update()
    {
        //Glide();
    }

    public void SetUmbrellaState(UmbrellaState state)
    {
        umbrellaState = state;
        UpdateDebugColor();
    }

    public UmbrellaState GetUmbrellaState()
    {
         return umbrellaState;
    }

    public void ToggleUmbrella()
    {
        if (umbrellaState == UmbrellaState.Open)
        {
            SetUmbrellaState(UmbrellaState.Closed);
        }
        else
        {
            SetUmbrellaState(UmbrellaState.Open);
        }

        UpdateDebugColor();
    }

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
}
