using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
// 攻撃を受けるとシャッターを開閉するレバー
public class LeverSwitch2D : MonoBehaviour, IAttackReceiver
{
    // 連動するシャッター本体
    [SerializeField] private ShutterWallBlockRise shutterWall;
    // レバーの見た目を切り替えるための SpriteRenderer
    [SerializeField] private SpriteRenderer leverRenderer;
    // 閉状態のレバースプライト
    [SerializeField] private Sprite closedSprite;
    // 開状態のレバースプライト
    [SerializeField] private Sprite openedSprite;
    // true の場合、最初の成功操作後に再操作不可
    [SerializeField] private bool oneShot = true;

    // 初期化の多重実行を防ぐフラグ
    private bool initialized;
    // レバーの論理状態（ON=開く側）
    private bool isOn;
    // oneShot 消費済みかどうか
    private bool consumed;

    private void Awake()
    {
        InitializeIfNeeded();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (leverRenderer == null)
        {
            leverRenderer = GetComponent<SpriteRenderer>();
        }
    }
#endif

    public void ActivateFromAttack()
    {
        InitializeIfNeeded();

        // oneShot 消費後は無効
        if (oneShot && consumed)
        {
            return;
        }

        if (shutterWall == null)
        {
            Debug.LogWarning("LeverSwitch2D に ShutterWallBlockRise が設定されていません", this);
            return;
        }

        // シャッター移動中は入力を受け付けない（連打対策）
        if (shutterWall.IsTransitioning)
        {
            return;
        }

        // 次の状態に応じて開閉を試み、失敗時はレバー状態を変えない
        bool nextOn = !isOn;
        bool started = nextOn ? shutterWall.TryOpen() : shutterWall.TryClose();
        if (!started)
        {
            return;
        }

        // 成功時のみ oneShot を消費
        if (oneShot)
        {
            consumed = true;
        }

        isOn = nextOn;

        // 見た目を論理状態に同期
        SyncVisualState();
    }

    public void OnAttacked(AttackHitbox attacker, Collider2D hitCollider)
    {
        ActivateFromAttack();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (leverRenderer == null)
        {
            leverRenderer = GetComponent<SpriteRenderer>();
        }

        if (closedSprite == null && leverRenderer != null)
        {
            closedSprite = leverRenderer.sprite;
        }

        // 未設定時は名前 "ShutterWall" のオブジェクトを自動探索
        if (shutterWall == null)
        {
            GameObject shutterWallObject = GameObject.Find("ShutterWall");
            if (shutterWallObject != null)
            {
                shutterWall = shutterWallObject.GetComponent<ShutterWallBlockRise>();
            }
        }

        // 起動時にレバー状態をシャッター実状態へ合わせる
        if (shutterWall != null)
        {
            isOn = shutterWall.IsOpen;
            if (oneShot && isOn)
            {
                consumed = true;
            }
        }

        // 初期見た目も同期
        SyncVisualState();
    }

    private void SyncVisualState()
    {
        if (leverRenderer == null)
        {
            return;
        }

        Sprite targetSprite = isOn ? openedSprite : closedSprite;
        if (targetSprite != null)
        {
            leverRenderer.sprite = targetSprite;
        }
    }
}
