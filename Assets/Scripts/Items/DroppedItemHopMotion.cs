using System.Collections;
using UnityEngine;

/// <summary>
/// ドロップされたアイテムを、開始位置から着地点まで弧を描くように跳ねさせる補助クラス。
/// ItemDropOnDeath から生成時に追加される想定。
/// </summary>
[DisallowMultipleComponent]
public sealed class DroppedItemHopMotion : MonoBehaviour
{
    private Coroutine hopRoutine;
    private ColliderState[] triggerColliderStates;

    /// <summary>
    /// その場で上下に跳ねる互換用メソッド。
    /// </summary>
    public void Play(Vector3 landingPosition, float hopHeight, float duration, bool disablePickupDuringHop)
    {
        Play(landingPosition, landingPosition, hopHeight, duration, disablePickupDuringHop);
    }

    /// <summary>
    /// 開始位置から着地点まで、山なりに移動させる。
    /// </summary>
    public void Play(
        Vector3 startPosition,
        Vector3 landingPosition,
        float hopHeight,
        float duration,
        bool disablePickupDuringHop)
    {
        if (hopRoutine != null)
        {
            // 既にホップ中なら一度止めて、無効化したColliderを元に戻してから再生し直す。
            StopCoroutine(hopRoutine);
            RestoreTriggerColliders();
        }

        hopRoutine = StartCoroutine(HopRoutine(
            startPosition,
            landingPosition,
            Mathf.Max(0f, hopHeight),
            Mathf.Max(0f, duration),
            disablePickupDuringHop));
    }

    private IEnumerator HopRoutine(
        Vector3 startPosition,
        Vector3 landingPosition,
        float hopHeight,
        float duration,
        bool disablePickupDuringHop)
    {
        if (disablePickupDuringHop)
        {
            // 空中でプレイヤーに触れて即取得されないよう、着地までTriggerを切る。
            DisableTriggerColliders();
        }

        transform.position = startPosition;

        if (duration <= 0f)
        {
            transform.position = landingPosition;
            RestoreTriggerColliders();
            hopRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 position = Vector3.Lerp(startPosition, landingPosition, t);
            // Sinを使って、始点と終点では高さ0、中央で最大高度になる弧を作る。
            float heightOffset = Mathf.Sin(t * Mathf.PI) * hopHeight;
            transform.position = position + Vector3.up * heightOffset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = landingPosition;
        RestoreTriggerColliders();
        hopRoutine = null;
    }

    private void DisableTriggerColliders()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        int triggerCount = 0;

        // 元の有効状態を正しく戻すため、Trigger Colliderだけを記録する。
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].isTrigger)
            {
                triggerCount++;
            }
        }

        triggerColliderStates = new ColliderState[triggerCount];
        int stateIndex = 0;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider2D = colliders[i];
            if (collider2D == null || !collider2D.isTrigger)
            {
                continue;
            }

            triggerColliderStates[stateIndex++] = new ColliderState(collider2D, collider2D.enabled);
            collider2D.enabled = false;
        }
    }

    private void RestoreTriggerColliders()
    {
        if (triggerColliderStates == null)
        {
            return;
        }

        // ホップ前に有効だったColliderだけを元の状態へ戻す。
        for (int i = 0; i < triggerColliderStates.Length; i++)
        {
            ColliderState state = triggerColliderStates[i];
            if (state.Collider != null)
            {
                state.Collider.enabled = state.WasEnabled;
            }
        }

        triggerColliderStates = null;
    }

    private void OnDisable()
    {
        if (hopRoutine != null)
        {
            StopCoroutine(hopRoutine);
            hopRoutine = null;
        }

        // オブジェクト無効化時にも、取得判定が無効のまま残らないように戻す。
        RestoreTriggerColliders();
    }

    /// <summary>
    /// ホップ前のCollider状態を保持するための小さな値型。
    /// </summary>
    private readonly struct ColliderState
    {
        public ColliderState(Collider2D collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider2D Collider { get; }
        public bool WasEnabled { get; }
    }
}
