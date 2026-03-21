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
    [SerializeField]  private GunController gunController;    //銃関連のスクリプト
    private UmbrellaController umbrellaController;  //傘関連のスクリプ
    private UmbrellaAttackController umbrellaAttackController;　　//傘攻撃関連のスクリプト

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
        umbrellaAttackController = GetComponentInChildren<UmbrellaAttackController>();
        if (umbrellaAttackController == null)
        {
            Debug.LogError("UmbrellaAttackControllerが見つかっていません！");
        }
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
        bool isGliding = (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open);

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


        //傘開閉
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            umbrellaController.ToggleUmbrella();
        }

        //傘攻撃又は銃の反動
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!groundCheck.IsGround())
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();

                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
                    new Vector3(mousePos.x, mousePos.y, 0f)
                );
                mouseWorldPos.z = 0f;

                Vector2 shootDirection = (mouseWorldPos - transform.position).normalized;
                gunController.Shoot(shootDirection);
            }
            else
            {
                Debug.Log("地面にいるため銃を撃てません。");
                if (!isGliding)
                {
                    umbrellaAttackController.Attack();
                }
            }
        }

        //銃（下撃ちジャンプ）
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (groundCheck.IsGround())
            {
                gunController.Shoot(Vector2.down);
            }
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
            if (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open)
            {
                velocity.x *= 0.95f; // 空中は強く減速
            }
            else
            {
                velocity.x = 0;
            }
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
        bool isGround = groundCheck.IsGround();
        bool isGliding = (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open);

        // 地面
        if (isGround)
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

        // 滑空中
        if (isGliding)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
                new Vector3(mousePos.x, mousePos.y, 0f)
            );
            mouseWorldPos.z = 0f;

            if (mouseWorldPos.x > transform.position.x)
            {
                transform.localScale = new Vector3(1, 1, 1);
            }
            else
            {
                transform.localScale = new Vector3(-1, 1, 1);
            }

            return;
        }

        // 空中（滑空なし）
        if (moveInput > 0)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveInput < 0)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
    }
}
