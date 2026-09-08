using UnityEngine;

using NaughtyAttributes;

namespace Metroidvania.Data
{
    /// <summary>
    /// アイテムのデータを定義するScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData", menuName = "Metroidvania/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("基本情報")]
        [Label("アイテム名")]
        [Tooltip("アイテム名")]
        public string itemName;

        [Label("アイテムID")]
        [Tooltip("ID")]
        public string itemId;

        [Label("説明文")]
        [Tooltip("アイテムの説明")]
        [TextArea(3, 5)]
        public string description;
        
        [Label("アイコン")]
        [Tooltip("アイテムアイコン（スロット内の小さい画像）")]
        public Sprite icon;

        [Label("イラスト")]
        [Tooltip("右パネルに表示する大きいイラスト")]
        public Sprite illustration;

        [Header("アイテムタイプ")]
        [Label("アイテムタイプ")]
        public ItemType itemType;

        [Header("デコレーション購入")]
        [Label("エレガントポイント価格")]
        [Tooltip("0以下のアイテムはデコレーションショップで購入できません")]
        [Min(0)]
        public int elegantPointCost;

        [Header("効果")]
        [Label("HP回復量")]
        [Tooltip("回復量（回復アイテムの場合）")]
        public int healAmount;
        //[Label("スタミナ回復量")]
        //[Tooltip("スタミナ回復量")]
        //public int staminaAmount;
        [Label("最大HP増加量")]
        [Tooltip("増加量（HP増加アイテムの場合）")]
        public int maxHealthBonus;
        [Label("攻撃力増加量")]
        [Tooltip("増加量（攻撃力増加アイテムの場合）")]
        public int attackDamageBonus;

        [Label("解放する能力")]
        [Tooltip("解放する能力")]
        public PlayerAbilityType abilityType = PlayerAbilityType.None;

        [Header("その他")]
        [Label("重ね持ち可能")]
        [Tooltip("スタック可能か")]
        public bool stackable = true;
        
        [Label("最大所持数")]
        [Tooltip("最大スタック数")]
        public int maxStackSize = 99;
        
        [Label("取得時のSE")]
        [Tooltip("取得時の効果音")]
        public AudioClip pickupSound;

        [Header("装備")]
        [Label("装備バッジ画像")]
        [Tooltip("スロットに表示するバッジ画像（アイテムごとに異なる説明バッジ）")]
        public Sprite equipmentBadge;

        [Label("装備の効果")]
        [Tooltip("装備アイテムとして使う場合の効果一覧")]
        public EquipmentAbilityData[] equipmentAbility;

        [Label("装備の見た目")]
        [Tooltip("装備時にPlayerへ表示する画像レイヤー一覧")]
        public EquipmentVisualLayerData[] equipmentVisualLayers;
    }

    /// <summary>
    /// アイテムの分類を定義
    /// </summary>
    public enum ItemType
    {
        [InspectorName("消費アイテム")]
        Consumable,    // 消費アイテム
        [InspectorName("重要アイテム")]
        KeyItem,       // 重要アイテム
        [InspectorName("装備")]
        Equipment,     // 装備
        [InspectorName("収集アイテム")]
        Collectible    // 収集品
    }

    public enum EquipmentAbilityType
    {
        [InspectorName("攻撃力倍率")]
        AttackPowerMultiplier,  //攻撃力倍率
        [InspectorName("敵撃破時HP回復")]
        HealPercentOnEnemyKill, //敵撃破時HP回復
        [InspectorName("銃反動量アップ")]
        GunRecoilForceBonus,    //銃反動量をマス単位で追加
        [InspectorName("反動移動クールタイム倍率")]
        RecoilCooldownMultiplier,   // 反動移動CT倍率。10%減なら0.9
        [InspectorName("ジャンプ高さアップ")]
        JumpHeightBonus,            // ジャンプ高さ加算。+0.5マスなら0.5
        [InspectorName("HP上限倍率")]
        MaxHealthMultiplier,        // HP上限倍率。+10%なら1.1
        [InspectorName("反動移動クールタイムなし")]
        RecoilCooldownDisabled,     // 反動移動CTなし
        [InspectorName("優雅ポイント獲得倍率")]
        ElegantPointGainMultiplier  // 優雅ポイント獲得倍率。+5%なら1.05
    }

    public enum EquipmentVisualParent
    {
        [InspectorName("後ろ")]
        Back,
        [InspectorName("前")]
        Front
    }

    [System.Serializable]

    ///装備時の能力効果を示すデータ
    public sealed class EquipmentAbilityData
    {
        [Label("効果タイプ")]
        [Tooltip("装備効果の種類")]
        public EquipmentAbilityType abilityType;

        [Label("効果値")]
        [Tooltip("効果値。攻撃力5%アップなら1.05、撃破時HP2%回復なら0.02、銃反動+1マスなら1、反動CT10%減なら0.9、ジャンプ+0.5マスなら0.5、HP上限10%アップなら1.1、反動CTなしなら1、優雅ポイント5%アップなら1.05")]
        public float value = 1f;
    }


    //装備時に表示する画像情報を示すデータ
    //画像によってサイズや配置を決められるようにする
    [System.Serializable]
    public sealed class EquipmentVisualLayerData
    {
        [Label("レイヤー名")]
        [Tooltip("管理用の名前。空でも動作します")]
        public string layerName;

        [Label("表示画像")]
        [Tooltip("装備時に表示するSprite")]
        public Sprite sprite;

        [Label("表示位置")]
        [Tooltip("Playerの後ろ/前どちらの子オブジェクトへ置くか")]
        public EquipmentVisualParent parent = EquipmentVisualParent.Back;

        [Label("位置")]
        [Tooltip("親から見た位置")]
        public Vector3 localPosition;

        [Label("左向き専用位置を使う")]
        [Tooltip("左向き時だけ別の位置を使う")]
        public bool useLeftFacingLocalPosition;

        [Label("左向き時の位置")]
        [Tooltip("左向き時の親から見た位置")]
        public Vector3 leftFacingLocalPosition;

        [Label("回転")]
        [Tooltip("親から見た回転")]
        public Vector3 localEulerAngles;

        [Label("拡大率")]
        [Tooltip("親から見た拡大率")]
        public Vector3 localScale = Vector3.one;

        [Label("描画順の差")]
        [Tooltip("PlayerのSpriteRendererを基準にした描画順の差。後ろ側は-1、前側は+1から調整する想定")]
        public int sortingOrderOffset;
    }
}
