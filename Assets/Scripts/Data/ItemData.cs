using UnityEngine;

namespace Metroidvania.Data
{
    /// <summary>
    /// アイテムのデータを定義するScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData", menuName = "Metroidvania/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("基本情報")]
        [Tooltip("アイテム名")]
        public string itemName;

        [Tooltip("ID")]
        public string itemId;

        [Tooltip("アイテムの説明")]
        [TextArea(3, 5)]
        public string description;
        
        [Tooltip("アイテムアイコン")]
        public Sprite icon;
        
        [Header("アイテムタイプ")]
        public ItemType itemType;

        [Header("効果")]
        [Tooltip("回復量（回復アイテムの場合）")]
        public int healAmount;
        [Tooltip("スタミナ回復量")]
        public int staminaAmount;
        [Tooltip("HP増加量（HP増加アイテムの場合）")]
        public int maxHealthBonus;
        [Tooltip("攻撃力増加量（攻撃力増加アイテムの場合）")]
        public int attackhBonus;

        [Tooltip("解放する能力")]
        public PlayerAbilityType abilityType = PlayerAbilityType.None;

        [Header("その他")]
        [Tooltip("スタック可能か")]
        public bool stackable = true;
        
        [Tooltip("最大スタック数")]
        public int maxStackSize = 99;
        
        [Tooltip("取得時の効果音")]
        public AudioClip pickupSound;
    }

    /// <summary>
    /// アイテムの分類を定義
    /// </summary>
    public enum ItemType
    {
        Consumable,    // 消費アイテム
        KeyItem,       // 重要アイテム
        Equipment,     // 装備
        Collectible    // 収集品
    }
}
