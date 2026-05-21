using UnityEngine;

public class DiaryPickupItem : MonoBehaviour, ISaveDataModule
{
    //--------------日記データ関連------------------
    [SerializeField] private DiaryEntryData diaryEntryData;

    //--------------状態関連------------------
    private bool isPickedUp = false;

    public int Priority => 253;

    private void OnEnable()
    {
        SaveManager.RegisterModule(this);
    }

    private void OnDisable()
    {
        SaveManager.UnregisterModule(this);
    }

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

        CompletePickup();
    }

    private void CompletePickup()
    {
        isPickedUp = true;

        ItemEffectController effectController = GetComponent<ItemEffectController>();
        if (effectController != null && effectController.PlayPickupEffectAndDestroy())
        {
            return;
        }

        Destroy(gameObject);
    }

    public void Capture(SaveGameData saveData)
    {
    }

    public void Restore(SaveGameData saveData)
    {
        if (diaryEntryData == null)
        {
            return;
        }

        if (GameProgressFlags.Get(diaryEntryData.GetProgressFlagKey()))
        {
            Destroy(gameObject);
        }
    }
}
