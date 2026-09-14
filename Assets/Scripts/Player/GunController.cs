using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunController : MonoBehaviour
{
    private static readonly Vector2 RecoilEffectSourceTextureSize = new Vector2(5120f, 3072f);
    private static readonly Rect[] RecoilEffectSourceRects =
    {
        new Rect(379f, 2352f, 428f, 424f),
        new Rect(1425f, 2332f, 432f, 447f),
        new Rect(2485f, 2324f, 417f, 449f),
        new Rect(3538f, 2322f, 387f, 455f),
        new Rect(4574f, 2322f, 381f, 459f),
        new Rect(489f, 1299f, 380f, 462f),
        new Rect(1522f, 1300f, 383f, 466f),
        new Rect(2553f, 1301f, 387f, 468f),
        new Rect(3584f, 1301f, 390f, 470f),
        new Rect(4616f, 1301f, 388f, 471f),
        new Rect(532f, 278f, 378f, 462f),
        new Rect(1571f, 283f, 362f, 431f),
        new Rect(2614f, 290f, 335f, 419f),
        new Rect(3638f, 290f, 335f, 419f),
    };

    [Header("銃の設定")]
    [SerializeField, Min(0f)] private float secondRecoilPowerMultiplier = 1.0f;
    [SerializeField] private float airRecoilPower  = 25.0f;   //銃反動/リコイルジャンプ共通の反動量
    [SerializeField] private float recoilDuration = 0.1f;      //反動状態の時間

    [Header("地面判定")]
    [SerializeField] private GroundCheck groundCheck;   //地面判定のスクリプト

    [Header("SE")]
    [SerializeField] private AudioClip player_gun_fire;  //発砲時SE

    [Header("発砲エフェクト")]
    [SerializeField] private Texture2D recoilEffectTexture;
    [SerializeField, Min(0.01f)] private float recoilEffectFrameSeconds = 0.025f;
    [SerializeField, Min(0f)] private float recoilEffectSpawnDistance = 0.6f;
    [SerializeField] private Vector3 recoilEffectWorldOffset = Vector3.zero;
    [SerializeField] private Vector3 recoilEffectScale = Vector3.one;
    [SerializeField] private int recoilEffectSortingOrderOffset = 3;

    private bool isRecoiling = false;   　//反動が起きているかどうか
    private bool airborneRecoilStartedForCurrentAction = false;
    private bool preserveHorizontalRecoilMomentum = false;
    private float firstRecoilCoolTime = 0.5f;
    private float secondRecoilCoolTime = 2.5f;
    private float recoilCooldownMultiplier = 1.0f;
    private bool recoilCooldownDisabled = false;
    private float currentCoolTime = 0.0f; //クールタイムの残り時間    
    private float currentCoolTimeDuration = 0.0f;
    private bool isSecondRecoilNext = false;
    private Rigidbody2D rigidBody2d;      //反動を加えるためのRigidbody2D
    private PlayerCollisionMover2D collisionMover;
    private Player.IPlayerViewStateProvider playerStateProvider;
    private float defaultLinearDamping = 0.0f;
    private float recoilForceBonus = 0.0f;

    private AudioSource audioSource;      //AudioSource
    private SpriteRenderer sourceSpriteRenderer;
    private Sprite[] recoilEffectSprites;
    private RecoilTrajectoryPreview trajectoryPreview;

    /// <summary>
    /// Raised at most once per physical recoil, on the first frame that recoil
    /// is observed airborne. Once started, ground-check flicker does not end or
    /// restart the action; it remains active until the recoil itself ends.
    /// </summary>
    public event System.Action AirborneRecoilStarted;
    public bool CurrentRecoilIsJump { get; private set; }
    public Vector2 RecoilStartPosition { get; private set; }
    public int RecoilSequence { get; private set; }

    public bool IsAirborneRecoilActive { get; private set; }

    public float CurrentCoolTime => Mathf.Max(0.0f, currentCoolTime);
    public float ReloadDuration => Mathf.Max(0.0f, currentCoolTimeDuration);
    public float ReloadRemainingRatio =>
        currentCoolTimeDuration > 0.0f
            ? Mathf.Clamp01(currentCoolTime / currentCoolTimeDuration)
            : 0.0f;
    public bool IsReloading => currentCoolTime > 0.0f && currentCoolTimeDuration > 0.0f;

    void Start()
    {
        //Rigidbody2Dの取得
        rigidBody2d = GetComponentInParent<Rigidbody2D>();
        if (rigidBody2d != null)
        {
            defaultLinearDamping = rigidBody2d.linearDamping;
            collisionMover = rigidBody2d.GetComponent<PlayerCollisionMover2D>();

            if (groundCheck == null)
            {
                groundCheck = rigidBody2d.GetComponentInChildren<GroundCheck>(true);
            }
        }

        playerStateProvider = GetComponentInParent<Player.IPlayerViewStateProvider>();

        //AudioSourceの取得
        audioSource = GetComponentInParent<AudioSource>();
        sourceSpriteRenderer = GetComponent<SpriteRenderer>();

        trajectoryPreview = GetComponent<RecoilTrajectoryPreview>();
        if (trajectoryPreview == null)
        {
            trajectoryPreview = gameObject.AddComponent<RecoilTrajectoryPreview>();
        }

        trajectoryPreview.Initialize(this);
    }

    void Update()
    {
        //クールタイムの更新
        if (currentCoolTime > 0)
        {
            currentCoolTime = Mathf.Max(0.0f, currentCoolTime - Time.deltaTime);
        }

        UpdateAirborneRecoilLifecycle();
    }

    void FixedUpdate()
    {
        // 反動中は毎FixedUpdateで壁向きの速度を削り、壁に押し込まれず沿って流れるようにする。
        ProjectRecoilVelocityForNextFixedStep();
        UpdateAirborneRecoilLifecycle();
    }


    /// <summary>
    /// 反動が起きているかどうかを返すGet関数
    /// </summary>
    /// <returns>反動が起きているかのbool値</returns>
    public bool GetRecoiling()
    {
        return isRecoiling;
    }

    /// <summary>
    /// 通常の空中移動速度まで減速する間、移動入力による横速度の上書きを防ぐ。
    /// </summary>
    public bool ShouldPreserveHorizontalRecoil(float horizontalControlSpeed)
    {
        if (!preserveHorizontalRecoilMomentum || rigidBody2d == null)
        {
            return false;
        }

        if (Mathf.Abs(rigidBody2d.linearVelocity.x) <= Mathf.Max(0.0f, horizontalControlSpeed))
        {
            preserveHorizontalRecoilMomentum = false;
            return false;
        }

        return true;
    }

    public void SetAirRecoilPower(float force)
    {
        airRecoilPower = force;
    }
    public float GetAirRecoilPower()
    {
        return airRecoilPower;
    }

    public Vector2 GetCurrentRecoilLaunchVelocity(Vector2 shotDirection)
    {
        if (shotDirection.sqrMagnitude <= 0.0f)
        {
            return Vector2.zero;
        }

        return -shotDirection.normalized *
            GetModifiedAirRecoilPower() *
            GetCurrentRecoilPowerMultiplier();
    }

    public void SetRecoilForceBonus(float bonus)
    {
        recoilForceBonus = Mathf.Max(0.0f, bonus);
    }

    public void SetRecoilDuration(float duration)
    {
        recoilDuration = Mathf.Max(0.01f, duration);
    }

    public float GetRecoilDuration()
    {
        return recoilDuration;
    }


    public void SetRecoilCoolTimes(float firstCoolTime, float secondCoolTime)
    {
        firstRecoilCoolTime = Mathf.Max(0.0f, firstCoolTime);
        secondRecoilCoolTime = Mathf.Max(0.0f, secondCoolTime);
    }

    public void SetRecoilCooldownModifier(float multiplier, bool disabled)
    {
        recoilCooldownMultiplier = Mathf.Max(0.0f, multiplier);
        recoilCooldownDisabled = disabled;

        if (recoilCooldownDisabled)
        {
            currentCoolTime = 0.0f;
            currentCoolTimeDuration = 0.0f;
        }
    }

    public void ResetRecoilCycle()
    {
        isSecondRecoilNext = false;
        preserveHorizontalRecoilMomentum = false;
    }

    public void RestoreAllRecoilUses()
    {
        currentCoolTime = 0.0f;
        currentCoolTimeDuration = 0.0f;
        ResetRecoilCycle();
    }



    /// <summary>
    /// 銃の発射処理を行う関数
    /// </summary>
    /// <param name="direction">銃の発射方向</param>
    public void Shoot(Vector2 direction)
    {
        if (currentCoolTime > 0) { return; }

        float recoilPowerMultiplier = GetCurrentRecoilPowerMultiplier();

        //銃の反動を適用
        if (!TryApplyRecoil(direction, recoilPowerMultiplier))
        {
            return;
        }

        StartRecoilCoolTime();

        PlayRecoilEffect(direction);

        //発砲SE再生
        PlaySE(player_gun_fire);
        //Debug.Log("Shoot! Cooldown started.");
    }

    /// <summary>
    /// 銃の反動を適用する関数
    /// </summary>
    /// <param name="direction">反動の方向</param>
    private bool TryApplyRecoil(Vector2 direction, float powerMultiplier)
    {
        if (rigidBody2d == null){ return false; }

        if (direction == Vector2.zero){ return false; }

        isRecoiling = true;

        //反動中は空気抵抗を増やす
        rigidBody2d.linearDamping = 2.0f;

        // 射撃前の移動速度を引き継ぐと、同じ照準でも移動キーの向きによって
        // 飛距離が変わってしまう。反動量を初速として直接設定し、毎回同じ軌道にする。
        Vector2 recoilVelocity =
            -direction.normalized * GetModifiedAirRecoilPower() * powerMultiplier;
        rigidBody2d.linearVelocity = recoilVelocity;
        preserveHorizontalRecoilMomentum = Mathf.Abs(recoilVelocity.x) > 0.0f;

        BeginRecoil(recoilDuration);
        // 速度設定直後にも補正して、次の物理ステップ前に壁方向の速度が残らないようにする。
        ProjectRecoilVelocityForNextFixedStep();
        return true;
    }

    /// <summary>
    /// 地面からの跳ね上がりの反動を適用する関数
    /// </summary>
    public void JumpRecoil()
    {
        if (currentCoolTime > 0){ return; }

        if (rigidBody2d == null){ return; }

        float recoilPowerMultiplier = GetCurrentRecoilPowerMultiplier();

        // 射撃反動と同様に初速を直接設定し、直前の移動や Rigidbody の質量に左右されないようにする。
        rigidBody2d.linearVelocity =
            Vector2.up * GetModifiedAirRecoilPower() * recoilPowerMultiplier;
        preserveHorizontalRecoilMomentum = false;
        BeginRecoil(recoilDuration, true);
        // リコイルジャンプも同じ補正を通し、天井や角で押し込まれないようにする。
        ProjectRecoilVelocityForNextFixedStep();

        StartRecoilCoolTime();
        PlayRecoilEffect(Vector2.down);

        //Debug.Log("Jump Recoil!");
    }

    private void BeginRecoil(float duration, bool isJump = false)
    {
        CurrentRecoilIsJump = isJump;
        RecoilStartPosition = rigidBody2d != null ? rigidBody2d.position : (Vector2)transform.position;
        RecoilSequence++;
        isRecoiling = true;
        airborneRecoilStartedForCurrentAction = false;
        IsAirborneRecoilActive = false;
        if (rigidBody2d != null)
        {
            rigidBody2d.linearDamping = 2.0f;
            rigidBody2d.WakeUp();
        }

        CancelInvoke(nameof(EndRecoil));
        Invoke(nameof(EndRecoil), duration);
        UpdateAirborneRecoilLifecycle();
    }

    private void UpdateAirborneRecoilLifecycle()
    {
        if (!isRecoiling || airborneRecoilStartedForCurrentAction || !IsCurrentlyAirborne())
        {
            return;
        }

        airborneRecoilStartedForCurrentAction = true;
        IsAirborneRecoilActive = true;
        AirborneRecoilStarted?.Invoke();
    }

    private bool IsCurrentlyAirborne()
    {
        if (playerStateProvider != null)
        {
            return !playerStateProvider.IsGrounded;
        }

        return groundCheck != null && !groundCheck.IsGround();
    }

    private void ProjectRecoilVelocityForNextFixedStep()
    {
        if (!isRecoiling || rigidBody2d == null)
        {
            return;
        }

        if (collisionMover == null)
        {
            // 銃は子オブジェクトに付いているため、親Rigidbody側の移動ヘルパーを遅延取得する。
            collisionMover = rigidBody2d.GetComponent<PlayerCollisionMover2D>();
        }

        if (collisionMover == null)
        {
            return;
        }

        // 現在速度を「次のFixedUpdateで移動する距離」としてSweepし、壁法線方向だけを取り除く。
        rigidBody2d.linearVelocity =
            collisionMover.ProjectRecoilVelocityForNextFixedStep(rigidBody2d.linearVelocity);
    }

    private float GetCurrentRecoilPowerMultiplier()
    {
        return isSecondRecoilNext ? secondRecoilPowerMultiplier : 1.0f;
    }

    private float GetModifiedAirRecoilPower()
    {
        return Mathf.Max(0.0f, airRecoilPower + recoilForceBonus);
    }

    private void StartRecoilCoolTime()
    {
        currentCoolTimeDuration = GetCurrentRecoilCoolTimeDuration();
        currentCoolTime = currentCoolTimeDuration;
        isSecondRecoilNext = !isSecondRecoilNext;
    }

    private float GetCurrentRecoilCoolTimeDuration()
    {
        if (recoilCooldownDisabled)
        {
            return 0.0f;
        }

        float baseCoolTime = isSecondRecoilNext
            ? secondRecoilCoolTime
            : firstRecoilCoolTime;
        return Mathf.Max(0.0f, baseCoolTime * recoilCooldownMultiplier);
    }

    /// <summary>
    /// 反動終了の処理を行う関数
    /// </summary>
    void EndRecoil()
    {
        isRecoiling = false;
        airborneRecoilStartedForCurrentAction = false;
        IsAirborneRecoilActive = false;
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

    private void PlayRecoilEffect(Vector2 direction)
    {
        if (recoilEffectTexture == null)
        {
            return;
        }

        BuildRecoilEffectSpritesIfNeeded();

        if (recoilEffectSprites == null || recoilEffectSprites.Length == 0)
        {
            return;
        }

        StartCoroutine(PlayRecoilEffectCoroutine(direction));
    }

    private IEnumerator PlayRecoilEffectCoroutine(Vector2 direction)
    {
        Vector2 normalizedDirection = direction.sqrMagnitude > 0f
            ? direction.normalized
            : Vector2.right;

        Vector3 effectPosition =
            transform.position +
            (Vector3)(normalizedDirection * recoilEffectSpawnDistance) +
            recoilEffectWorldOffset;

        GameObject effectObject = new GameObject("RecoilShotEffect");
        effectObject.transform.position = effectPosition;
        effectObject.transform.rotation = Quaternion.identity;
        effectObject.transform.localScale = recoilEffectScale;

        SpriteRenderer recoilEffectRenderer = effectObject.AddComponent<SpriteRenderer>();
        ApplyRecoilEffectRendererSettings(recoilEffectRenderer);
        recoilEffectRenderer.flipX = normalizedDirection.x < -0.01f;
        recoilEffectRenderer.flipY = false;

        for (int i = 0; i < recoilEffectSprites.Length; i++)
        {
            if (recoilEffectRenderer == null)
            {
                yield break;
            }

            Sprite frame = recoilEffectSprites[i];
            if (frame != null)
            {
                recoilEffectRenderer.sprite = frame;
            }

            yield return new WaitForSeconds(recoilEffectFrameSeconds);
        }

        Destroy(effectObject);
    }

    private void ApplyRecoilEffectRendererSettings(SpriteRenderer recoilEffectRenderer)
    {
        if (recoilEffectRenderer == null)
        {
            return;
        }

        if (sourceSpriteRenderer != null)
        {
            recoilEffectRenderer.sortingLayerID = sourceSpriteRenderer.sortingLayerID;
            recoilEffectRenderer.sortingOrder = sourceSpriteRenderer.sortingOrder + recoilEffectSortingOrderOffset;
            recoilEffectRenderer.sharedMaterial = sourceSpriteRenderer.sharedMaterial;
        }
        else
        {
            recoilEffectRenderer.sortingOrder = recoilEffectSortingOrderOffset;
        }
    }

    private void BuildRecoilEffectSpritesIfNeeded()
    {
        if (recoilEffectSprites != null && recoilEffectSprites.Length > 0)
        {
            return;
        }

        recoilEffectSprites = new Sprite[RecoilEffectSourceRects.Length];
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        float pixelsPerUnit = Mathf.Max(1f, recoilEffectTexture.width / 25.6f);

        for (int i = 0; i < RecoilEffectSourceRects.Length; i++)
        {
            Rect frameRect = GetScaledRecoilEffectFrameRect(RecoilEffectSourceRects[i]);
            recoilEffectSprites[i] = Sprite.Create(
                recoilEffectTexture,
                frameRect,
                pivot,
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }
    }

    private Rect GetScaledRecoilEffectFrameRect(Rect sourceRect)
    {
        float scaleX = recoilEffectTexture.width / RecoilEffectSourceTextureSize.x;
        float scaleY = recoilEffectTexture.height / RecoilEffectSourceTextureSize.y;

        int x = Mathf.RoundToInt(sourceRect.x * scaleX);
        int y = Mathf.RoundToInt(sourceRect.y * scaleY);
        int width = Mathf.RoundToInt(sourceRect.width * scaleX);
        int height = Mathf.RoundToInt(sourceRect.height * scaleY);

        width = Mathf.Min(width, recoilEffectTexture.width - x);
        height = Mathf.Min(height, recoilEffectTexture.height - y);

        return new Rect(x, y, width, height);
    }

    private void OnDestroy()
    {
        if (recoilEffectSprites == null)
        {
            return;
        }

        for (int i = 0; i < recoilEffectSprites.Length; i++)
        {
            if (recoilEffectSprites[i] != null)
            {
                Destroy(recoilEffectSprites[i]);
            }
        }
    }
}
