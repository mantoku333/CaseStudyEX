using System.Collections.Generic;
using GameName.Enemy;
using Metroidvania.Enemy;
using UnityEngine;
using Player;

public class ParryHitbox : MonoBehaviour
{
    // パリィ判定に接触している敵攻撃を保持する。
    // 通常弾はEnemyBullet、LastBossの範囲攻撃はLastBossAttackParryTargetで判別する。
    private List<GameObject> enemyAttacks = new List<GameObject>();     //接触管理
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private Collider2D hitboxCollider;
    private ContactFilter2D overlapFilter;

    //--------------パリィ関連------------------
    private UmbrellaParryController umbrellaParryController;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        overlapFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        overlapFilter.SetLayerMask(Physics2D.AllLayers);

        umbrellaParryController = GetComponentInParent<UmbrellaParryController>();
    }

    public void ScanCurrentOverlaps()
    {
        if (hitboxCollider == null || !hitboxCollider.enabled)
        {
            return;
        }

        Physics2D.SyncTransforms();
        int overlapCount = hitboxCollider.Overlap(overlapFilter, overlapResults);
        for (int i = 0; i < overlapCount; i++)
        {
            AddEnemyAttackIfNeeded(overlapResults[i]);
            overlapResults[i] = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        AddEnemyAttackIfNeeded(collision);

        EnemyBullet enemyBullet = collision.GetComponent<EnemyBullet>();

        if (enemyBullet != null)
        {
            if (enemyBullet.IsReflectedByPlayer)
            {
                Debug.Log("反射弾なのでパリィ対象外です");
                return;
            }

            if (umbrellaParryController == null) { return; }

            if (!umbrellaParryController.IsParrying()) { return; }

            Debug.Log("弾を通常パリィしました");

            enemyBullet.DestroyByParry();
            HitStopController.RequestParry();
            return;
        }

        if (TryGetParryableAttack(collision, out IParryableAttack parryableAttack))
        {
            if (umbrellaParryController == null) { return; }

            if (!umbrellaParryController.IsParrying()) { return; }

            if (!parryableAttack.IsParryable)
            {
                Debug.Log("敵攻撃はパリィ可能状態ではありません");
                return;
            }

            Debug.Log("突進攻撃をパリィしました");

            parryableAttack.StopByParry();
            HitStopController.RequestParry();
        }
    }

    private void AddEnemyAttackIfNeeded(Collider2D collision)
    {
        if (collision == null || !IsEnemyAttack(collision))
        {
            return;
        }

        if (!enemyAttacks.Contains(collision.gameObject))
        {
            enemyAttacks.Add(collision.gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (IsEnemyAttack(collision))
        {
            if (enemyAttacks.Contains(collision.gameObject))
            {
                enemyAttacks.Remove(collision.gameObject);
            }
        }
    }

    /// <summary>
    /// 敵の攻撃に接触しているかを返す
    /// </summary>
    /// <returns></returns>
    public bool HasEnemyAttack()
    {
        ScanCurrentOverlaps();

        //無効をオブジェクト削除
        for (int i = enemyAttacks.Count - 1; i >= 0; i--)
        {
            if (!IsTrackedAttackActive(enemyAttacks[i]))
            {
                enemyAttacks.RemoveAt(i);
            }
        }

        if (enemyAttacks.Count > 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 取得した敵の攻撃のリストを返す
    /// </summary>
    /// <returns></returns>
    public List<GameObject> GetEnemyAttacks()
    {
        return enemyAttacks;
    }

    /// <summary>
    /// 敵の攻撃のリストをリセットする
    /// </summary>
    public void ClearEnemyAttacks()
    {
        // LastBossの範囲攻撃予兆は同じオブジェクトが数秒残るため、
        // 連打中も再パリィできるよう、通常弾だけリストから外す。
        for (int i = enemyAttacks.Count - 1; i >= 0; i--)
        {
            GameObject attackObject = enemyAttacks[i];
            if (attackObject == null) { continue; }

            EnemyBullet enemyBullet = attackObject.GetComponent<EnemyBullet>();
            if (enemyBullet != null)
            {
                enemyBullet.DestroyByParry();
                continue;
            }

            if (attackObject.GetComponent<LastBossAttackParryTarget>() == null) { continue; }
        }

        enemyAttacks.Clear();
    }

    private static bool IsEnemyAttack(Collider2D collision)
    {
        if (collision == null)
        {
            return false;
        }

        EnemyBullet enemyBullet = collision.GetComponent<EnemyBullet>();

        if (enemyBullet != null)
        {
            if (enemyBullet.IsReflectedByPlayer)
            {
                return false;
            }

            return true;
        }

        return TryGetParryableAttack(collision, out _) ||
       collision.GetComponent<LastBossAttackParryTarget>() != null;
    }

    private bool IsTrackedAttackActive(GameObject attackObject)
    {
        if (attackObject == null || !attackObject.activeInHierarchy)
        {
            return false;
        }

        if (hitboxCollider == null || !hitboxCollider.enabled)
        {
            return false;
        }

        Collider2D attackCollider = attackObject.GetComponent<Collider2D>();
        if (attackCollider == null || !attackCollider.enabled)
        {
            return false;
        }

        return hitboxCollider.Distance(attackCollider).isOverlapped;
    }

    /// <summary>
    /// 敵の弾をパリィできるか試みる。
    ///成功した場合は通常弾を消すか反射させる。
    ///ジャストパリィなら反射、そうでなければ消す。
    ///LastBossの範囲攻撃予兆は同じオブジェクトが数秒残るため、連打中も再パリィできるよう、通常弾だけリストから外す。
    /// </summary>
    /// <returns></returns>
    public bool TryParryEnemyBullets()
    {
        ScanCurrentOverlaps();

        bool parried = false;
        List<GameObject> parriedAttacks = new List<GameObject>();

        for (int i = enemyAttacks.Count - 1; i >= 0; i--)
        {
            if (i < 0 || i >= enemyAttacks.Count)
            {
                continue;
            }

            GameObject attackObject = enemyAttacks[i];

            if (attackObject == null)
            {
                parriedAttacks.Add(attackObject);
                continue;
            }

            EnemyBullet enemyBullet = attackObject.GetComponent<EnemyBullet>();

            if (enemyBullet == null) { continue; }

            if (enemyBullet.IsReflectedByPlayer) { continue; }

            if (enemyBullet.CanJustParry())
            {
                Debug.Log("ジャストパリィです");
                enemyBullet.ReflectByJustParry(transform.position);
                parriedAttacks.Add(attackObject);
                parried = true;
                continue;
            }

            Debug.Log("通常パリィです");
            enemyBullet.DestroyByParry();
            parriedAttacks.Add(attackObject);
            parried = true;
        }

        for (int i = 0; i < parriedAttacks.Count; i++)
        {
            enemyAttacks.Remove(parriedAttacks[i]);
        }

        if (parried)
        {
            HitStopController.RequestParry();
        }

        return parried;
    }


    /// <summary>
    /// 突進攻撃をパリィできるか試みる。
    /// 成功した場合は敵の突進を止める。
    /// </summary>
    /// <returns></returns>
    public bool TryParryEnemyAttack()
    {
        if (hitboxCollider == null || !hitboxCollider.enabled)
        {
            return false;
        }

        Physics2D.SyncTransforms();

        int overlapCount = hitboxCollider.Overlap(overlapFilter, overlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D hitCollider = overlapResults[i];
            overlapResults[i] = null;

            if (hitCollider == null)
            {
                continue;
            }

            if (!TryGetParryableAttack(hitCollider, out IParryableAttack parryableAttack))
            {
                continue;
            }

            if (!parryableAttack.IsParryable)
            {
                Debug.Log("パリィ可能状態ではない攻撃なのでパリィしません");
                continue;
            }

            parryableAttack.StopByParry();
            HitStopController.RequestParry();
            return true;
        }

        return false;
    }

    private static bool TryGetParryableAttack(
    Collider2D collision,
    out IParryableAttack parryableAttack)
    {
        parryableAttack = null;

        if (collision == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours =
            collision.GetComponentsInParent<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IParryableAttack attack)
            {
                parryableAttack = attack;
                return true;
            }
        }

        return false;
    }

}
