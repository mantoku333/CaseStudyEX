using Player;
using NaughtyAttributes;
using UnityEngine.InputSystem;
using UnityEngine;

/// <summary>
/// プレイヤー操作のオーケストレーター。
/// 入力取得、状態更新、各サブコンポーネントへの命令発行を担う。
/// 
/// 設計意図:
/// - 物理値の最終決定はこのクラスで行い、機能別処理は各Controllerへ委譲する
/// - Viewは IPlayerViewStateProvider 経由で状態参照し、操作ロジックと分離する
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour, IPlayerViewStateProvider
{
    private const string PlayerActionMapName = "Player";
    private const float ExternalMoveArrivalThreshold = 0.03f;

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
    private Collider2D playerCollider;
    private PlayerInput playerInput;
    private PhysicsMaterial2D runtimeNoFrictionMaterial;

    [Header("物理設定")]
    [SerializeField] private bool applyNoFrictionMaterial = true;

    [Header("プレイヤーステータス")]
    [SerializeField, Required, Expandable] private PlayerStatsData playerStatsData;

    //--------------移動関連------------------
    private float moveInput;
    private bool jumpInput;
    private bool isGround;
    private bool hasPreviousGroundState;
    private bool previousGroundState;
    private bool isFacingRight = true;
    private bool externalControlLocked;
    private bool externalFacingLocked;
    private bool externalFacingRight = true;
    private bool externalMovementActive;
    private float externalMoveInput;
    private bool externalMovementHasTarget;
    private float externalMovementTargetX;

    //-------各種コンポーネント参照関連--------
    private GroundCheck groundCheck;                           //地面判定のスクリプト
    private GunController gunController;                       //銃関連のスクリプト
    private UmbrellaController umbrellaController;             //傘関連のスクリプト
    private UmbrellaAttackController umbrellaAttackController; //傘攻撃関連のスクリプト
    private UmbrellaParryController  umbrellaParryController;  //パリィ関連のスクリプト
    private ParryHitbox parryHitbox;
    private AttackHitbox[] attackHitboxes;
    private DodgeController dodgeController;                   //回避関連のスクリプト
    private MonoBehaviour fallThroughController;               //床すり抜け関連のスクリプト
    private PlayerAbilityController playerAbilityController;   //能力管理のスクリプト

    //-------入力関連--------
    private InputAction moveAction;
    private InputAction aimAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction dodgeAction;
    private InputAction recoilJumpAction;
    private InputAction umbrellaToggleAction;
    private bool inputActionsReady;
    private bool wasDownHeld;

    //-------View向け状態公開--------
    // Animator/View が参照する読み取り専用状態。
    // ロジック追加時は「計算済みの状態」をここに公開し、View側で判定させない方針。
    public bool IsGrounded => isGround;
    public bool IsMoving => Mathf.Abs(moveInput) > 0.01f || externalMovementActive;
    public bool IsGliding =>
        umbrellaController != null &&
        umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open &&
        !isGround;
    public bool IsUmbrellaOpen =>
        umbrellaController != null &&
        umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open;
    public bool IsFacingRight => isFacingRight;
    public bool IsDodging => dodgeController != null && dodgeController.IsDodging();
    public bool IsParrying => umbrellaParryController != null && umbrellaParryController.IsParrying();
    public bool IsUmbrellaChanging => umbrellaController != null && umbrellaController.IsChanging();
    public bool IsAttacking =>
        umbrellaAttackController != null &&
        umbrellaAttackController.IsAttacking() &&
        umbrellaController != null &&
        umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Closed;
    public bool IsRecoilBoosting => gunController != null && gunController.GetRecoiling() && !isGround;
    public bool IsExternalControlLocked => externalControlLocked;
    public bool IsExternalFacingLocked => externalFacingLocked;
    public bool IsExternalMovementActive => externalMovementActive;

    private void Awake()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        playerInput = GetComponent<PlayerInput>();

        if (rigidBody2d == null)
        {
            Debug.LogError("Rigidbody2Dが見つかっていません");
        }
        else
        {
            // 高速な回避やリコイル時に接触判定を落としにくくする。
            rigidBody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (applyNoFrictionMaterial && playerCollider != null)
        {
            ApplyNoFrictionMaterial();
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
        externalControlLocked = false;
        externalFacingLocked = false;
        externalFacingRight = true;
        externalMovementActive = false;
        externalMoveInput = 0.0f;
        externalMovementHasTarget = false;
        externalMovementTargetX = 0.0f;
    }

    private void Start()
    {
        FindComponents();
        ApplyStats();
    }

    // フレーム入力の取得と見た目向きの更新のみを担当。
    // 物理値の更新は FixedUpdate 側で行う。
    private void Update()
    {
        GetInput();
        UpdateFacingDirection();
        RefreshParryColliderFacing();
    }

    // 物理更新順は依存関係を持つため固定:
    // 1) 接地状態更新 -> 2) 接地遷移処理 -> 3) 移動 -> 4) ジャンプ
    // この順序を変えると入力遅延やジャンプ取りこぼしの原因になる。
    private void FixedUpdate()
    {
        RefreshGroundState();
        HandleGroundTransition();

        if (externalMovementActive)
        {
            UpdateExternalTargetMovement();
            jumpInput = false;
            Move();
            return;
        }

        if (externalControlLocked)
        {
            moveInput = 0.0f;
            jumpInput = false;
            return;
        }

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

    public void SetExternalControlLocked(bool locked)
    {
        externalControlLocked = locked;

        if (externalControlLocked)
        {
            moveInput = 0.0f;
            jumpInput = false;
        }
    }

    public void SetExternalFacingLocked(bool locked, bool faceRight)
    {
        externalFacingLocked = locked;
        externalFacingRight = faceRight;

        if (externalFacingLocked)
        {
            isFacingRight = externalFacingRight;
        }
    }

    public void SetExternalMovementDirection(float horizontalDirection)
    {
        externalMoveInput = Mathf.Clamp(horizontalDirection, -1.0f, 1.0f);
        externalMovementActive = Mathf.Abs(externalMoveInput) > 0.01f;

        if (externalMovementActive && !externalFacingLocked)
        {
            isFacingRight = externalMoveInput > 0.0f;
        }
    }

    public void StartExternalMoveToX(float targetX)
    {
        externalMovementTargetX = targetX;
        externalMovementHasTarget = true;
        SetExternalMovementDirection(targetX - transform.position.x);
    }

    public void ClearExternalMovementDirection()
    {
        externalMovementActive = false;
        externalMoveInput = 0.0f;
        externalMovementHasTarget = false;
    }

    private void FindComponents()
    {
        groundCheck = GetComponentInChildren<GroundCheck>();
        if (groundCheck == null)
        {
            Debug.LogError("GroundCheckが見つかっていません");
        }

        gunController = GetComponentInChildren<GunController>();
        if (gunController == null)
        {
            Debug.LogError("GunControllerが見つかっていません");
        }

        umbrellaController = GetComponentInChildren<UmbrellaController>();
        if (umbrellaController == null)
        {
            Debug.LogError("UmbrellaControllerが見つかっていません");
        }

        umbrellaAttackController = GetComponentInChildren<UmbrellaAttackController>();
        if (umbrellaAttackController == null)
        {
            Debug.LogError("UmbrellaAttackControllerが見つかっていません");
        }

        umbrellaParryController = GetComponentInChildren<UmbrellaParryController>();
        if (umbrellaParryController == null)
        {
            Debug.LogError("UmbrellaParryControllerが見つかっていません");
        }

        parryHitbox = GetComponentInChildren<ParryHitbox>();
        if (parryHitbox == null)
        {
            Debug.LogError("ParryHitboxが見つかっていません");
        }

        attackHitboxes = GetComponentsInChildren<AttackHitbox>(true);

        dodgeController = GetComponent<DodgeController>();
        if (dodgeController == null)
        {
            Debug.LogError("DodgeControllerが見つかっていません");
        }

        fallThroughController = GetComponent("FallThroughController") as MonoBehaviour;
        if (fallThroughController == null)
        {
            Debug.LogError("FallThroughControllerが見つかっていません");
        }

        playerAbilityController = GetComponent<PlayerAbilityController>();
        if (playerAbilityController == null)
        {
            Debug.LogError("PlayerAbilityControllerが見つかっていません");
        }
    }

    // ScriptableObject(PlayerStatsData) の値を各機能コンポーネントへ反映する窓口。
    // パラメータ追加時は「Data -> このメソッド -> 各Controller」の順で配線する。
    private void ApplyStats()
    {
        if (playerStatsData == null)
        {
            Debug.LogWarning("PlayerStatsDataが設定されていません。Inspectorから設定してください。");
            return;
        }

        ApplyStatsToAttackHitboxes();

        if (umbrellaController != null)
        {
            umbrellaController.SetGlideMoveSpeed(playerStatsData.GlideMoveSpeed);
            umbrellaController.SetFallSpeed(playerStatsData.FallSpeed);
        }

        if (gunController != null)
        {
            gunController.SetAirRecoilPower(playerStatsData.GunRecoilForce);
            gunController.SetRecoilDuration(playerStatsData.GunRecoilDuration);
            gunController.SetRecoilCoolTimes(
                playerStatsData.FirstRecoilCoolTime,
                playerStatsData.SecondRecoilCoolTime);
        }

        if (umbrellaAttackController != null)
        {
            umbrellaAttackController.SetAttackSecondsPerAttack(playerStatsData.AttackSecondsPerAttack);
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

    private void ApplyStatsToAttackHitboxes()
    {
        if (attackHitboxes == null || attackHitboxes.Length == 0)
        {
            attackHitboxes = GetComponentsInChildren<AttackHitbox>(true);
        }

        for (int i = 0; i < attackHitboxes.Length; i++)
        {
            if (attackHitboxes[i] != null)
            {
                attackHitboxes[i].SetPlayerStatsData(playerStatsData);
            }
        }
    }

    // 入力受付専用。
    // ここでは「何をするか」を決めるだけで、実際の物理移動量の確定は Move/Jump に任せる。
    // 攻撃仕様:
    // - 空中攻撃(銃)は滑空中のみ
    // - 地上攻撃は傘攻撃
    private void GetInput()
    {
        if (externalControlLocked)
        {
            moveInput = 0.0f;
            jumpInput = false;
            wasDownHeld = false;
            return;
        }

        if (umbrellaController == null)
        {
            wasDownHeld = false;
            return;
        }

        if (!inputActionsReady)
        {
            wasDownHeld = false;
            return;
        }

        bool isGliding =
            umbrellaController.GetUmbrellaState() ==
            UmbrellaController.UmbrellaState.Open;

        moveInput = 0.0f;
        Vector2 move = Vector2.zero;

        // 左右移動の入力
        if (moveAction != null)
        {
            move = moveAction.ReadValue<Vector2>();
            moveInput = Mathf.Clamp(move.x, -1.0f, 1.0f);
        }

        UpdateFacingDirection();
        RefreshParryColliderFacing();

        bool isDownHeld = move.y < -0.5f;
        bool isDownPressedThisFrame = isDownHeld && !wasDownHeld;
        wasDownHeld = isDownHeld;

        if (isDownPressedThisFrame)
        {
            if (fallThroughController != null)
            {
                fallThroughController.SendMessage(
                    "TryFallThrough",
                    SendMessageOptions.DontRequireReceiver);
            }
        }

        if (dodgeController != null && dodgeController.IsDodging())
        {
            jumpInput = false;
            return;
        }

        //--------------回避関連------------------

        bool canUseDodge = false;

        if (playerAbilityController != null)
        {
            canUseDodge = playerAbilityController.GetCanDodge();
        }

        bool isDodgeTriggered =
            IsPressedThisFrame(dodgeAction) ||
            (IsPressed(dodgeAction) && IsPressedThisFrame(moveAction));

        if (canUseDodge)
        {
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

                if (dodgeController != null)
                {
                    dodgeController.Dodge(dodgeDirection);
                }
            }
        }

        //--------------ジャンプ関連------------------

        if (IsPressedThisFrame(jumpAction))
        {
            jumpInput = true;
        }

        //--------------パリィ・傘関連------------------

        //--------------パリィ・傘関連------------------

        if (IsPressedThisFrame(umbrellaToggleAction))
        {
            bool canUseParry = false;

            if (playerAbilityController != null)
            {
                canUseParry = playerAbilityController.GetCanParry();
            }

            if (canUseParry)
            {
                bool parrySuccess = false;

                if (parryHitbox != null)
                {
                    parrySuccess = parryHitbox.TryParryEnemyBullets();

                    if (!parrySuccess)
                    {
                        parrySuccess = parryHitbox.TryParryEnemyAttack();
                    }
                }

                if (parrySuccess)
                {
                    if (umbrellaController != null)
                    {
                        umbrellaController.SetUmbrellaState(
                            UmbrellaController.UmbrellaState.Open,
                            false);
                    }

                    if (umbrellaParryController != null)
                    {
                        umbrellaParryController.Parry();
                    }

                    return;
                }
            }

            if (umbrellaController != null)
            {
                umbrellaController.ToggleUmbrella();
            }
        }

        //--------------攻撃関連------------------

        if (IsPressedThisFrame(attackAction))
        {
            bool isUmbrellaOpen =
                umbrellaController.GetUmbrellaState() ==
                UmbrellaController.UmbrellaState.Open;

            bool isPlayerGliding =
                isUmbrellaOpen &&
                !isGround;

            // 滑空中射撃
            if (isPlayerGliding)
            {
                bool handledGlideShot = false;
                Camera mainCamera = Camera.main;
                bool canUseGunRecoil =
                    playerAbilityController != null &&
                    playerAbilityController.GetCanGunRecoil();

                if (!canUseGunRecoil)
                {
                    return;
                }

                if (mainCamera != null && gunController != null)
                {
                    if (TryGetAimWorldPosition(mainCamera, out Vector3 aimWorldPosition))
                    {
                        handledGlideShot = true;

                        Vector2 shootDirection =
                            (aimWorldPosition - transform.position).normalized;

                        gunController.Shoot(shootDirection);
                    }
                }

                if (handledGlideShot)
                {
                    return;
                }
            }

            // 傘が開いていたら閉じる
            if (isUmbrellaOpen)
            {
                umbrellaController.SetUmbrellaState(
                    UmbrellaController.UmbrellaState.Closed,
                    false);
            }

            // 通常攻撃
            if (umbrellaAttackController != null)
            {
                umbrellaAttackController.Attack();
            }
        }

        //--------------リコイルジャンプ関連------------------

        if (IsPressedThisFrame(recoilJumpAction))
        {
            if (isGround)
            {
                if (gunController != null)
                {
                    bool canUseGunRecoil = false;

                    if (playerAbilityController != null)
                    {
                        canUseGunRecoil =
                            playerAbilityController.GetCanGunRecoil();
                    }

                    if (canUseGunRecoil)
                    {
                        gunController.JumpRecoil();
                    }
                }
            }
        }
    }

    /// <summary>
    /// プレイヤーの左右移動処理。
    /// </summary>
    /// <remarks>
    /// 反動/回避のような外部インパルス挙動を優先するため、該当状態ではここでの速度上書きを止める。
    /// 「入力由来の速度」と「特殊アクション由来の速度」の競合回避が目的。
    /// </remarks>
    private void Move()
    {
        if (dodgeController != null && dodgeController.IsDodging()) { return; }

        if (gunController != null && gunController.GetRecoiling()) { return; }

        if (rigidBody2d == null) { return; }

        if (playerStatsData == null) { return; }

        if (umbrellaController == null) { return; }

        Vector2 velocity = rigidBody2d.linearVelocity;

        bool isGliding = umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open && !isGround;

        float horizontalInput = ResolveHorizontalMoveInput();

        if (horizontalInput != 0.0f)
        {
            float moveSpeed = playerStatsData.MoveSpeed;

            if (isGliding)
            {
                moveSpeed = umbrellaController.GetGlideMoveSpeed();
            }

            velocity.x = horizontalInput * moveSpeed;
        }
        else
        {
            if (isGround)
            {
                velocity.x = 0.0f;
            }
            else
            {
                if (isGliding)
                {
                    velocity.x *= 0.95f;
                }
                else
                {
                    velocity.x *= 0.98f;
                }
            }
        }

        rigidBody2d.linearVelocity = velocity;
    }

    private float ResolveHorizontalMoveInput()
    {
        return externalMovementActive ? externalMoveInput : moveInput;
    }

    private void UpdateExternalTargetMovement()
    {
        if (!externalMovementHasTarget)
        {
            return;
        }

        float remainingX = externalMovementTargetX - transform.position.x;
        float moveSpeed = playerStatsData != null ? playerStatsData.MoveSpeed : 0.0f;
        float arrivalDistance = Mathf.Max(ExternalMoveArrivalThreshold, moveSpeed * Time.fixedDeltaTime);

        if (Mathf.Abs(remainingX) <= arrivalDistance)
        {
            SnapExternalMovementTargetX();
            ClearExternalMovementDirection();
            return;
        }

        SetExternalMovementDirection(remainingX);
    }

    private void SnapExternalMovementTargetX()
    {
        Vector3 nextPosition = transform.position;
        nextPosition.x = externalMovementTargetX;
        transform.position = nextPosition;

        if (rigidBody2d == null)
        {
            return;
        }

        rigidBody2d.position = new Vector2(externalMovementTargetX, rigidBody2d.position.y);
        rigidBody2d.linearVelocity = new Vector2(0.0f, rigidBody2d.linearVelocity.y);
    }

    /// <summary>
    /// ジャンプ処理。
    /// </summary>
    /// <remarks>
    /// GetInput で立てた jumpInput フラグを消費する単方向フロー。
    /// 入力読み取りと物理反映を分離し、判定競合を減らしている。
    /// </remarks>
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
            float jumpVelocity = CreateJumpVelocity();
            rigidBody2d.linearVelocity = new Vector2(rigidBody2d.linearVelocity.x,jumpVelocity);
        }

        jumpInput = false;
    }

    private float CreateJumpVelocity()
    {
        if(playerStatsData == null)
        {
            return 0.0f;
        }

        if(rigidBody2d == null)
        {
            return 0.0f;
        }

        float gravity = Mathf.Abs(Physics2D.gravity.y * rigidBody2d.gravityScale);

        if (gravity <= 0.0f)
        {
            return 0.0f;
        }

        return Mathf.Sqrt(2.0f * gravity * playerStatsData.JumpForce);
    }

    /// <summary>
    /// View層が参照する向き状態の更新。
    /// </summary>
    /// <remarks>
    /// ルール:
    /// - 地上: 移動入力で向き決定
    /// - 滑空中: 移動入力優先。入力が無いときのみ Aim 方向で決定
    /// - 非滑空空中: 移動入力で決定
    /// </remarks>
    private void UpdateFacingDirection()
    {
        if (externalFacingLocked)
        {
            isFacingRight = externalFacingRight;
            return;
        }

        if (externalMovementActive)
        {
            if (externalMoveInput > 0.01f)
            {
                isFacingRight = true;
            }
            else if (externalMoveInput < -0.01f)
            {
                isFacingRight = false;
            }

            return;
        }

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
            if (moveInput > 0.01f)
            {
                isFacingRight = true;
                return;
            }

            if (moveInput < -0.01f)
            {
                isFacingRight = false;
                return;
            }

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

    private void RefreshParryColliderFacing()
    {
        if (umbrellaParryController != null)
        {
            umbrellaParryController.RefreshParryColliderFacing();
        }
    }

    // 接地遷移(空中 -> 接地)の副作用を集約する場所。
    // 着地時の演出/状態リセットを追加する場合はここに寄せる。
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
            if (gunController != null)
            {
                gunController.ResetRecoilCycle();
            }

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

    // InputAction のバインドを1か所に集約。
    // Action名は InputActionNames を唯一の正とし、Asset側名称と必ず一致させる。
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

    private bool TryGetAimWorldPosition(Camera mainCamera, out Vector3 worldPosition)
    {
        if (GameCursorController.TryGetClampedAimWorldPosition(
                transform,
                mainCamera,
                out worldPosition))
        {
            return true;
        }

        if (!TryGetAimScreenPosition(out Vector2 pointerPos))
        {
            worldPosition = Vector3.zero;
            return false;
        }

        worldPosition = mainCamera.ScreenToWorldPoint(
            new Vector3(
                pointerPos.x,
                pointerPos.y,
                Mathf.Abs(mainCamera.transform.position.z - transform.position.z)));
        worldPosition.z = transform.position.z;
        return true;
    }

    // 必須Actionの取得+有効化ヘルパー。
    // ここで失敗したActionがある場合は入力系をReadyにしない設計。
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

    // 壁張り付き対策: プレイヤー本体コライダーへ摩擦ゼロマテリアルをランタイムで適用する。
    // 既存マテリアルがある場合は、名前を引き継いだRuntime複製を作成して差し替える。
    private void ApplyNoFrictionMaterial()
    {
        if (playerCollider == null)
        {
            return;
        }

        if (playerCollider.sharedMaterial != null)
        {
            runtimeNoFrictionMaterial = new PhysicsMaterial2D($"{playerCollider.sharedMaterial.name}_RuntimeNoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
            playerCollider.sharedMaterial = runtimeNoFrictionMaterial;
            return;
        }

        runtimeNoFrictionMaterial = new PhysicsMaterial2D("Runtime_Player_NoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };
        playerCollider.sharedMaterial = runtimeNoFrictionMaterial;
    }
}
