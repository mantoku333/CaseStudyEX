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
    [SerializeField] private float coolTime = 3.0f;           //銃のクールタイム
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
    private float currentCoolTime = 0.0f; //クールタイムの残り時間    
    private Rigidbody2D rigidBody2d;      //反動を加えるためのRigidbody2D
    private float defaultLinearDamping = 0.0f;

    private AudioSource audioSource;      //AudioSource
    private SpriteRenderer sourceSpriteRenderer;
    private Sprite[] recoilEffectSprites;

    void Start()
    {
        //Rigidbody2Dの取得
        rigidBody2d = GetComponentInParent<Rigidbody2D>();
        if (rigidBody2d != null)
        {
            defaultLinearDamping = rigidBody2d.linearDamping;
        }

        //AudioSourceの取得
        audioSource = GetComponentInParent<AudioSource>();
        sourceSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        //クールタイムの更新
        if (currentCoolTime > 0)
        {
            currentCoolTime -= Time.deltaTime;
        }
    }


    /// <summary>
    /// 反動が起きているかどうかを返すGet関数
    /// </summary>
    /// <returns>反動が起きているかのbool値</returns>
    public bool GetRecoiling()
    {
        return isRecoiling;
    }

    public void SetAirRecoilPower(float force)
    {
        airRecoilPower = force;
    }
    public float GetAirRecoilPower()
    {
        return airRecoilPower;
    }

    public void SetRecoilDuration(float duration)
    {
        recoilDuration = Mathf.Max(0.01f, duration);
    }

    public float GetRecoilDuration()
    {
        return recoilDuration;
    }


    public void SetCoolTime(float time)
    {
        coolTime = time;
    }

    public float GetCoolTime()
    {
        return coolTime;
    }



    /// <summary>
    /// 銃の発射処理を行う関数
    /// </summary>
    /// <param name="direction">銃の発射方向</param>
    public void Shoot(Vector2 direction)
    {
        if (currentCoolTime > 0) { return; }

        //銃の反動を適用
        ApplyRecoil(direction);

        currentCoolTime = coolTime;

        PlayRecoilEffect(direction);

        //発砲SE再生
        PlaySE(player_gun_fire);
        //Debug.Log("Shoot! Cooldown started.");
    }

    /// <summary>
    /// 銃の反動を適用する関数
    /// </summary>
    /// <param name="direction">反動の方向</param>
    void ApplyRecoil(Vector2 direction)
    {
        if (rigidBody2d == null){ return; }

        if (direction == Vector2.zero){ return; }

        isRecoiling = true;

        //反動中は空気抵抗を増やす
        rigidBody2d.linearDamping = 2.0f;

        //現在の速度を取得
        Vector2 recoil = -direction.normalized * airRecoilPower;
        rigidBody2d.AddForce(recoil, ForceMode2D.Impulse);

        BeginRecoil(recoilDuration);
    }

    /// <summary>
    /// 地面からの跳ね上がりの反動を適用する関数
    /// </summary>
    public void JumpRecoil()
    {
        if (currentCoolTime > 0){ return; }

        if (rigidBody2d == null){ return; }

        // リコイルジャンプ中は移動入力と混ざらないように速度をリセットしてからインパルスを与える
        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.x = 0.0f;
        velocity.y = 0.0f;
        rigidBody2d.linearVelocity = velocity;
        rigidBody2d.AddForce(Vector2.up * airRecoilPower, ForceMode2D.Impulse);
        BeginRecoil(recoilDuration);

        currentCoolTime = coolTime;
        PlayRecoilEffect(Vector2.down);

        //Debug.Log("Jump Recoil!");
    }

    private void BeginRecoil(float duration)
    {
        isRecoiling = true;
        if (rigidBody2d != null)
        {
            rigidBody2d.linearDamping = 2.0f;
            rigidBody2d.WakeUp();
        }

        CancelInvoke(nameof(EndRecoil));
        Invoke(nameof(EndRecoil), duration);
    }

    /// <summary>
    /// 反動終了の処理を行う関数
    /// </summary>
    void EndRecoil()
    {
        isRecoiling = false;
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
