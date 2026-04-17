using Player;
using UnityEngine;

public class HitPointHealItem : MonoBehaviour
{
    [Header("回復量")]
    [SerializeField] private int healAmount = 2;

    private bool isPickedUp = false;
    private void Reset()
    {
        BoxCollider2D collider2D = GetComponent<BoxCollider2D>();

        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(isPickedUp) { return; }

        //PlayerHealthコンポーネントを取得
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

        //PlayerHealthコンポーネントが取得できない場合、親オブジェクトから取得を試みる
        if (playerHealth == null)
        {
            playerHealth = other.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth == null){ return; }

        //HP回復
        playerHealth.Heal(healAmount);

        Debug.Log($"Playerが回復しました　HP: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");

        //取得判定をtrueにする
        isPickedUp = true;

        //アイテム削除
        Destroy(gameObject);
    }
}
