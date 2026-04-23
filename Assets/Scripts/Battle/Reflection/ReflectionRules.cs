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

    public static bool IsFullReflectionCard(CardData c)
    {
        return c != null && c.reflectionKind == ReflectionKind.Full;
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
        if (CardRules.IsMagicClassifiedAttackCombo(incomingAttack)) return false;
        return true;
    }

    /// <summary>
    /// 魔法反射が「跳ね返せる」攻撃か（魔法単体＝混在なし。属性は問わない）。
    /// </summary>
    public static bool CanReflectMagic(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        return CardRules.IsMagicClassifiedAttackCombo(incomingAttack);
    }

    /// <summary>プレイヤー／敵防御解決用：物理反射経路が成立するか。</summary>
    public static bool CanUsePhysicalReflectionAgainstAttack(CardData defense, IReadOnlyList<CardData> incomingAttack)
    {
        if (!CanReflectPhysical(incomingAttack)) return false;
        if (defense == null) return false;
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttack))
            return IsFullReflectionCard(defense);
        return IsPhysicalReflectionCard(defense);
    }

    /// <summary>プレイヤー／敵防御解決用：魔法反射経路が成立するか。</summary>
    public static bool CanUseMagicReflectionAgainstAttack(CardData defense, IReadOnlyList<CardData> incomingAttack)
    {
        if (!CanReflectMagic(incomingAttack)) return false;
        if (defense == null) return false;
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttack))
            return IsFullReflectionCard(defense);
        return IsMagicReflectionCard(defense);
    }

    /// <summary>
    /// 反射カードが単独確定必須か（他防御と併用不可）。該当する反射種別の攻撃であるときのみ true。
    /// </summary>
    public static bool RequiresReflectionExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        if (card == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        if (CanUsePhysicalReflectionAgainstAttack(card, incomingAttack)) return true;
        if (CanUseMagicReflectionAgainstAttack(card, incomingAttack)) return true;
        return false;
    }
}
