using System.Collections.Generic;

/// <summary>
/// 打ち払い防御の可否（物理／魔法／完全）。判定は反射と同じ攻撃分類を使用。
/// </summary>
public static class ParryRules
{
    public static bool IsPhysicalParryCard(CardData c)
    {
        return c != null && c.parryKind == ParryKind.Physical;
    }

    public static bool IsMagicParryCard(CardData c)
    {
        return c != null && c.parryKind == ParryKind.Magic;
    }

    public static bool IsFullParryCard(CardData c)
    {
        return c != null && c.parryKind == ParryKind.Full;
    }

    public static bool IsParryCard(CardData c)
    {
        return c != null && c.parryKind != ParryKind.None;
    }

    /// <summary>打ち払いカードが、与えられた攻撃に対して有効か。</summary>
    public static bool CanParryIncoming(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        if (card == null || !IsParryCard(card) || incomingAttack == null || incomingAttack.Count == 0)
            return false;
        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(incomingAttack))
            return IsFullParryCard(card);
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttack))
        {
            return IsFullParryCard(card)
                && (ReflectionRules.CanReflectPhysical(incomingAttack) || ReflectionRules.CanReflectMagic(incomingAttack));
        }
        if (IsPhysicalParryCard(card) && ReflectionRules.CanReflectPhysical(incomingAttack)) return true;
        if (IsMagicParryCard(card) && ReflectionRules.CanReflectMagic(incomingAttack)) return true;
        if (IsFullParryCard(card)
            && (ReflectionRules.CanReflectPhysical(incomingAttack) || ReflectionRules.CanReflectMagic(incomingAttack)))
            return true;
        return false;
    }

    /// <summary>反射・無効化と同様、該当攻撃に対して単独選択必須か。</summary>
    public static bool RequiresParryExclusiveLock(CardData card, IReadOnlyList<CardData> incomingAttack)
    {
        return CanParryIncoming(card, incomingAttack);
    }
}
