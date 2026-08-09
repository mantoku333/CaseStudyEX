using Metroidvania.Data;

/// <summary>
/// Result of attempting to buy a decoration with Elegant Points.
/// </summary>
public enum DecorationPurchaseResult
{
    Success,
    InvalidItem,
    NotEquipment,
    NotPurchasable,
    AlreadyOwned,
    InsufficientPoints
}

/// <summary>
/// Performs the inventory and progress-flag mutation for a decoration purchase.
/// Saving is deliberately left to the game's normal manual/autosave flow.
/// </summary>
public static class DecorationPurchaseService
{
    public static bool TryPurchase(ItemData itemData, out DecorationPurchaseResult result)
    {
        if (itemData == null || string.IsNullOrWhiteSpace(itemData.itemId))
        {
            result = DecorationPurchaseResult.InvalidItem;
            return false;
        }

        if (itemData.itemType != ItemType.Equipment)
        {
            result = DecorationPurchaseResult.NotEquipment;
            return false;
        }

        if (itemData.elegantPointCost <= 0)
        {
            result = DecorationPurchaseResult.NotPurchasable;
            return false;
        }

        // Either representation means this save already owns the item. Older
        // saves may contain only the progress flag, so both stores matter.
        if (GameItems.GetCount(itemData.itemId) > 0 || GameProgressFlags.Get(itemData.itemId))
        {
            result = DecorationPurchaseResult.AlreadyOwned;
            return false;
        }

        if (!ElegantPointWallet.TrySpend(itemData.elegantPointCost))
        {
            result = DecorationPurchaseResult.InsufficientPoints;
            return false;
        }

        GameItems.SetCount(itemData.itemId, 1);
        GameProgressFlags.Set(itemData.itemId, true);
        result = DecorationPurchaseResult.Success;
        return true;
    }
}
