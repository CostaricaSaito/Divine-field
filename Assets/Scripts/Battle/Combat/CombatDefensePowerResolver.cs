using System.Collections.Generic;

/// <summary>
/// 防御カード＋残存DEFの実効防御力（属性はカード合算と残存を別判定して加算）。
/// </summary>
public static class CombatDefensePowerResolver
{
    public static int SumCardDefensePower(IReadOnlyList<CardData> defenseCards)
    {
        if (defenseCards == null || defenseCards.Count == 0) return 0;
        int sum = 0;
        for (int i = 0; i < defenseCards.Count; i++)
        {
            CardData c = defenseCards[i];
            if (c != null) sum += c.defensePower;
        }
        return sum;
    }

    public static int SumCardDefensePower(CardData singleCard) =>
        singleCard != null ? singleCard.defensePower : 0;

    public static int ResolveEffectiveDefensePower(
        IReadOnlyList<CardData> defenseCards,
        PlayerStatus defender,
        ElementType attackElement)
    {
        int cardPart = ResolveEffectiveCardDefensePower(defenseCards, attackElement);
        int permPart = ResolveEffectivePermanentDefensePower(defender, attackElement);
        return cardPart + permPart;
    }

    public static int ResolveEffectiveDefensePower(
        CardData singleCard,
        PlayerStatus defender,
        ElementType attackElement)
    {
        if (singleCard == null)
            return ResolveEffectiveDefensePower((IReadOnlyList<CardData>)null, defender, attackElement);
        return ResolveEffectiveDefensePower(new List<CardData> { singleCard }, defender, attackElement);
    }

    public static bool TryGetDefenseDisplay(
        IReadOnlyList<CardData> defenseCards,
        PlayerStatus defender,
        ElementType attackElement,
        out int effectivePower,
        out ElementType displayElement)
    {
        effectivePower = ResolveEffectiveDefensePower(defenseCards, defender, attackElement);
        displayElement = PermanentDefRules.ResolveDisplayCombinedElement(defenseCards, defender);
        return effectivePower > 0;
    }

    private static int ResolveEffectiveCardDefensePower(
        IReadOnlyList<CardData> defenseCards,
        ElementType attackElement)
    {
        if (defenseCards == null || defenseCards.Count == 0) return 0;
        int sum = SumCardDefensePower(defenseCards);
        if (sum <= 0) return 0;
        if (attackElement != ElementType.None
            && !ElementHelper.CanDefendAgainst(attackElement, defenseCards))
            return 0;
        return sum;
    }

    private static int ResolveEffectivePermanentDefensePower(
        PlayerStatus defender,
        ElementType attackElement)
    {
        PermanentDefRules.Bundle perm = PermanentDefRules.GetBundle(defender);
        if (perm.Power <= 0) return 0;
        if (attackElement != ElementType.None
            && !ElementHelper.CanDefendAgainst(attackElement, perm.Element))
            return 0;
        return perm.Power;
    }
}
