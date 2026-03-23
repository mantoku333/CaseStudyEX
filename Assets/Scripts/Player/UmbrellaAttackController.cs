using UnityEngine;
using Cysharp.Threading.Tasks;

public class UmbrellaAttackController : MonoBehaviour
{
    [Header("攻撃設定")]
    [SerializeField] private float attackDuration = 0.2f;   //攻撃の当たり判定が有効な時間

    [Header("当たり判定")]
    [SerializeField] private Collider2D attackCollider;     //攻撃の当たり判定用コライダー

    private bool isAttacking = false;   //攻撃中かどうかのフラグ

    private void Awake()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
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

        //当たり判定ON
        attackCollider.enabled = true;

        //数秒待つ
        await UniTask.Delay((int)(attackDuration * 1000));

        //当たり判定OFF
        attackCollider.enabled = false;
        
        isAttacking = false;
    }
}
