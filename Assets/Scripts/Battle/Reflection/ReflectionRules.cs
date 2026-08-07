using System.Collections.Generic;

/// <summary>
/// 反射カードの可否判定（物理反射・魔法反射・完全反射）。
/// </summary>
public static class ReflectionRules
{
    public static bool IsReflectionCard(CardData c)
    {
        return c != null && c.reflectionKind != ReflectionKind.None;
    }

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
        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(incomingAttack)) return false;
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
        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(incomingAttack)) return false;
        return CardRules.IsMagicClassifiedAttackCombo(incomingAttack);
    }

    /// <summary>
    /// 跳ね返し対象 incoming がダメージ戦闘反射（<see cref="PhysicalReflectionFlow"/>）か。
    /// false のときは即時効果反射（回復・状態異常・Special 単体等）。
    /// </summary>
    public static bool ShouldUseImmediateEffectReflectionFlow(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        if (DeadlyChainRules.IsActivePostDeathIncoming(incomingAttack)) return false;
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttack)) return false;
        if (IncomingHasReflectableCombatDamage(incomingAttack)) return false;
        if (CardRules.IncomingIsSingleImmediateActionAttack(incomingAttack)) return true;
        if (incomingAttack.Count == 1 && incomingAttack[0] != null && incomingAttack[0].cardType == CardType.Special)
            return true;
        return CardRules.IncomingRequiresFullOnlyReactiveDefense(incomingAttack);
    }

    /// <summary>防御カードが incoming を跳ね返せるか（物理／魔法／完全反射の統合判定）。</summary>
    public static bool CanReflectIncoming(CardData defense, IReadOnlyList<CardData> incomingAttack)
    {
        if (defense == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        if (DeadlyChainRules.IsActivePostDeathIncoming(incomingAttack))
            return false;
        if (IsFullReflectionCard(defense)) return true;
        if (CanUsePhysicalReflectionAgainstAttack(defense, incomingAttack)) return true;
        return CanUseMagicReflectionAgainstAttack(defense, incomingAttack);
    }

    /// <summary>プレイヤー／敵防御解決用：物理反射経路が成立するか。</summary>
    public static bool CanUsePhysicalReflectionAgainstAttack(CardData defense, IReadOnlyList<CardData> incomingAttack)
    {
        if (defense == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        if (IsFullReflectionCard(defense) && !ShouldUseImmediateEffectReflectionFlow(incomingAttack))
            return true;
        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(incomingAttack))
            return IsFullReflectionCard(defense);
        if (!CanReflectPhysical(incomingAttack)) return false;
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttack))
            return IsFullReflectionCard(defense);
        return IsPhysicalReflectionCard(defense) || IsFullReflectionCard(defense);
    }

    /// <summary>プレイヤー／敵防御解決用：魔法反射経路が成立するか。</summary>
    public static bool CanUseMagicReflectionAgainstAttack(CardData defense, IReadOnlyList<CardData> incomingAttack)
    {
        if (defense == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        if (IsFullReflectionCard(defense) && !ShouldUseImmediateEffectReflectionFlow(incomingAttack))
            return true;
        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(incomingAttack))
            return IsFullReflectionCard(defense);
        if (!CanReflectMagic(incomingAttack)) return false;
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttack))
            return IsFullReflectionCard(defense);
        return IsMagicReflectionCard(defense) || IsFullReflectionCard(defense);
    }

    /// <summary>
    /// 反射カードが単独確定必須か（他防御と併用不可）。該当する反射種別の攻撃であるときのみ true。
    /// </summary>
    public static bool RequiresReflectionExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        return CanReflectIncoming(card, incomingAttack);
    }

    private static bool IncomingHasReflectableCombatDamage(IReadOnlyList<CardData> incomingAttack)
    {
        for (int i = 0; i < incomingAttack.Count; i++)
        {
            var c = incomingAttack[i];
            if (c == null) continue;
            if (c.attackPower > 0) return true;
            if (CardRules.IsAttackMagic(c)) return true;
            if (c.cardType == CardType.Ultimate || c.cardType == CardType.ArchMagic) return true;
        }
        return false;
    }
}
