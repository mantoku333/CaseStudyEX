using UnityEngine;
using Cysharp.Threading.Tasks;

public class UmbrellaParryController : MonoBehaviour
{
    [Header("パリィ時間")]
    [SerializeField] private float parryDuration = 0.1f;

    [Header("当たり判定")]
    [SerializeField] private Collider2D parryCollider;

    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private Color parryColor = Color.white;
    [SerializeField] private float flashDuration = 0.1f;

    private Color defaultColor;

    private bool isParrying = false;

    private void Awake()
    {
        if (playerSprite != null)
        {
            defaultColor = playerSprite.color;
        }
    }

    /// <summary>
    /// パリィ時の処理を行う関数
    /// </summary>
    /// <returns></returns>
    public async UniTaskVoid Parry()
    {
        if (isParrying){ return; }

        Debug.Log("パリィ開始");

        isParrying = true;

        if (parryCollider != null)
        {
            parryCollider.enabled = true;
        }

        FlashEffect().Forget();

        await UniTask.Delay((int)(parryDuration * 1000));

        if (parryCollider != null)
        {
            parryCollider.enabled = false;
        }

        isParrying = false;

        Debug.Log("パリィ終了");
    }

    private async UniTask FlashEffect()
    {
        if (playerSprite == null)
        {
            return;
        }

        playerSprite.color = parryColor;

        await UniTask.Delay((int)(flashDuration * 1000));

        playerSprite.color = defaultColor;
    }

    /// <summary>
    /// パリィ状態かどうかを返す関数
    /// </summary>
    /// <returns>今パリィしているかどうかのbool型</returns>
    public bool IsParrying()
    {
        return isParrying;
    }
}
