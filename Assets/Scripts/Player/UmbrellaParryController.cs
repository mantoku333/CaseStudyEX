using UnityEngine;
using Cysharp.Threading.Tasks;

public class UmbrellaParryController : MonoBehaviour
{
    [Header("パリィ時間")]
    [SerializeField] private float parryDuration = 0.1f;   //パリィ状態が続く時間

    [Header("当たり判定")]
    [SerializeField] private Collider2D parryCollider;     //パリィの当たり判定用コライダー

    [SerializeField] private SpriteRenderer playerSprite;    //プレイヤーのスプライトレンダラー
    [SerializeField] private Color parryColor = Color.white; //パリィ中のフラッシュの色
    [SerializeField] private float flashDuration = 0.1f;     //フラッシュの持続時間

    [Header("SE")]
    [SerializeField] private AudioClip umbrella_open;    //パリィ時SE

    private Color defaultColor;       //プレイヤーのスプライトのデフォルトの色
    private bool isParrying = false;  //現在パリィ状態かどうかのフラグ

    private AudioSource audioSource;      //AudioSource

    private void Awake()
    {
        if (playerSprite != null)
        {
            defaultColor = playerSprite.color;
        }
        //AudioSourceの取得
        audioSource = GetComponentInParent<AudioSource>();
    }

    /// <summary>
    /// パリィ時の処理を行う関数
    /// </summary>
    /// <returns></returns>
    public async UniTaskVoid Parry()
    {
        if (isParrying)
        {
            return;
        }

        //傘開けるSE再生
        PlaySE(umbrella_open);

        isParrying = true;

        if (parryCollider != null)
        {
            parryCollider.enabled = true;
        }

        //パリィ成功のフラッシュエフェクト
        FlashEffect().Forget();

        //パリィ状態が続く時間待機
        await UniTask.Delay((int)(parryDuration * 1000));

        isParrying = false;
    }

    /// <summary>
    /// パリィが出来たらプレイヤーが光るエフェクトを行う関数
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// SE再生用関数
    /// </summary>
    private void PlaySE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
