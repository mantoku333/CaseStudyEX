using Player;
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

    [Header("プレイヤーステータス")]
    [SerializeField] private PlayerStatsData playerStatsData;

    private Animator animator;
    private bool wasGround;  //前フレームの接地状態を保存する変数
    private bool wasGliding; //前フレームの滑空状態を保存する変数

    //--------------移動関連------------------
    private float moveInput;
    private bool jumpInput;
    private bool isGround;

    //-------各種コンポーネント参照関連--------
    private GroundCheck groundCheck;                           //地面判定のスクリプト
    private GunController gunController;                       //銃関連のスクリプト
    private UmbrellaController umbrellaController;             //傘関連のスクリプト
    private UmbrellaAttackController umbrellaAttackController; //傘攻撃関連のスクリプト
    private UmbrellaParryController  umbrellaParryController;  //パリィ関連のスクリプト
    private ParryHitbox parryHitbox;
    private DodgeController dodgeController;                   //回避関連のスクリプト
    private void Awake()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();

        if (rigidBody2d == null)
        {
            Debug.LogError("Rigidbody2Dが見つかっていません！");
        }

        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("Animatorが見つかっていません！");
        }
    }

    private void Start()
    {
        FindComponents();
        ApplyStats();

        wasGround = isGround;
        wasGliding = false;
    }

    private void Update()
    {
        if (groundCheck != null)
        {
            isGround = groundCheck.IsGround();
        }
        else
        {
            isGround = false;
        }

        GetInput();
        Flip();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        Move();
        Jump();
    }

    public void SetPlayerStatsData(PlayerStatsData playerData)
    {
        playerStatsData = playerData;
        ApplyStats();
    }

    //--------Get関数-------
    public PlayerStatsData GetPlayerStatsData()
    {
        return playerStatsData;
    }

    private void FindComponents()
    {
        groundCheck = GetComponentInChildren<GroundCheck>();
        if (groundCheck == null)
        {
            Debug.LogError("GroundCheckが見つかっていません！");
        }

        gunController = GetComponentInChildren<GunController>();
        if (gunController == null)
        {
            Debug.LogError("GunControllerが見つかっていません！");
        }

        umbrellaController = GetComponentInChildren<UmbrellaController>();
        if (umbrellaController == null)
        {
            Debug.LogError("UmbrellaControllerが見つかっていません！");
        }

        umbrellaAttackController = GetComponentInChildren<UmbrellaAttackController>();
        if (umbrellaAttackController == null)
        {
            Debug.LogError("UmbrellaAttackControllerが見つかっていません！");
        }

        umbrellaParryController = GetComponentInChildren<UmbrellaParryController>();
        if (umbrellaParryController == null)
        {
            Debug.LogError("UmbrellaParryControllerが見つかっていません！");
        }

        parryHitbox = GetComponentInChildren<ParryHitbox>();
        if (parryHitbox == null)
        {
            Debug.LogError("ParryHitboxが見つかっていません！");
        }

        dodgeController = GetComponent<DodgeController>();
        if (dodgeController == null)
        {
            Debug.LogError("DodgeControllerが見つかっていません！");
        }
    }

    private void ApplyStats()
    {
        if (playerStatsData == null)
        {
            Debug.LogWarning("PlayerStatsDataが設定されていません。Inspectorから設定してください。");
            return;
        }

        if (umbrellaController != null)
        {
            umbrellaController.SetGlideMoveSpeed(playerStatsData.GlideMoveSpeed);
            umbrellaController.SetFallSpeed(playerStatsData.FallSpeed);
        }

        if (gunController != null)
        {
            gunController.SetAirRecoilPower(playerStatsData.GunRecoilForce);
            gunController.SetCoolTime(playerStatsData.ReloadSeconds);
        }

        if (umbrellaAttackController != null)
        {
            umbrellaAttackController.SetAttackPerSecond(playerStatsData.AttackPerSecond);
        }
    }

    private void GetInput()
    {
        if (umbrellaController == null){ return; }

        bool isGliding = (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open);
        moveInput = 0.0f;

        // 左右移動の入力
        if (Keyboard.current.aKey.isPressed)
        {
            moveInput = -1.0f;
        }
        else if (Keyboard.current.dKey.isPressed)
        {
            moveInput = 1.0f;
        }

        if(dodgeController != null && dodgeController.IsDodging())
        {
            jumpInput = false;
            return;
        }

        //回避(左シフトキー+移動キーで左右に回避)
        if (Keyboard.current.leftShiftKey.wasPressedThisFrame ||
           (Keyboard.current.leftShiftKey.isPressed &&
           (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)))
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

        //ジャンプ(スペースキーでジャンプ)
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpInput = true;
        }

        //パリィor傘開閉 (右クリックでパリィ、敵の攻撃がない場合は傘の開け閉め)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (parryHitbox != null && parryHitbox.HasEnemyAttack())
            {
                if (umbrellaController != null)
                {
                    umbrellaController.SetUmbrellaState(UmbrellaController.UmbrellaState.Open);
                }

                if (umbrellaParryController != null)
                {
                    umbrellaParryController.Parry();
                }

                parryHitbox.ClearEnemyAttacks();
            }
            else
            {
                if (umbrellaController != null)
                {
                    umbrellaController.ToggleUmbrella();
                }
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!isGround)
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


        //銃での飛び上がり(空中でEキーを押すと銃の反動で飛び上がる)
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (isGround)
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
        if(dodgeController != null && dodgeController.IsDodging()){ return; }

        if (rigidBody2d == null) { return; }

        if(playerStatsData == null) { return; }

        if(umbrellaController == null) { return; }

        Vector2 velocity = rigidBody2d.linearVelocity;

        bool isGliding = (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open);

        if (moveInput != 0.0f)
        {
            float moveSpeed = playerStatsData.MoveSpeed;

            if (isGliding)
            {
                moveSpeed = umbrellaController.GetGlideMoveSpeed();
            }

            velocity.x = moveInput * moveSpeed;
        }
        else
        {
            if (isGliding)
            {
                velocity.x *= 0.95f;
            }
            else
            {
                velocity.x = 0.0f;
            }
        }

        rigidBody2d.linearVelocity = velocity;
    }

    /// <summary>
    /// プレイヤーのジャンプ処理の関数
    /// </summary>
    private void Jump()
    {
        if (dodgeController != null && dodgeController.IsDodging())
        {
            jumpInput = false;
            return;
        }

        if (!jumpInput)
        {
            return;
        }

        if (rigidBody2d == null)
        {
            jumpInput = false;
            return;
        }

        if (playerStatsData == null)
        {
            jumpInput = false;
            return;
        }

        if (isGround)
        {
            rigidBody2d.linearVelocity = new Vector2(rigidBody2d.linearVelocity.x, playerStatsData.JumpForce);
        }

        jumpInput = false;
    }

    /// <summary>
    /// プレイヤーの向き処理の関数
    /// </summary>
    private void Flip()
    {
        if (umbrellaController == null)
        {
            return;
        }

        bool isGliding = (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open);

        if (isGround)
        {
            if (moveInput > 0.0f)
            {
                transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            }
            else if (moveInput < 0.0f)
            {
                transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
            }

            return;
        }

        if (isGliding)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0.0f));
            mouseWorldPos.z = 0.0f;

            if (mouseWorldPos.x > transform.position.x)
            {
                transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            }
            else
            {
                transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
            }

            return;
        }

        if (moveInput > 0.0f)
        {
            transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        }
        else if (moveInput < 0.0f)
        {
            transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
        }
    }

    //--------------アニメーション関連------------------
    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (umbrellaController == null)
        {
            return;
        }

        bool isUmbrellaOpen = (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open);
        bool isGliding = isUmbrellaOpen && !isGround;
        bool isMoving = false;
        bool isDodging = false;

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            isMoving = true;
        }

        if (dodgeController != null)
        {
            isDodging = dodgeController.IsDodging();
        }

        animator.SetBool("IsGround", isGround);
        animator.SetBool("IsMove", isMoving);
        animator.SetBool("IsGlide", isGliding);
        animator.SetBool("IsDodge", isDodging);

        bool hasLandedFromGlide = false;

        if (!wasGround && isGround && wasGliding)
        {
            hasLandedFromGlide = true;
        }

        if (hasLandedFromGlide)
        {
            animator.ResetTrigger("LandTrigger");
            animator.SetTrigger("LandTrigger");
        }

        wasGround = isGround;
        wasGliding = isGliding;
    }

        public void OnLandAnimationEnd()
    {
        if (umbrellaController == null)
        {
            return;
        }

        if (!isGround)
        {
            return;
        }

        if (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open)
        {
            umbrellaController.SetUmbrellaState(UmbrellaController.UmbrellaState.Closed);
        }
    }
}

