using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一方向床(PlatformEffector2D)を一定時間だけすり抜ける制御
/// 入力判定は PlayerController 側で行い、このクラスは物理的な無視/復帰のみ担当する
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FallThroughController : MonoBehaviour
{
    [Header("判定対象")]
    // 検索候補のレイヤー最終的には IsOneWayPlatform で Effector 有無を確認する
    [SerializeField] private LayerMask oneWayPlatformLayerMask = ~0;
    // 足元チェックの高さ薄すぎると取りこぼし、厚すぎると隣接床を拾いやすい
    [SerializeField, Min(0.02f)] private float feetCheckHeight = 0.08f;

    [Header("すり抜け設定")]
    // 最低でもこの時間は衝突を無視する
    [SerializeField, Min(0.01f)] private float minIgnoreDuration = 0.12f;
    // 何らかの理由で分離判定できない場合の保険タイムアウト
    [SerializeField, Min(0.05f)] private float maxIgnoreDuration = 0.6f;
    // プレイヤー上端が床下端よりどれだけ下に来たら復帰可能とみなすか
    [SerializeField, Min(0f)] private float restoreBelowMargin = 0.05f;
    // すり抜け開始直後に下向き速度を与えて、即時再接触を減らす
    [SerializeField, Min(0f)] private float forceDownSpeed = 2.0f;

    // 現在 IgnoreCollision している床のみ保持し、確実に復帰する
    private readonly List<Collider2D> ignoredPlatforms = new List<Collider2D>();
    // 毎フレームのGCを避けるための再利用バッファ
    private readonly Collider2D[] overlapBuffer = new Collider2D[8];

    private Collider2D playerCollider;
    private Rigidbody2D rigidBody2d;
    private Coroutine fallRoutine;

    private void Awake()
    {
        playerCollider = GetComponent<Collider2D>();
        rigidBody2d = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// すり抜け要求の入口
    /// 条件を満たした場合のみコルーチンを開始する
    /// </summary>
    public void TryFallThrough()
    {
        // 多重実行防止
        if (fallRoutine != null)
        {
            return;
        }

        if (playerCollider == null)
        {
            return;
        }

        CollectPlatformsAtFeet();
        if (ignoredPlatforms.Count == 0)
        {
            return;
        }

        fallRoutine = StartCoroutine(FallThroughRoutine());
    }

    /// <summary>
    /// プレイヤー足元の一方向床を収集し、今回無視する対象リストを作る
    /// </summary>
    private void CollectPlatformsAtFeet()
    {
        ignoredPlatforms.Clear();

        Bounds bounds = playerCollider.bounds;
        Vector2 checkCenter = new Vector2(bounds.center.x, bounds.min.y - (feetCheckHeight * 0.5f));
        Vector2 checkSize = new Vector2(Mathf.Max(0.05f, bounds.size.x * 0.95f), feetCheckHeight);

        ContactFilter2D overlapFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = oneWayPlatformLayerMask,
            useTriggers = false
        };
        int hitCount = Physics2D.OverlapBox(checkCenter, checkSize, 0f, overlapFilter, overlapBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = overlapBuffer[i];
            overlapBuffer[i] = null;

            if (candidate == null || candidate == playerCollider)
            {
                continue;
            }

            if (!IsOneWayPlatform(candidate))
            {
                continue;
            }

            if (ignoredPlatforms.Contains(candidate))
            {
                continue;
            }

            ignoredPlatforms.Add(candidate);
        }

        for (int i = hitCount; i < overlapBuffer.Length; i++)
        {
            overlapBuffer[i] = null;
        }
    }

    private static bool IsOneWayPlatform(Collider2D collider2d)
    {
        if (collider2d.usedByEffector)
        {
            return true;
        }

        return collider2d.GetComponent<PlatformEffector2D>() != null;
    }

    /// <summary>
    /// 衝突無視の開始〜復帰までを管理する
    /// </summary>
    private IEnumerator FallThroughRoutine()
    {
        for (int i = 0; i < ignoredPlatforms.Count; i++)
        {
            Collider2D platform = ignoredPlatforms[i];
            if (platform != null)
            {
                Physics2D.IgnoreCollision(playerCollider, platform, true);
            }
        }

        if (rigidBody2d != null)
        {
            Vector2 velocity = rigidBody2d.linearVelocity;
            if (velocity.y > -forceDownSpeed)
            {
                velocity.y = -forceDownSpeed;
                rigidBody2d.linearVelocity = velocity;
            }
        }

        float elapsed = 0f;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();

        // 最低時間は必ず待ち、その後は「十分に床の下に出たら」復帰する
        while (elapsed < maxIgnoreDuration)
        {
            elapsed += Time.fixedDeltaTime;

            bool minTimeReached = elapsed >= minIgnoreDuration;
            if (minTimeReached && IsBelowIgnoredPlatforms())
            {
                break;
            }

            yield return wait;
        }

        RestoreIgnoredCollisions();
        fallRoutine = null;
    }

    /// <summary>
    /// すり抜け対象の床すべてに対して、プレイヤー上端が床下端より下に出たかを判定する
    /// </summary>
    private bool IsBelowIgnoredPlatforms()
    {
        if (playerCollider == null)
        {
            return true;
        }

        float playerTop = playerCollider.bounds.max.y;
        for (int i = 0; i < ignoredPlatforms.Count; i++)
        {
            Collider2D platform = ignoredPlatforms[i];
            if (platform == null)
            {
                continue;
            }

            float platformBottom = platform.bounds.min.y;
            if (playerTop > platformBottom - restoreBelowMargin)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 今回無視した床との衝突を元に戻す
    /// </summary>
    private void RestoreIgnoredCollisions()
    {
        if (playerCollider != null)
        {
            for (int i = 0; i < ignoredPlatforms.Count; i++)
            {
                Collider2D platform = ignoredPlatforms[i];
                if (platform != null)
                {
                    Physics2D.IgnoreCollision(playerCollider, platform, false);
                }
            }
        }

        ignoredPlatforms.Clear();
    }

    private void OnDisable()
    {
        if (fallRoutine != null)
        {
            StopCoroutine(fallRoutine);
            fallRoutine = null;
        }

        RestoreIgnoredCollisions();
    }
}
