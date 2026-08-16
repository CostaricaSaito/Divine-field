using System.Collections.Generic;

/// <summary>
/// ArchMagic and Ultimate Skill card grand-magic attack classification.
/// 反射・打ち払い・無効化は対応する Kind が <see cref="ReflectionKind.Full"/> 等のときのみ有効。
/// </summary>
public static class GrandMagicRules
{
    public static bool IsGrandMagicStyleAttackCard(CardData c)
    {
        if (c == null) return false;
        if (c.cardType == CardType.Ultimate) return true;
        return ArchMagicRules.IsArchMagicCard(c);
    }

    public static bool ContainsGrandMagicStyleAttack(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsGrandMagicStyleAttackCard(cards[i])) return true;
        }
        return false;
    }
}
