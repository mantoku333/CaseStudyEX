using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UmbrellaController : MonoBehaviour
{
    
    private Rigidbody2D rigidBody2D; //PlayerのRigidbody2D

    //--------------能力関連------------------
    private Player.PlayerAbilityController playerAbilityController;
    private PlayerDiveAttackController diveAttackController;
    private Player.IPlayerViewStateProvider playerStateProvider;

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

    [Header("見た目")]
    [SerializeField] private Sprite closedUmbrellaSprite;
    [SerializeField] private Sprite openDebugSprite;
    [SerializeField, Min(0.01f)] private float changeAnimationDuration = 0.2f;

    private UmbrellaState umbrellaState = UmbrellaState.Closed;  //現在の傘の状態
    private SpriteRenderer spriteRenderer;  //デバッグ用のスプライトレンダラー(傘が出来たら削除)
    private float changeAnimationTimer;

    /// <summary>
    /// Raised once when an open-umbrella airborne glide first becomes applicable.
    /// Temporary recoil, dive, or upward movement does not create another glide
    /// action; the action remains active until landing or closing the umbrella.
    /// </summary>
    public event System.Action GlideStarted;

    public bool IsGlideActionActive { get; private set; }

    [Header("SE")]
    [SerializeField] private AudioClip umbrella_open;       //傘開くSE
    [SerializeField] private AudioClip umbrella_close;      //傘閉じるSE

    private AudioSource audioSource;        //AudioSource

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rigidBody2D = GetComponentInParent<Rigidbody2D>();
        if (gunController == null && rigidBody2D != null)
        {
            gunController = rigidBody2D.GetComponentInChildren<GunController>(true);
        }
        audioSource = GetComponentInParent<AudioSource>();
        playerAbilityController = GetComponentInParent<Player.PlayerAbilityController>();
        diveAttackController = GetComponentInParent<PlayerDiveAttackController>();
        playerStateProvider = GetComponentInParent<Player.IPlayerViewStateProvider>();

        if (openDebugSprite == null && spriteRenderer != null)
        {
            openDebugSprite = spriteRenderer.sprite;
        }

        UpdateDebugColor();
    }

    private void Update()
    {
        if (changeAnimationTimer > 0f)
        {
            changeAnimationTimer = Mathf.Max(0f, changeAnimationTimer - Time.deltaTime);
        }

        Glide();
    }

    /// <summary>
    /// 傘の状態のSet関数
    /// </summary>
    /// <param name="state">セットする傘の状態</param>
    public void SetUmbrellaState(UmbrellaState state)
    {
        SetUmbrellaState(state, true);
    }

    public void SetUmbrellaState(UmbrellaState state, bool playChangeAnimation)
    {
        if (umbrellaState != state)
        {
            if (playChangeAnimation)
            {
                StartChangeAnimation();
            }

            umbrellaState = state;
        }

        if (state == UmbrellaState.Closed)
        {
            EndGlideActionSession();
        }

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

    public bool IsChanging()
    {
        return changeAnimationTimer > 0f;
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

    public void OpenUmbrella()
    {
        if (umbrellaState == UmbrellaState.Open)
        {
            UpdateDebugColor();
            return;
        }

        umbrellaState = UmbrellaState.Open;
        StartChangeAnimation();
        PlaySE(umbrella_open);
        UpdateDebugColor();
    }

    public void CloseUmbrella()
    {
        if (umbrellaState == UmbrellaState.Closed)
        {
            EndGlideActionSession();
            UpdateDebugColor();
            return;
        }

        umbrellaState = UmbrellaState.Closed;
        EndGlideActionSession();
        StartChangeAnimation();
        PlaySE(umbrella_close);
        UpdateDebugColor();
    }


    /// <summary>
    /// 傘の開閉を切り替える関数
    /// </summary>
    public void ToggleUmbrella()
    {
        if (umbrellaState == UmbrellaState.Closed)
        {
            OpenUmbrella();
        }
        else
        {
            CloseUmbrella();
        }
    }

    /// <summary>
    /// 傘での滑空を行う関数
    /// </summary>
    private void Glide()
    {
        if (umbrellaState == UmbrellaState.Closed ||
            (playerStateProvider != null && playerStateProvider.IsGrounded))
        {
            EndGlideActionSession();
        }

        if (rigidBody2D == null) { return; }

        // Do not keep applying glide fall velocity while the landing contact is
        // settling. Reapplying it here can make the GroundCheck alternate between
        // grounded and airborne across Update/FixedUpdate in standalone builds.
        if (playerStateProvider != null && playerStateProvider.IsGrounded) { return; }

        if (playerAbilityController == null) { return; }

        if (!playerAbilityController.GetCanGlide()) { return; }

        if (umbrellaState != UmbrellaState.Open) {  return; }

        if (gunController != null && gunController.GetRecoiling()) { return; }

        if (diveAttackController != null && diveAttackController.IsDiveAttacking) { return; }

        if (rigidBody2D.linearVelocity.y >= 0) { return; }

        BeginGlideActionSession();

        float maxFallVelocity = -Mathf.Abs(glideFallSpeed);

        if (rigidBody2D.linearVelocity.y < maxFallVelocity)
        {
            Vector2 velocity = rigidBody2D.linearVelocity;
            velocity.y = maxFallVelocity;
            rigidBody2D.linearVelocity = velocity;
        }
    }

    private void BeginGlideActionSession()
    {
        if (IsGlideActionActive)
        {
            return;
        }

        IsGlideActionActive = true;
        GlideStarted?.Invoke();
    }

    private void EndGlideActionSession()
    {
        IsGlideActionActive = false;
    }

    /// <summary>
    /// 傘の開閉に応じてスプライトの色を変える関数(デバッグ用)
    /// </summary>
    private void UpdateDebugColor()
    {
        if (spriteRenderer == null) { return; }

        if (umbrellaState == UmbrellaState.Open)
        {
            spriteRenderer.sprite = openDebugSprite;
            spriteRenderer.color = Color.blue;
        }
        else
        {
            spriteRenderer.sprite = closedUmbrellaSprite != null ? closedUmbrellaSprite : openDebugSprite;
            spriteRenderer.color = Color.white;
        }
    }

    private void StartChangeAnimation()
    {
        changeAnimationTimer = Mathf.Max(0.01f, changeAnimationDuration);
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
