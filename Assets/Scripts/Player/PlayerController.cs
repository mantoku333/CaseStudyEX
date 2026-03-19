using UnityEngine.InputSystem;
using UnityEngine;

/// <summary>
/// 
/// </summary>
public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rigidBody2d;

    [Header("移動設定")]
    [SerializeField] private float groundMoveSpeed = 5.0f;

    [Header("ジャンプ設定")]
    [SerializeField] private float jumpForce = 8.0f;

    [Header("地面判定")]
    [SerializeField] private GroundCheck groundCheck;

    private GunController gunController;    //銃関連のスクリプト
    private UmbrellaController umbrellaController;  //傘関連のスクリプ

    private float moveInput;
    private bool jumpInput;
    private bool isGround;

    private void Awake()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        gunController = GetComponentInChildren<GunController>();
        umbrellaController = GetComponentInChildren<UmbrellaController>();
    }

    private void Update()
    {
        GetInput();
        Flip();

        isGround = groundCheck.IsGround();
    }

    private void FixedUpdate()
    {
        Move();
        Jump();
    }

    private void GetInput()
    {
        //基本移動
        moveInput = 0;
        if (Keyboard.current.aKey.isPressed)
        {
            moveInput = -1;
        }
        else if (Keyboard.current.dKey.isPressed)
        {
            moveInput = 1;
        }

        //ジャンプ
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpInput = true;
        }
    }

    /// <summary>
    /// プレイヤーの左右移動処理の関数
    /// </summary>
    private void Move()
    {
        Vector2 velocity = rigidBody2d.linearVelocity;

        if (moveInput != 0)
        {
            velocity.x = moveInput * groundMoveSpeed;
        }
        else
        {
            velocity.x = 0;
        }

        rigidBody2d.linearVelocity = velocity;
    }

    /// <summary>
    /// プレイヤーのジャンプ処理の関数
    /// </summary>
    private void Jump()
    {
        if (jumpInput)
        {
            if (isGround)
            {
                rigidBody2d.linearVelocity = new Vector2(rigidBody2d.linearVelocity.x, jumpForce);
            }

            jumpInput = false;
        }
    }

    /// <summary>
    /// プレイヤーの向き処理の関数
    /// </summary>
    private void Flip()
    {
         if (moveInput > 0)
         {
             transform.localScale = new Vector3(1, 1, 1);
         }
         else if (moveInput < 0)
         {
             transform.localScale = new Vector3(-1, 1, 1);
         }

         return;
    }

}
