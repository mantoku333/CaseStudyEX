using UnityEngine;
using Cysharp.Threading.Tasks;
using Player;
using System.Collections;
using System.Collections.Generic;

public class UmbrellaParryController : MonoBehaviour
{
    [Header("パリィ時間")]
    [SerializeField] private float parryDuration = 0.1f;   //パリィ状態が続く時間

    [Header("当たり判定")]
    [SerializeField] private Collider2D parryCollider;     //パリィの当たり判定用コライダー
    [SerializeField] private float leftFacingParryColliderRightOffset = 1f;

    [SerializeField] private SpriteRenderer playerSprite;    //プレイヤーのスプライトレンダラー
    [SerializeField] private Color parryColor = Color.white; //パリィ中のフラッシュの色
    [SerializeField] private float flashDuration = 0.1f;     //フラッシュの持続時間

    [Header("パリィ成功エフェクト")]
    [SerializeField] private Texture2D parryEffectSpriteSheet;
    [SerializeField, Min(1)] private int parryEffectFrameColumns = 5;
    [SerializeField, Min(1)] private int parryEffectFrameRows = 3;
    [SerializeField, Min(1)] private int parryEffectFrameCount = 15;
    [SerializeField, Min(0.01f)] private float parryEffectFrameSeconds = 0.033f;
    [SerializeField, Min(1f)] private float parryEffectPixelsPerUnit = 100f;
    [SerializeField] private Vector2 parryEffectSpritePivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector3 parryEffectWorldOffset = Vector3.zero;
    [SerializeField] private Vector3 parryEffectScale = Vector3.one;
    [SerializeField] private int parryEffectSortingOrderOffset = 4;
    [SerializeField] private bool copyPlayerMaterial = true;

    [Header("SE")]
    [SerializeField] private AudioClip umbrella_open;    //パリィ時SE
    [SerializeField] private AudioClip parrySuccessClip; //パリィ成功時SE

    private Color defaultColor;       //プレイヤーのスプライトのデフォルトの色
    private bool isParrying = false;  //現在パリィ状態かどうかのフラグ

    private AudioSource audioSource;      //AudioSource
    private IPlayerViewStateProvider facingStateProvider;
    private Vector3 parryColliderDefaultLocalPosition;
    private bool hasParryColliderDefaultLocalPosition;
    private readonly List<Sprite> generatedParryEffectSprites = new();
    private Sprite[] parryEffectFrames;
    private GameObject parryEffectObject;
    private SpriteRenderer parryEffectRenderer;
    private Coroutine parryEffectRoutine;

    private void Awake()
    {
        if (parryCollider != null)
        {
            parryColliderDefaultLocalPosition = parryCollider.transform.localPosition;
            hasParryColliderDefaultLocalPosition = true;
        }

        if (playerSprite != null)
        {
            defaultColor = playerSprite.color;
        }
        //AudioSourceの取得
        audioSource = GetComponentInParent<AudioSource>();
        facingStateProvider = GetComponentInParent<IPlayerViewStateProvider>();
        BuildParryEffectFramesIfNeeded();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        parryEffectFrameColumns = Mathf.Max(1, parryEffectFrameColumns);
        parryEffectFrameRows = Mathf.Max(1, parryEffectFrameRows);
        parryEffectFrameCount = Mathf.Max(1, parryEffectFrameCount);
        parryEffectFrameSeconds = Mathf.Max(0.01f, parryEffectFrameSeconds);
        parryEffectPixelsPerUnit = Mathf.Max(1f, parryEffectPixelsPerUnit);
        parryEffectSpritePivot = new Vector2(
            Mathf.Clamp01(parryEffectSpritePivot.x),
            Mathf.Clamp01(parryEffectSpritePivot.y));
    }
#endif

    public void SetParryDuration(float duration)
    {
        parryDuration = Mathf.Max(0.01f, duration);
    }

    public float GetParryDuration()
    {
        return parryDuration;
    }

    public void SetFlashDuration(float duration)
    {
        flashDuration = Mathf.Max(0.01f, duration);
    }

    public float GetFlashDuration()
    {
        return flashDuration;
    }

    /// <summary>
    /// パリィ時の処理を行う関数
    /// </summary>
    /// <returns></returns>
    public UniTaskVoid Parry()
    {
        Vector2 fallbackDirection = IsFacingLeft() ? Vector2.left : Vector2.right;
        Vector2 effectPosition = ResolveParryEffectPosition();
        return Parry(effectPosition + fallbackDirection);
    }

    public async UniTaskVoid Parry(Vector2 hitWorldPosition)
    {
        RefreshParryColliderFacing();

        if (isParrying){ return; }

        //傘開けるSE再生
        PlaySE(umbrella_open);
        PlaySE(parrySuccessClip);

        isParrying = true;

        if (parryCollider != null)
        {
            parryCollider.enabled = true;
        }

        //パリィ成功のフラッシュエフェクト
        // FlashEffect().Forget();
        PlayParrySuccessEffect(hitWorldPosition);

        //パリィ状態が続く時間待機
        await UniTask.Delay((int)(parryDuration * 1000));

        //if (parryCollider != null)
        //{
        //    parryCollider.enabled = false;
        //}

        isParrying = false;
    }

    /// <summary>
    /// パリィが出来たらプレイヤーが光るエフェクトを行う関数
    /// </summary>
    /// <returns></returns>
    private async UniTask FlashEffect()
    {
        if (playerSprite == null){ return; }

        // playerSprite.color = parryColor;

        await UniTask.Delay((int)(flashDuration * 1000));

        // playerSprite.color = defaultColor;
    }

    public void PlayParrySuccessEffect(Vector2 hitWorldPosition)
    {
        BuildParryEffectFramesIfNeeded();
        if (parryEffectFrames == null || parryEffectFrames.Length == 0)
        {
            return;
        }

        StopAndDestroyParryEffect();

        Vector3 effectPosition = ResolveParryEffectPosition();
        Vector2 direction = hitWorldPosition - (Vector2)effectPosition;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = IsFacingLeft() ? Vector2.left : Vector2.right;
        }

        bool effectFacesLeft = ResolveParryEffectFacingLeft(direction);

        parryEffectObject = new GameObject("ParrySuccessEffect");
        parryEffectObject.transform.position = effectPosition;
        parryEffectObject.transform.rotation = ResolveParryEffectRotation(direction.normalized, effectFacesLeft);
        parryEffectObject.transform.localScale = parryEffectScale;
        parryEffectObject.transform.SetParent(transform, true);

        parryEffectRenderer = parryEffectObject.AddComponent<SpriteRenderer>();
        parryEffectRenderer.flipX = !effectFacesLeft;
        ApplyParryEffectRendererSettings(parryEffectRenderer);
        parryEffectRoutine = StartCoroutine(PlayParryEffectRoutine());
    }

    private Vector3 ResolveParryEffectPosition()
    {
        if (playerSprite != null)
        {
            return playerSprite.bounds.center + parryEffectWorldOffset;
        }

        return transform.position + parryEffectWorldOffset;
    }

    private bool ResolveParryEffectFacingLeft(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > 0.001f)
        {
            return direction.x < 0f;
        }

        return IsFacingLeft();
    }

    private static Quaternion ResolveParryEffectRotation(Vector2 direction, bool effectFacesLeft)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float baseAngle = effectFacesLeft ? 180f : 0f;
        float angleOffset = Mathf.DeltaAngle(baseAngle, angle);
        return Quaternion.Euler(0f, 0f, angleOffset);
    }

    private IEnumerator PlayParryEffectRoutine()
    {
        for (int i = 0; i < parryEffectFrames.Length; i++)
        {
            if (parryEffectRenderer == null)
            {
                yield break;
            }

            Sprite frame = parryEffectFrames[i];
            if (frame != null)
            {
                parryEffectRenderer.sprite = frame;
            }

            yield return new WaitForSeconds(parryEffectFrameSeconds);
        }

        StopAndDestroyParryEffect();
    }

    private void BuildParryEffectFramesIfNeeded()
    {
        if (parryEffectFrames == null || parryEffectFrames.Length == 0)
        {
            parryEffectFrames = BuildParryEffectFrames(parryEffectSpriteSheet);
        }
    }

    private Sprite[] BuildParryEffectFrames(Texture2D spriteSheet)
    {
        if (spriteSheet == null)
        {
            return System.Array.Empty<Sprite>();
        }

        int frameWidth = spriteSheet.width / parryEffectFrameColumns;
        int frameHeight = spriteSheet.height / parryEffectFrameRows;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return System.Array.Empty<Sprite>();
        }

        int maxFrameCount = Mathf.Min(parryEffectFrameCount, parryEffectFrameColumns * parryEffectFrameRows);
        Sprite[] frames = new Sprite[maxFrameCount];
        int index = 0;

        for (int row = 0; row < parryEffectFrameRows && index < maxFrameCount; row++)
        {
            int y = spriteSheet.height - ((row + 1) * frameHeight);

            for (int column = 0; column < parryEffectFrameColumns && index < maxFrameCount; column++)
            {
                Rect rect = new Rect(column * frameWidth, y, frameWidth, frameHeight);
                Sprite sprite = Sprite.Create(
                    spriteSheet,
                    rect,
                    parryEffectSpritePivot,
                    parryEffectPixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                frames[index++] = sprite;
                generatedParryEffectSprites.Add(sprite);
            }
        }

        return frames;
    }

    private void StopAndDestroyParryEffect()
    {
        if (parryEffectRoutine != null)
        {
            StopCoroutine(parryEffectRoutine);
            parryEffectRoutine = null;
        }

        if (parryEffectObject != null)
        {
            Destroy(parryEffectObject);
            parryEffectObject = null;
            parryEffectRenderer = null;
        }
    }

    private void ApplyParryEffectRendererSettings(SpriteRenderer renderer)
    {
        if (playerSprite == null)
        {
            renderer.sortingOrder = parryEffectSortingOrderOffset;
            return;
        }

        renderer.sortingLayerID = playerSprite.sortingLayerID;
        renderer.sortingOrder = playerSprite.sortingOrder + parryEffectSortingOrderOffset;

        if (copyPlayerMaterial && playerSprite.sharedMaterial != null)
        {
            renderer.sharedMaterial = playerSprite.sharedMaterial;
        }
    }

    /// <summary>
    /// パリィ状態かどうかを返す関数
    /// </summary>
    /// <returns>今パリィしているかどうかのbool型</returns>
    public bool IsParrying()
    {
        return isParrying;
    }

    public void RefreshParryColliderFacing()
    {
        UpdateParryColliderFacing();
    }

    private bool IsFacingLeft()
    {
        if (facingStateProvider != null)
        {
            return !facingStateProvider.IsFacingRight;
        }

        return playerSprite != null && playerSprite.flipX;
    }

    private void UpdateParryColliderFacing()
    {
        if (parryCollider == null || !hasParryColliderDefaultLocalPosition)
        {
            return;
        }

        Vector3 localPosition = parryColliderDefaultLocalPosition;
        bool isFacingLeft = IsFacingLeft();
        localPosition.x = Mathf.Abs(parryColliderDefaultLocalPosition.x) * (isFacingLeft ? -1f : 1f);
        if (isFacingLeft)
        {
            localPosition.x += leftFacingParryColliderRightOffset;
        }

        parryCollider.transform.localPosition = localPosition;
    }

    /// <summary>
    /// SE再生用関数
    /// </summary>
    private void PlaySE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    private void OnDestroy()
    {
        StopAndDestroyParryEffect();

        for (int i = 0; i < generatedParryEffectSprites.Count; i++)
        {
            if (generatedParryEffectSprites[i] != null)
            {
                Destroy(generatedParryEffectSprites[i]);
            }
        }
    }
}
