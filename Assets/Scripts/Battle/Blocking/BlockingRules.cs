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

    /// <summary>物理無効が通る攻撃か（合算属性が無属性かつ魔法単体でない＝物理判定側）。</summary>
    public static bool CanBlockPhysical(IReadOnlyList<CardData> incomingAttack)
    {
        return ReflectionRules.CanReflectPhysical(incomingAttack);
    }

    /// <summary>
    /// 無効化カードが単独確定必須か（他防御と併用不可）。該当する無効化種別の攻撃であるときのみ true。
    /// </summary>
    public static bool RequiresBlockingExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        if (card == null || incomingAttack == null || incomingAttack.Count == 0) return false;
        if (IsPhysicalBlockingCard(card) && CanBlockPhysical(incomingAttack)) return true;
        return false;
    }

    /// <summary>反射用・無効化用のいずれかで「防御1枚のみ」ロックが必要か。</summary>
    public static bool RequiresDefenseNullifyExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        return ReflectionRules.RequiresReflectionExclusiveLock(card, incomingAttack)
            || RequiresBlockingExclusiveLock(card, incomingAttack);
    }
}
