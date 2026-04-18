using System.Collections.Generic;

/// <summary>
/// 反射カードの可否判定（物理反射・将来の魔法／完全反射用に分離）。
/// </summary>
public static class ReflectionRules
{
    public static bool IsPhysicalReflectionCard(CardData c)
    {
        return c != null && c.reflectionKind == ReflectionKind.Physical;
    }

    /// <summary>
    /// 物理反射が「跳ね返せる」攻撃か（無属性かつ魔法単体攻撃でない）。
    /// </summary>
    public static bool CanReflectPhysical(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        var list = new List<CardData>(incomingAttack.Count);
        for (int i = 0; i < incomingAttack.Count; i++)
            list.Add(incomingAttack[i]);
        if (ElementHelper.GetCombinedElement(list) != ElementType.None)
            return false;
        if (CardRules.IsMagicOnlyAttackCombo(incomingAttack)) return false;
        return true;
    }
}
