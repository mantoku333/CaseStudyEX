using UnityEngine;
using Cysharp.Threading.Tasks;

public class UmbrellaAttackController : MonoBehaviour
{
    [Header("攻撃設定")]
    [SerializeField] private float attackDuration = 0.2f;   //攻撃の当たり判定が有効な時間

    [Header("当たり判定")]
    [SerializeField] private Collider2D attackCollider;     //攻撃の当たり判定用コライダー

    [Header("SE")]
    [SerializeField] private AudioClip player_normalAttack;    //攻撃時SE

    private bool isAttacking = false;   //攻撃中かどうかのフラグ

    private AudioSource audioSource;    //AudioSource

    private void Awake()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
        audioSource = GetComponentInParent<AudioSource>();
    }

    /// <summary>
    /// 傘での攻撃処理を行う関数
    /// </summary>
    /// <returns></returns>
    public async UniTaskVoid Attack()
    {
        if (isAttacking)
        {
            return;
        }

        isAttacking = true;

        //通常攻撃音再生
        PlaySE(player_normalAttack);

        //当たり判定ON
        attackCollider.enabled = true;

        //数秒待つ
        await UniTask.Delay((int)(attackDuration * 1000));

        //当たり判定OFF
        attackCollider.enabled = false;
        
        isAttacking = false;
    }

    /// <summary>
    /// SE再生用関数
    /// </summary>
    private void PlaySE(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
        Debug.Log("atk played");
    }
}
