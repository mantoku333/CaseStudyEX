using Cysharp.Threading.Tasks;
using UnityEngine;
using Player;

public class UmbrellaAttackController : MonoBehaviour
{
    private static readonly Vector2 SourceTextureSize = new Vector2(2560f, 2560f);
    private static readonly Vector2 SourceCellSize = new Vector2(512f, 512f);
    private static readonly Vector2Int[] AttackEffectFrameCells =
    {
        new Vector2Int(1, 0),
        new Vector2Int(2, 0),
        new Vector2Int(3, 0),
        new Vector2Int(4, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(2, 1),
        new Vector2Int(3, 1)
    };

    [Header("攻撃設定")]
    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float attackPerSecond = 4.0f;

    [Header("当たり判定")]
    [SerializeField] private Collider2D attackCollider;

    [Header("SE")]
    [SerializeField] private AudioClip player_normalAttack;

    [Header("斬撃エフェクト")]
    [SerializeField] private Texture2D attackEffectTexture;
    [SerializeField, Min(0.01f)] private float attackEffectFrameSeconds = 0.025f;
    [SerializeField] private Vector3 attackEffectWorldOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private Vector3 attackEffectScale = Vector3.one;
    [SerializeField] private int attackEffectSortingOrderOffset = 2;

    private bool isAttacking;
    private float lastAttackTime = -999.0f;

    private AudioSource audioSource;
    private SpriteRenderer sourceSpriteRenderer;
    private SpriteRenderer attackEffectRenderer;
    private Sprite[] attackEffectSprites;
    private IPlayerViewStateProvider facingStateProvider;

    private void Awake()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }

        audioSource = GetComponentInParent<AudioSource>();
        sourceSpriteRenderer = GetComponent<SpriteRenderer>();
        facingStateProvider = GetComponentInParent<IPlayerViewStateProvider>();
        EnsureAttackEffectRenderer();
    }

    public void SetAttackPerSecond(float attackPerSecond)
    {
        this.attackPerSecond = Mathf.Max(0.01f, attackPerSecond);
    }

    public float GetAttackPerSecond()
    {
        return attackPerSecond;
    }

    public void SetAttackDuration(float duration)
    {
        attackDuration = Mathf.Max(0.01f, duration);
    }

    public float GetAttackDuration()
    {
        return attackDuration;
    }

    public async UniTaskVoid Attack()
    {
        if (isAttacking) { return; }

        float attackInterval = 1.0f / attackPerSecond;
        if (Time.time < lastAttackTime + attackInterval) { return; }
        if (attackCollider == null) { return; }

        isAttacking = true;

        PlaySE(player_normalAttack);
        PlayAttackEffect().Forget();

        lastAttackTime = Time.time;
        attackCollider.enabled = true;

        await UniTask.Delay((int)(attackDuration * 1000));

        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }

        isAttacking = false;
    }

    private async UniTaskVoid PlayAttackEffect()
    {
        if (attackEffectTexture == null)
        {
            return;
        }

        EnsureAttackEffectRenderer();
        BuildAttackEffectSpritesIfNeeded();

        if (attackEffectRenderer == null || attackEffectSprites == null || attackEffectSprites.Length == 0)
        {
            return;
        }

        Vector3 effectPosition = attackCollider != null
            ? attackCollider.bounds.center
            : transform.position;

        bool isFacingLeft = IsFacingLeft();

        attackEffectRenderer.transform.position = effectPosition + GetFacingOffset(isFacingLeft);
        attackEffectRenderer.transform.localScale = attackEffectScale;
        attackEffectRenderer.flipX = isFacingLeft;
        attackEffectRenderer.enabled = true;

        for (int i = 0; i < attackEffectSprites.Length; i++)
        {
            Sprite frame = attackEffectSprites[i];
            if (frame == null)
            {
                continue;
            }

            attackEffectRenderer.sprite = frame;
            await UniTask.Delay((int)(attackEffectFrameSeconds * 1000f));
        }

        attackEffectRenderer.enabled = false;
        attackEffectRenderer.sprite = null;
    }

    private bool IsFacingLeft()
    {
        if (facingStateProvider != null)
        {
            return !facingStateProvider.IsFacingRight;
        }

        return sourceSpriteRenderer != null && sourceSpriteRenderer.flipX;
    }

    private Vector3 GetFacingOffset(bool isFacingLeft)
    {
        Vector3 offset = attackEffectWorldOffset;
        if (isFacingLeft)
        {
            offset.x *= -1f;
        }

        return offset;
    }

    private void EnsureAttackEffectRenderer()
    {
        if (attackEffectRenderer != null)
        {
            return;
        }

        Transform parentTransform = transform.root != null ? transform.root : transform;
        Transform existing = parentTransform.Find("AttackSlashEffect");

        if (existing == null)
        {
            GameObject effectObject = new GameObject("AttackSlashEffect");
            existing = effectObject.transform;
            existing.SetParent(parentTransform, false);
        }

        attackEffectRenderer = existing.GetComponent<SpriteRenderer>();
        if (attackEffectRenderer == null)
        {
            attackEffectRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
        }

        if (sourceSpriteRenderer != null)
        {
            attackEffectRenderer.sortingLayerID = sourceSpriteRenderer.sortingLayerID;
            attackEffectRenderer.sortingOrder = sourceSpriteRenderer.sortingOrder + attackEffectSortingOrderOffset;
            attackEffectRenderer.sharedMaterial = sourceSpriteRenderer.sharedMaterial;
        }
        else
        {
            attackEffectRenderer.sortingOrder = attackEffectSortingOrderOffset;
        }

        attackEffectRenderer.enabled = false;
        existing.localScale = attackEffectScale;
    }

    private void BuildAttackEffectSpritesIfNeeded()
    {
        if (attackEffectSprites != null && attackEffectSprites.Length > 0)
        {
            return;
        }

        attackEffectSprites = new Sprite[AttackEffectFrameCells.Length];
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < AttackEffectFrameCells.Length; i++)
        {
            Rect frameRect = GetScaledFrameRect(AttackEffectFrameCells[i]);
            attackEffectSprites[i] = Sprite.Create(
                attackEffectTexture,
                frameRect,
                pivot,
                Mathf.Max(1f, attackEffectTexture.width / 25.6f),
                0,
                SpriteMeshType.FullRect);
        }
    }

    private Rect GetScaledFrameRect(Vector2Int cell)
    {
        float scaleX = attackEffectTexture.width / SourceTextureSize.x;
        float scaleY = attackEffectTexture.height / SourceTextureSize.y;

        int x = Mathf.RoundToInt(cell.x * SourceCellSize.x * scaleX);
        int y = Mathf.RoundToInt(SourceTextureSize.y - ((cell.y + 1) * SourceCellSize.y));
        y = Mathf.RoundToInt(y * scaleY);

        int width = Mathf.RoundToInt(SourceCellSize.x * scaleX);
        int height = Mathf.RoundToInt(SourceCellSize.y * scaleY);

        width = Mathf.Min(width, attackEffectTexture.width - x);
        height = Mathf.Min(height, attackEffectTexture.height - y);

        return new Rect(x, y, width, height);
    }

    private void OnDestroy()
    {
        if (attackEffectSprites == null)
        {
            return;
        }

        for (int i = 0; i < attackEffectSprites.Length; i++)
        {
            if (attackEffectSprites[i] != null)
            {
                Destroy(attackEffectSprites[i]);
            }
        }
    }

    private void PlaySE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
