using System.Collections.Generic;

/// <summary>
/// 反射カードの可否判定（物理反射・魔法反射・将来の完全反射用に分離）。
/// </summary>
public static class ReflectionRules
{
    public static bool IsPhysicalReflectionCard(CardData c)
    {
        return c != null && c.reflectionKind == ReflectionKind.Physical;
    }

    public static bool IsMagicReflectionCard(CardData c)
    {
        return c != null && c.reflectionKind == ReflectionKind.Magic;
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

    /// <summary>
    /// 魔法反射が「跳ね返せる」攻撃か（魔法単体＝混在なし。属性は問わない）。
    /// </summary>
    public static bool CanReflectMagic(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        return CardRules.IsMagicOnlyAttackCombo(incomingAttack);
    }

    /// <summary>
    /// 反射カードが単独確定必須か（他防御と併用不可）。該当する反射種別の攻撃であるときのみ true。
    /// </summary>
    public static bool RequiresReflectionExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        if (card == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        if (IsPhysicalReflectionCard(card) && CanReflectPhysical(incomingAttack)) return true;
        if (IsMagicReflectionCard(card) && CanReflectMagic(incomingAttack)) return true;
        return false;
    }
}
