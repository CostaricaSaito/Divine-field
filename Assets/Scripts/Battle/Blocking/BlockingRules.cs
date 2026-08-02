using System.Collections.Generic;

/// <summary>
/// 無効化（ブロッキング）防御カードの可否。最終攻撃属性は <see cref="ElementHelper.GetCombinedElement"/>、
/// 物理／魔法判定は反射と同じ（<see cref="ReflectionRules"/>・<see cref="CardRules.IsMagicOnlyAttackCombo"/>）。
/// </summary>
public static class BlockingRules
{
    public static bool IsPhysicalBlockingCard(CardData c)
    {
        return c != null && c.blockingKind == BlockingKind.Physical;
    }

    /// <summary>物理無効が通る攻撃か（合算属性が無属性かつ魔法単体攻撃でない）。</summary>
    public static bool CanBlockPhysical(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        if (ElementHelper.GetCombinedElement(incomingAttack) != ElementType.None) return false;
        return ReflectionRules.CanReflectPhysical(incomingAttack);
    }

    /// <summary>
    /// 物理無効防御（<<アイアンクラッド>> 等）が与えられた攻撃に対して成立するか。
    /// 最終合算属性が無属性の物理攻撃のみ。魔法単体攻撃は不可。
    /// </summary>
    public static bool CanUsePhysicalBlockingAgainstAttack(CardData defense, IReadOnlyList<CardData> incomingAttack)
    {
        if (defense == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttack)) return false;
        if (!IsPhysicalBlockingCard(defense)) return false;
        return CanBlockPhysical(incomingAttack);
    }

    /// <summary>魔法防御カードの MP が足りるか（非魔法・MP0 は true）。</summary>
    public static bool CanAffordMagicDefenseMp(CardData defense, PlayerStatus defender)
    {
        if (defense == null || defender == null) return true;
        if (defense.cardType != CardType.Magic || defense.mpCost <= 0) return true;
        if (defender.IsMagicUseForbidden()) return false;
        return defender.currentMP >= defender.GetEffectiveMagicMpCost(defense.mpCost);
    }

    /// <summary>プレイヤーが手札／魔法パネルから選べる物理無効魔法防御か（属性＋MP）。</summary>
    public static bool CanPlayerSelectPhysicalBlockingDefense(
        CardData defense,
        IReadOnlyList<CardData> incomingAttack,
        PlayerStatus defender)
    {
        return CanUsePhysicalBlockingAgainstAttack(defense, incomingAttack)
            && CanAffordMagicDefenseMp(defense, defender);
    }

    /// <summary>
    /// 無効化カードが単独確定必須か（他防御と併用不可）。該当する無効化種別の攻撃であるときのみ true。
    /// </summary>
    public static bool RequiresBlockingExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        return CanUsePhysicalBlockingAgainstAttack(card, incomingAttack);
    }

    /// <summary>反射用・無効化用・打ち払い用のいずれかで「防御1枚のみ」ロックが必要か。</summary>
    public static bool RequiresDefenseNullifyExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        return ReflectionRules.RequiresReflectionExclusiveLock(card, incomingAttack)
            || RequiresBlockingExclusiveLock(card, incomingAttack)
            || ParryRules.RequiresParryExclusiveLock(card, incomingAttack);
    }

    /// <summary>
    /// いずれかの防御カードが、与えられた攻撃に対して反射（物理／魔法）・無効化・打ち払いとして解決されるか。
    /// TotalATK/DEF に DEF を出さない判定などに使う。
    /// </summary>
    public static bool AnyDefenseCardResolvesAsReflectionOrNullify(
        IReadOnlyList<CardData> defenseCards,
        IReadOnlyList<CardData> incomingAttack)
    {
        if (defenseCards == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        foreach (var c in defenseCards)
        {
            if (c != null && RequiresDefenseNullifyExclusiveLock(c, incomingAttack))
                return true;
        }
        return false;
    }
}
