using UnityEngine;

public class DiaryPickupItem : MonoBehaviour
{
    //--------------日記データ関連------------------
    [SerializeField] private DiaryEntryData diaryEntryData;

    //--------------状態関連------------------
    private bool isPickedUp = false;

    private void Start()
    {
        if (diaryEntryData == null)
        {
            Debug.LogError("DiaryEntryDataが設定されていません", this);
            return;
        }

        string flagKey = diaryEntryData.GetProgressFlagKey();

        if (GameProgressFlags.Get(flagKey))
        {
            // すでに取得済みなら消す
            Destroy(gameObject);
        }
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPickedUp){ return; }

        if (diaryEntryData == null){ return; }

        if (!other.CompareTag("Player")){ return; }

        string flagKey = diaryEntryData.GetProgressFlagKey();

        // すでに取得済みなら何もしない
        if (GameProgressFlags.Get(flagKey)){ return; }

        //フラグ立てる
        GameProgressFlags.Set(flagKey, true);

        Debug.Log($"日記取得: {diaryEntryData.GetTitle()}");

        isPickedUp = true;

        Destroy(gameObject);
    }
}
