using UnityEngine;
using Cysharp.Threading.Tasks;

public class DodgeController : MonoBehaviour
{
    [Header("回避距離")]
    [SerializeField] private float dodgeDistance = 3.0f;   //回避する距離

    [Header("回避時間")]
    [SerializeField] private float dodgeDuration = 0.1f;　 //回避にかかる時間

    private bool isDodging = false;   //回避中かどうかのフラグ
    private Rigidbody2D rigidBody2d;  //Rigidbody2Dコンポーネント

    private void Awake()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// プレイヤーの回避動作を実行する関数
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    public async UniTaskVoid Dodge(Vector2 direction)
    {
        if (isDodging){ return; }

        if (direction == Vector2.zero){ return; }

        if (rigidBody2d == null) { return; }

        isDodging = true;

        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.x = 0.0f;
        rigidBody2d.linearVelocity = velocity;

        //回避の開始位置と目標位置を計算
        Vector2 startPos = rigidBody2d.position;
        Vector2 targetPos = startPos + direction.normalized * dodgeDistance;

        float elapsedTime = 0.0f;

        //回避動作を時間で補間して実行
        while (elapsedTime < dodgeDuration)
        {
            float t = elapsedTime / dodgeDuration;

            Vector2 newPos = Vector2.Lerp(startPos, targetPos, t);
            rigidBody2d.MovePosition(newPos);

            elapsedTime += Time.deltaTime;

            await UniTask.Yield();
        }

        //最終的に目標位置に移動
        rigidBody2d.MovePosition(targetPos);

        isDodging = false;
    }

    /// <summary>
    /// 今回避中かどうかを返す関数
    /// </summary>
    /// <returns></returns>
    public bool IsDodging()
    {
        return isDodging;
    }
}
