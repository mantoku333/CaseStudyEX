using UnityEngine;
using Cysharp.Threading.Tasks;

public class ParryHitbox : MonoBehaviour
{
    [Header("ヒットストップ時間")]
    [SerializeField] private float hitStopTime = 0.05f;

    private async void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("EnemyBullet"))
        {
            Debug.Log("パリィ成功！");

            Rigidbody2D rigidBody2D = collision.GetComponent<Rigidbody2D>();

            if (rigidBody2D != null)
            {
                //プレイヤーから見た方向に反射
                Vector2 direction = (collision.transform.position - transform.position).normalized;
                float speed = rigidBody2D.linearVelocity.magnitude;

                rigidBody2D.linearVelocity = direction * speed;
            }

            //ヒットストップ
            Time.timeScale = 0f;

            await UniTask.Delay((int)(hitStopTime * 1000), ignoreTimeScale: true);

            Time.timeScale = 1f;
        }
    }
}
