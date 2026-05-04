using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
// Playerが上から踏んだ時だけ、本棚ギミックを一度だけ順番に動かすスイッチ。
public class MovingBookshelfPressureSwitch : MonoBehaviour
{
    // 接触点がCollider上端から少し下でも「踏んだ」と扱うための許容値。
    private const float TopContactTolerance = 0.08f;

    [Header("Targets")]
    // 同じMovingBookshelf Prefab内のBookshelf。未設定なら名前で自動探索する。
    [SerializeField] private Transform bookshelf;

    [Header("Trigger")]
    // 敵はUntaggedなので、Playerタグだけを起動対象にする。
    [SerializeField] private string playerTag = "Player";
    // trueなら一度起動した後は再起動しない。
    [SerializeField] private bool oneShot = true;

    [Header("Motion")]
    // Switchが下がる時間と距離。
    [SerializeField, Min(0.01f)] private float switchPressDuration = 1f;
    [SerializeField, Min(0f)] private float switchPressDistance = 1f;
    // Switchが下がりきった後、Bookshelfが右へ動く時間と距離。
    [SerializeField, Min(0.01f)] private float bookshelfMoveDuration = 4f;
    [SerializeField] private float bookshelfMoveDistance = 2f;

    private Collider2D switchCollider;
    // 初期位置からの相対移動にするため、起動前のローカル座標を保持する。
    private Vector3 initialSwitchLocalPosition;
    private Vector3 initialBookshelfLocalPosition;
    private Coroutine sequenceRoutine;
    private bool initialized;
    private bool consumed;

    private void Awake()
    {
        InitializeIfNeeded();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bookshelf == null)
        {
            bookshelf = FindSibling("Bookshelf");
        }
    }
#endif

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryStartSequence(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryStartSequence(collision);
    }

    private void TryStartSequence(Collision2D collision)
    {
        InitializeIfNeeded();

        // 移動中の多重起動と、oneShot消費後の再起動を防ぐ。
        if ((oneShot && consumed) || sequenceRoutine != null)
        {
            return;
        }

        // Player以外、または横から触れただけの接触では起動しない。
        if (!IsPlayerCollision(collision) || !IsTopContact(collision))
        {
            return;
        }

        if (bookshelf == null)
        {
            Debug.LogWarning("MovingBookshelfPressureSwitch requires a Bookshelf Transform.", this);
            return;
        }

        if (oneShot)
        {
            consumed = true;
        }

        sequenceRoutine = StartCoroutine(PressAndMoveRoutine());
    }

    private IEnumerator PressAndMoveRoutine()
    {
        // 先にスイッチを押し込み、その完了後に本棚を動かす。
        Vector3 switchTarget = initialSwitchLocalPosition + Vector3.down * switchPressDistance;
        yield return MoveLocalPosition(transform, transform.localPosition, switchTarget, switchPressDuration);

        Vector3 bookshelfTarget = initialBookshelfLocalPosition + Vector3.right * bookshelfMoveDistance;
        yield return MoveLocalPosition(bookshelf, bookshelf.localPosition, bookshelfTarget, bookshelfMoveDuration);

        sequenceRoutine = null;
    }

    private IEnumerator MoveLocalPosition(Transform target, Vector3 start, Vector3 end, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            // 既存のシャッター壁と同じくSmoothStepで急な動きを抑える。
            float eased = Mathf.SmoothStep(0f, 1f, t);
            target.localPosition = Vector3.LerpUnclamped(start, end, eased);

            yield return null;
        }

        target.localPosition = end;
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        switchCollider = GetComponent<Collider2D>();

        // Prefab内で参照が外れても、同階層のBookshelfを自動で拾う。
        if (bookshelf == null)
        {
            bookshelf = FindSibling("Bookshelf");
        }

        initialSwitchLocalPosition = transform.localPosition;
        if (bookshelf != null)
        {
            initialBookshelfLocalPosition = bookshelf.localPosition;
        }
    }

    private bool IsPlayerCollision(Collision2D collision)
    {
        if (string.IsNullOrEmpty(playerTag))
        {
            return false;
        }

        return IsTaggedPlayer(collision.gameObject)
            || IsTaggedPlayer(collision.collider)
            || IsTaggedPlayer(collision.otherCollider);
    }

    private bool IsTaggedPlayer(Collider2D targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        if (IsTaggedPlayer(targetCollider.gameObject))
        {
            return true;
        }

        // 子Colliderに当たった場合でも、Rigidbody本体のタグで判定できるようにする。
        Rigidbody2D attachedRigidbody = targetCollider.attachedRigidbody;
        return attachedRigidbody != null && IsTaggedPlayer(attachedRigidbody.gameObject);
    }

    private bool IsTaggedPlayer(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return false;
        }

        Transform targetTransform = targetObject.transform;
        // 子オブジェクト経由の接触にも対応するため、rootのタグも確認する。
        return targetObject.CompareTag(playerTag)
            || (targetTransform.parent != null && targetTransform.root.CompareTag(playerTag));
    }

    private bool IsTopContact(Collision2D collision)
    {
        if (switchCollider == null)
        {
            return false;
        }

        float topY = switchCollider.bounds.max.y;
        int contactCount = collision.contactCount;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            // 接触点がスイッチ上面付近にある場合だけ「踏んだ」と判定する。
            if (contact.point.y >= topY - TopContactTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private Transform FindSibling(string objectName)
    {
        Transform parent = transform.parent;
        return parent != null ? parent.Find(objectName) : null;
    }
}
