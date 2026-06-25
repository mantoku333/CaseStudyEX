using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// 敵やボスの死亡時に、指定したアイテムPrefabを生成してドロップさせるクラス。
    /// EnemyController / LastBossController が実装している IBossHealthSource の Died イベントを利用する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemDropOnDeath : MonoBehaviour
    {
        [Header("Drop Item")]
        [Tooltip("死亡時に生成するアイテムPrefab。Assets/Prefabs/Items などから設定する。")]
        [SerializeField] private GameObject itemPrefab;

        [Tooltip("ドロップ位置を固定したい場合に使う基準Transform。未設定なら自身のCollider中心を使う。")]
        [SerializeField] private Transform dropPoint;

        [Tooltip("基準位置からずらす量。アイテムを少し上や横に出したい時に調整する。")]
        [SerializeField] private Vector3 dropOffset;

        [Header("Hop")]
        [Tooltip("着地点を左右にランダムでずらす範囲。初期値は死亡位置のX-3からX+3。")]
        [SerializeField] private Vector2 randomHorizontalOffsetRange = new Vector2(-3f, 3f);

        [Tooltip("ドロップ時にアイテムが跳ねる高さ。1で約1ブロック分。")]
        [SerializeField, Min(0f)] private float hopHeight = 3f;

        [Tooltip("跳ねる演出にかかる時間。")]
        [SerializeField, Min(0f)] private float hopDuration = 0.35f;

        [Tooltip("跳ねている間は取得判定を無効にし、着地後に拾えるようにする。")]
        [SerializeField] private bool disablePickupDuringHop = true;

        private IBossHealthSource healthSource;
        private IBossHealthSource subscribedHealthSource;
        private Collider2D bodyCollider;
        private bool hasDropped;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            hopHeight = Mathf.Max(0f, hopHeight);
            hopDuration = Mathf.Max(0f, hopDuration);

            // Inspectorで範囲を逆に入れても、内部では小さい値から大きい値へ揃える。
            if (randomHorizontalOffsetRange.x > randomHorizontalOffsetRange.y)
            {
                randomHorizontalOffsetRange = new Vector2(
                    randomHorizontalOffsetRange.y,
                    randomHorizontalOffsetRange.x);
            }
        }
#endif

        private void CacheReferences()
        {
            if (healthSource == null)
            {
                // 同じGameObject上の死亡通知元を探す。通常敵とラスボスの両方に対応するため、共通Interfaceを見る。
                healthSource = FindHealthSourceOnThisObject();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }
        }

        private IBossHealthSource FindHealthSourceOnThisObject()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                // UnityのGetComponent<T>ではInterface検索が読みづらくなるため、MonoBehaviour一覧から判定する。
                if (behaviours[i] is IBossHealthSource source)
                {
                    return source;
                }
            }

            return null;
        }

        private void Subscribe()
        {
            if (healthSource == null || subscribedHealthSource == healthSource)
            {
                return;
            }

            // 死亡直前のイベントに登録し、Destroyされる前にアイテムを生成する。
            Unsubscribe();
            healthSource.Died += DropItem;
            subscribedHealthSource = healthSource;
        }

        private void Unsubscribe()
        {
            if (subscribedHealthSource == null)
            {
                return;
            }

            subscribedHealthSource.Died -= DropItem;
            subscribedHealthSource = null;
        }

        private void DropItem()
        {
            if (hasDropped)
            {
                return;
            }

            // Diedイベントが複数回呼ばれても、ドロップは1回だけにする。
            hasDropped = true;

            if (itemPrefab == null)
            {
                Debug.LogWarning($"{nameof(ItemDropOnDeath)} on {name} has no item prefab assigned.", this);
                return;
            }

            Vector3 spawnPosition = GetDropPosition();
            Vector3 landingPosition = GetRandomLandingPosition(spawnPosition);
            GameObject droppedItem = Instantiate(itemPrefab, spawnPosition, itemPrefab.transform.rotation);

            // アイテムPrefab側に事前追加していなくても、生成時にホップ演出を付与する。
            DroppedItemHopMotion hopMotion = droppedItem.GetComponent<DroppedItemHopMotion>();
            if (hopMotion == null)
            {
                hopMotion = droppedItem.AddComponent<DroppedItemHopMotion>();
            }

            hopMotion.Play(spawnPosition, landingPosition, hopHeight, hopDuration, disablePickupDuringHop);
        }

        private Vector3 GetDropPosition()
        {
            // 明示的なドロップ地点があれば最優先で使う。
            if (dropPoint != null)
            {
                return dropPoint.position + dropOffset;
            }

            // 未設定の場合は、敵やボスの見た目に近いCollider中心から出す。
            if (bodyCollider != null)
            {
                return bodyCollider.bounds.center + dropOffset;
            }

            return transform.position + dropOffset;
        }

        private Vector3 GetRandomLandingPosition(Vector3 spawnPosition)
        {
            // 死亡位置を基準に、X方向だけランダムな着地点へずらす。
            float minOffset = Mathf.Min(randomHorizontalOffsetRange.x, randomHorizontalOffsetRange.y);
            float maxOffset = Mathf.Max(randomHorizontalOffsetRange.x, randomHorizontalOffsetRange.y);
            float xOffset = Random.Range(minOffset, maxOffset);

            return spawnPosition + Vector3.right * xOffset;
        }
    }
}
