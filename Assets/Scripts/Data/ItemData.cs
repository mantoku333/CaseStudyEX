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
        
        [Tooltip("アイテムアイコン（スロット内の小さい画像）")]
        public Sprite icon;

        [Tooltip("右パネルに表示する大きいイラスト")]
        public Sprite illustration;

        [Header("アイテムタイプ")]
        public ItemType itemType;

        [Header("効果")]
        [Tooltip("回復量（回復アイテムの場合）")]
        public int healAmount;
        [Tooltip("スタミナ回復量")]
        public int staminaAmount;
        [Tooltip("増加量（HP増加アイテムの場合）")]
        public int maxHealthBonus;

        [Tooltip("解放する能力")]
        public PlayerAbilityType abilityType = PlayerAbilityType.None;

        [Header("その他")]
        [Tooltip("スタック可能か")]
        public bool stackable = true;
        
        [Tooltip("最大スタック数")]
        public int maxStackSize = 99;
        
        [Tooltip("取得時の効果音")]
        public AudioClip pickupSound;

        [Header("装備")]
        [Tooltip("スロットに表示するバッジ画像（アイテムごとに異なる説明バッジ）")]
        public Sprite equipmentBadge;

        [Tooltip("装備アイテムとして使う場合の効果一覧")]
        public EquipmentAbilityData[] equipmentAbility;

        [Tooltip("装備時にPlayerへ表示する画像レイヤー一覧")]
        public EquipmentVisualLayerData[] equipmentVisualLayers;
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

    public enum EquipmentAbilityType
    {
        AttackPowerMultiplier,  //攻撃力倍率
        HealPercentOnEnemyKill, //敵撃破時HP回復
        GunRecoilForceBonus     //銃反動量をマス単位で追加
    }

    public enum EquipmentVisualParent
    {
        Back,
        Front
    }

    [System.Serializable]

    ///装備時の能力効果を示すデータ
    public sealed class EquipmentAbilityData
    {
        [Tooltip("装備効果の種類")]
        public EquipmentAbilityType abilityType;

        [Tooltip("効果値。攻撃力5%アップなら1.05、撃破時HP2%回復なら0.02、銃反動+1マスなら1")]
        public float value = 1f;
    }


    //装備時に表示する画像情報を示すデータ
    //画像によってサイズや配置を決められるようにする
    [System.Serializable]
    public sealed class EquipmentVisualLayerData
    {
        [Tooltip("管理用の名前。空でも動作します")]
        public string layerName;

        [Tooltip("装備時に表示するSprite")]
        public Sprite sprite;

        [Tooltip("Playerの後ろ/前どちらの子オブジェクトへ置くか")]
        public EquipmentVisualParent parent = EquipmentVisualParent.Back;

        [Tooltip("親から見た位置")]
        public Vector3 localPosition;

        [Tooltip("左向き時だけ別の位置を使う")]
        public bool useLeftFacingLocalPosition;

        [Tooltip("左向き時の親から見た位置")]
        public Vector3 leftFacingLocalPosition;

        [Tooltip("親から見た回転")]
        public Vector3 localEulerAngles;

        [Tooltip("親から見た拡大率")]
        public Vector3 localScale = Vector3.one;

        [Tooltip("PlayerのSpriteRendererを基準にした描画順の差。後ろ側は-1、前側は+1から調整する想定")]
        public int sortingOrderOffset;
    }
}
