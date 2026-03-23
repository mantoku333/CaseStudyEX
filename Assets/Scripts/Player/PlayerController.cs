using UnityEngine.InputSystem;
using UnityEngine;

/// <summary>
/// プレイヤーの基本操作を管理するクラス
/// 左右移動、ジャンプ、向き制御、銃反動、傘の開け閉め、敵への攻撃
/// 入力を受け取り、それぞれの機能へ処理を振り分ける
/// </summary>
public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rigidBody2d;

    [Header("移動設定")]
    [SerializeField] private float groundMoveSpeed = 5.0f;

    [Header("ジャンプ設定")]
    [SerializeField] private float jumpForce = 8.0f;

    private GroundCheck groundCheck;                           //地面判定のスクリプト
    private GunController gunController;                       //銃関連のスクリプト
    private UmbrellaController umbrellaController;             //傘関連のスクリプト
    private UmbrellaAttackController umbrellaAttackController; //傘攻撃関連のスクリプト
    private UmbrellaParryController  umbrellaParryController;  //パリィ関連のスクリプト
    private ParryHitbox parryHitbox;
    private DodgeController dodgeController;                   //回避関連のスクリプト

    private float moveInput;   //移動入力の値(-1:左,1:右)
    private bool  jumpInput;   //ジャンプ入力のフラグ
    private bool   isGround;　 //地面にいるかどうかのフラグ

    private void Awake()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        //各スクリプトの取得とエラーチェック
        //地面判定のスクリプト
        groundCheck = GetComponentInChildren<GroundCheck>();
        if (groundCheck == null)
        {
            Debug.LogError("groundCheckが見つかっていません！");
        }

        //銃のスクリプト
        gunController = GetComponentInChildren<GunController>();
        if (gunController == null)
        {
            Debug.LogError("gunControllerが見つかっていません！");
        }

        //傘のスクリプト
        umbrellaController = GetComponentInChildren<UmbrellaController>();
        if (umbrellaController == null)
        {
            Debug.LogError("umbrellaControllerが見つかっていません！");
        }

        //傘攻撃のスクリプト
        umbrellaAttackController = GetComponentInChildren<UmbrellaAttackController>();
        if (umbrellaAttackController == null)
        {
            Debug.LogError("UmbrellaAttackControllerが見つかっていません！");
        }

        //パリィのスクリプト
        umbrellaParryController = GetComponentInChildren<UmbrellaParryController>();
        if (umbrellaParryController == null)
        {
            Debug.LogError("umbrellaParryControllerが見つかっていません！");
        }

        //パリィの当たり判定のスクリプト
        parryHitbox = GetComponentInChildren<ParryHitbox>();
        if (parryHitbox == null)
        {
            Debug.LogError("parryHitboxが見つかっていません！");
        }

        //回避のスクリプト
        dodgeController = GetComponent<DodgeController>();
        if (dodgeController == null)
        {
                Debug.LogError("dodgeControllerが見つかっていません！");
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

        //回避
        if (
            Keyboard.current.leftShiftKey.wasPressedThisFrame ||
            (Keyboard.current.leftShiftKey.isPressed &&
            (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
        )
        {
            Vector2 dodgeDirection = Vector2.zero;

            if (Keyboard.current.aKey.isPressed)
            {
                dodgeDirection = Vector2.left;
            }
            else if (Keyboard.current.dKey.isPressed)
            {
                dodgeDirection = Vector2.right;
            }

            if (dodgeDirection != Vector2.zero)
            {
                if (dodgeController != null)
                {
                    dodgeController.Dodge(dodgeDirection);
                }
            }
        }

        //ジャンプ
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpInput = true;
        }

        //パリィor傘開閉
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            
            if (parryHitbox != null && parryHitbox.HasEnemyAttack())
            {
                //Debug.Log("パリィだよ");
                umbrellaParryController.Parry();

                parryHitbox.ClearEnemyAttacks();
            }
            else
            {
                //Debug.Log("パリィじゃないよ");
                umbrellaController.ToggleUmbrella();
            }
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
                if (!isGliding)
                {
                    umbrellaAttackController.Attack();
                }
            }
        }

        //銃
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (groundCheck.IsGround())
            {
                gunController.JumpRecoil();
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

        //地面→進んでいる方向に合わせて設定
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

        //滑空中→マウスの位置に合わせて設定
        if (isGliding)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
                new Vector3(mousePos.x, mousePos.y, 0f));
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

        //空中→進んでいる方向に合わせて設定
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
