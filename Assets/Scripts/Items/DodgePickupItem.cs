using UnityEngine;
using Player;

public class DodgePickupItem : MonoBehaviour
{
    private bool isPickedUp = false;

    private void Reset()
    {
        Collider2D collider2D = GetComponent<Collider2D>();

        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPickedUp) { return; }

        PlayerAbilityController abilityController = other.GetComponent<PlayerAbilityController>();

        if (abilityController == null)
        {
            abilityController = other.GetComponentInParent<PlayerAbilityController>();

            if (abilityController == null) { return; }
        }

        //Playerに回避の能力を解放させる
        abilityController.SetCanDodge(true);

        Debug.Log("回避能力を取得しました！");

        //ゲット状態にする
        isPickedUp = true;

        //アイテム削除
        Destroy(gameObject);
    }
}
