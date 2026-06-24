using UnityEngine;

public static class PlayerAttackPowerBonusState
{
    private static int attackDamageBonus;

    public static int AttackDamageBonus => Mathf.Max(0, attackDamageBonus);

    public static void AddBonus(int value)
    {
        if (value <= 0) return;
        attackDamageBonus += value;
    }

    public static void ClearAll()
    {
        attackDamageBonus = 0;
    }
}
