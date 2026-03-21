using UnityEngine;
using Cysharp.Threading.Tasks;

public class UmbrellaAttackController : MonoBehaviour
{
    [Header("攻撃設定")]
    [SerializeField] private float attackDuration = 0.2f;

    [Header("当たり判定")]
    [SerializeField] private Collider2D attackCollider;

    private bool isAttacking = false;

    private void Awake()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
        }
    }

    public async UniTaskVoid Attack()
    {
        Debug.Log("攻撃！");

        if (isAttacking){ return; }

        isAttacking = true;

        //当たり判定ON
        attackCollider.enabled = true;

        //数秒待つ
        await UniTask.Delay((int)(attackDuration * 1000));

        //当たり判定OFF
        attackCollider.enabled = false;
        
        isAttacking = false;

        Debug.Log("攻撃終わり！");
    }
}
