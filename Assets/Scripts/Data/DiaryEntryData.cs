using UnityEngine;

[CreateAssetMenu(fileName = "DiaryEntryData", menuName = "Item/Diary Entry Data")]
public class DiaryEntryData : ScriptableObject
{
    //--------------日記情報関連------------------
    [Header("日記ID")]
    [SerializeField] private string entryId = "";  //ゲーム内でこの日記を区別するためのID
    [Header("日記タイトル")]
    [SerializeField] private string title = "";    //日記のタイトル
    [Header("日記本文")]
    [SerializeField, TextArea(3, 12)] private string content = "";    //日記パネルに表示する本文
    [Header("取得フラグキー")]
    [SerializeField] private string progressFlagKey = "";           //この日記を取得したかどうかを管理するためのフラグキー(GameProgressFlagsで使用)
    [Header("取得時イベント名")]
    [SerializeField] private string pickupEventName = "";           //この日記を取得した時に再生するイベント名

    //--------Get関数-------
    public string GetEntryId()
    {
        return entryId;
    }

    public string GetTitle()
    {
        return title;
    }

    public string GetContent()
    {
        return content;
    }

    public string GetProgressFlagKey()
    {
        return progressFlagKey;
    }

    public string GetPickupEventName()
    {
        return pickupEventName;
    }
}
