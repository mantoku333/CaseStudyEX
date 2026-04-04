using Player;
using NaughtyAttributes;
using UnityEngine.InputSystem;
using UnityEngine;

/// <summary>
/// プレイヤーの基本操作を管理するクラス
/// 左右移動、ジャンプ、向き制御、銃反動、傘の開け閉め、敵への攻撃
/// 入力を受け取り、それぞれの機能へ処理を振り分ける
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour, IPlayerViewStateProvider
{
    private const string PlayerActionMapName = "Player";

    private static class InputActionNames
    {
        public const string Move = "Move";
        public const string Aim = "Aim";
        public const string Attack = "Attack";
        public const string Jump = "Jump";
        public const string Dodge = "Dodge";
        public const string UmbrellaToggle = "UmbrellaToggle";
        public const string RecoilJump = "RecoilJump";
    }

    private Rigidbody2D rigidBody2d;
    private PlayerInput playerInput;

    [Header("プレイヤーステータス")]
    [SerializeField, Required, Expandable] private PlayerStatsData playerStatsData;

    //--------------移動関連------------------
    private float moveInput;
    private bool jumpInput;
    private bool isGround;
    private bool hasPreviousGroundState;
    private bool previousGroundState;
    private bool isFacingRight = true;

    //-------各種コンポーネント参照関連--------
    private GroundCheck groundCheck;                           //地面判定のスクリプト
    private GunController gunController;                       //銃関連のスクリプト
    private UmbrellaController umbrellaController;             //傘関連のスクリプト
    private UmbrellaAttackController umbrellaAttackController; //傘攻撃関連のスクリプト
    private UmbrellaParryController  umbrellaParryController;  //パリィ関連のスクリプト
    private ParryHitbox parryHitbox;
    private DodgeController dodgeController;                   //回避関連のスクリプト

    //-------入力関連--------
    private InputAction moveAction;
    private InputAction aimAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction dodgeAction;
    private InputAction recoilJumpAction;
    private InputAction umbrellaToggleAction;
    private bool inputActionsReady;

    //-------View向け状態公開--------
    public bool IsGrounded => isGround;
    public bool IsMoving => Mathf.Abs(moveInput) > 0.01f;
    public bool IsGliding =>
        umbrellaController != null &&
        umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open &&
        !isGround;
    public bool IsFacingRight => isFacingRight;
    public bool IsDodging => dodgeController != null && dodgeController.IsDodging();

    private void Awake()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        if (rigidBody2d == null)
        {
            Debug.LogError("Rigidbody2Dが見つかっていません！");
        }

    }

    private void OnEnable()
    {
        BindInputActions();
        hasPreviousGroundState = false;
    }

    private void OnDisable()
    {
        inputActionsReady = false;
    }

    private void Start()
    {
        FindComponents();
        ApplyStats();
    }

    private void Update()
    {
        GetInput();
        UpdateFacingDirection();
    }

    private void FixedUpdate()
    {
        RefreshGroundState();
        HandleGroundTransition();
        Move();
        Jump();
    }

    private void RefreshGroundState()
    {
        if (groundCheck != null)
        {
            isGround = groundCheck.IsGround();
        }
        else
        {
            isGround = false;
        }
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
            gunController.SetRecoilDuration(playerStatsData.GunRecoilDuration);
            gunController.SetCoolTime(playerStatsData.ReloadSeconds);
        }

        if (umbrellaAttackController != null)
        {
            umbrellaAttackController.SetAttackPerSecond(playerStatsData.AttackPerSecond);
            umbrellaAttackController.SetAttackDuration(playerStatsData.UmbrellaAttackDuration);
        }

        if (umbrellaParryController != null)
        {
            umbrellaParryController.SetParryDuration(playerStatsData.ParryDuration);
            umbrellaParryController.SetFlashDuration(playerStatsData.ParryFlashDuration);
        }

        if (dodgeController != null)
        {
            dodgeController.SetDodgeDistance(playerStatsData.DodgeDistance);
            dodgeController.SetDodgeDuration(playerStatsData.DodgeDuration);
        }
    }

    private void GetInput()
    {
        if (umbrellaController == null){ return; }
        if (!inputActionsReady){ return; }

        bool isGliding = (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open);
        moveInput = 0.0f;

        // 左右移動の入力
        if (moveAction != null)
        {
            Vector2 move = moveAction.ReadValue<Vector2>();
            moveInput = Mathf.Clamp(move.x, -1.0f, 1.0f);
        }

        if(dodgeController != null && dodgeController.IsDodging())
        {
            jumpInput = false;
            return;
        }

        //回避(左シフトキー+移動キーで左右に回避)
        bool isDodgeTriggered = IsPressedThisFrame(dodgeAction) ||
            (IsPressed(dodgeAction) && IsPressedThisFrame(moveAction));
        if (isDodgeTriggered)
        {
            Vector2 dodgeDirection = Vector2.zero;

            if (moveInput < -0.01f)
            {
                dodgeDirection = Vector2.left;
            }
            else if (moveInput > 0.01f)
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
        if (IsPressedThisFrame(jumpAction))
        {
            jumpInput = true;
        }

        //パリィor傘開閉 (右クリックでパリィ、敵の攻撃がない場合は傘の開け閉め)
        if (IsPressedThisFrame(umbrellaToggleAction))
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

        if (IsPressedThisFrame(attackAction))
        {
            if (!isGround)
            {
                if (TryGetAimScreenPosition(out var pointerPos))
                {
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null && gunController != null)
                    {
                        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(
                            new Vector3(pointerPos.x, pointerPos.y, 0f)
                        );
                        mouseWorldPos.z = 0f;

                        Vector2 shootDirection = (mouseWorldPos - transform.position).normalized;
                        gunController.Shoot(shootDirection);
                    }
                }
            }
            else
            {
                if (!isGliding && umbrellaAttackController != null)
                {
                    umbrellaAttackController.Attack();
                }
            }
        }


        //銃での飛び上がり(空中でEキーを押すと銃の反動で飛び上がる)
        if (IsPressedThisFrame(recoilJumpAction))
        {
            if (isGround)
            {
                if (gunController != null)
                {
                    gunController.JumpRecoil();
                }
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

        bool isGliding = umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open && !isGround;

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
    /// View層が参照する向き状態を更新する関数
    /// </summary>
    private void UpdateFacingDirection()
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
                isFacingRight = true;
            }
            else if (moveInput < 0.0f)
            {
                isFacingRight = false;
            }

            return;
        }

        if (isGliding)
        {
            if (!TryGetAimScreenPosition(out var pointerPos))
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, 0.0f));
            mouseWorldPos.z = 0.0f;

            if (mouseWorldPos.x > transform.position.x)
            {
                isFacingRight = true;
            }
            else
            {
                isFacingRight = false;
            }

            return;
        }

        if (moveInput > 0.0f)
        {
            isFacingRight = true;
        }
        else if (moveInput < 0.0f)
        {
            isFacingRight = false;
        }
    }

    private void HandleGroundTransition()
    {
        if (!hasPreviousGroundState)
        {
            previousGroundState = isGround;
            hasPreviousGroundState = true;
            return;
        }

        if (!previousGroundState && isGround)
        {
            // CloseUmbrellaOnLanding();
        }

        previousGroundState = isGround;
    }

    private void CloseUmbrellaOnLanding()
    {
        if (umbrellaController == null)
        {
            return;
        }

        if (umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open)
        {
            umbrellaController.SetUmbrellaState(UmbrellaController.UmbrellaState.Closed);
        }
    }

    // Legacy callback kept for old animation event wiring.
    public void OnLandAnimationEnd()
    {
        // No-op: landing side effects are handled by logic-side ground transition.
    }

    private void BindInputActions()
    {
        inputActionsReady = false;

        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError("PlayerInput または InputActions が未設定です。", this);
            return;
        }

        InputActionMap playerActionMap = playerInput.actions.FindActionMap(PlayerActionMapName, false);
        if (playerActionMap == null)
        {
            Debug.LogError($"InputActionMap '{PlayerActionMapName}' が見つかりません。", this);
            return;
        }

        bool allBound = true;
        allBound &= TryBindRequiredAction(playerActionMap, InputActionNames.Move, ref moveAction);
        allBound &= TryBindRequiredAction(playerActionMap, InputActionNames.Aim, ref aimAction);
        allBound &= TryBindRequiredAction(playerActionMap, InputActionNames.Jump, ref jumpAction);
        allBound &= TryBindRequiredAction(playerActionMap, InputActionNames.Attack, ref attackAction);
        allBound &= TryBindRequiredAction(playerActionMap, InputActionNames.Dodge, ref dodgeAction);
        allBound &= TryBindRequiredAction(playerActionMap, InputActionNames.UmbrellaToggle, ref umbrellaToggleAction);
        allBound &= TryBindRequiredAction(playerActionMap, InputActionNames.RecoilJump, ref recoilJumpAction);

        inputActionsReady = allBound;
    }

    private static bool IsPressedThisFrame(InputAction action)
    {
        return action != null && action.WasPressedThisFrame();
    }

    private static bool IsPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    private bool TryGetAimScreenPosition(out Vector2 position)
    {
        if (aimAction != null)
        {
            position = aimAction.ReadValue<Vector2>();
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    private bool TryBindRequiredAction(InputActionMap actionMap, string actionName, ref InputAction targetAction)
    {
        targetAction = actionMap.FindAction(actionName, false);
        if (targetAction == null)
        {
            Debug.LogError($"Action '{actionName}' が見つかりません。", this);
            return false;
        }

        if (!targetAction.enabled)
        {
            targetAction.Enable();
        }

        return true;
    }
}
