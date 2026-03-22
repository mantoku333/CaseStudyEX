using UnityEngine;
using Cysharp.Threading.Tasks;

public class UmbrellaParryController : MonoBehaviour
{
    [Header("パリィ時間")]
    [SerializeField] private float parryDuration = 0.1f;

    [Header("当たり判定")]
    [SerializeField] private Collider2D parryCollider;

    private bool isParrying = false;

    private void Awake()
    {
        if (parryCollider != null)
        {
            parryCollider.enabled = false;
        }
    }

    /// <summary>
    /// パリィ時の処理を行う関数
    /// 
    /// </summary>
    /// <returns></returns>
    public async UniTaskVoid Parry()
    {
        if (isParrying)
        {
            return;
        }

        Debug.Log("パリィ開始");

        isParrying = true;

        if (parryCollider != null)
        {
            parryCollider.enabled = true;
        }

        await UniTask.Delay((int)(parryDuration * 1000));

        if (parryCollider != null)
        {
            parryCollider.enabled = false;
        }

        isParrying = false;

        Debug.Log("パリィ終了");
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
