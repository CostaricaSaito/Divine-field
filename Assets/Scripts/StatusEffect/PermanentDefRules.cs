using System.Collections.Generic;

/// <summary>
/// 残存DEFの集計（複数付与時は属性合算ルール＝<see cref="ElementHelper.GetCombinedElementFromElements"/>）。
/// </summary>
public static class PermanentDefRules
{
    public readonly struct Bundle
    {
        public readonly ElementType Element;
        public readonly int Power;

        public Bundle(ElementType element, int power)
        {
            Element = element;
            Power = power;
        }

        public static readonly Bundle Empty = new Bundle(ElementType.None, 0);
    }

    public static Bundle GetBundle(PlayerStatus defender)
    {
        if (defender == null) return Bundle.Empty;

        var elements = new List<ElementType>();
        int totalPower = 0;

        foreach (var effect in defender.activeEffects)
        {
            if (effect is not IPermanentDefContributor contributor) continue;
            PermanentDefContribution c = contributor.GetPermanentDefContribution();
            if (c.Value <= 0) continue;
            totalPower += c.Value;
            elements.Add(c.Element);
        }

        if (totalPower <= 0) return Bundle.Empty;
        ElementType combined = ElementHelper.GetCombinedElementFromElements(elements);
        return new Bundle(combined, totalPower);
    }

    public static ElementType ResolveDisplayCombinedElement(
        IReadOnlyList<CardData> defenseCards,
        PlayerStatus defender)
    {
        bool hasCards = defenseCards != null && defenseCards.Count > 0;
        ElementType cardElement = hasCards
            ? ElementHelper.GetCombinedElement(defenseCards)
            : ElementType.None;
        Bundle perm = GetBundle(defender);

        if (!hasCards)
            return perm.Power > 0 ? perm.Element : ElementType.None;
        if (perm.Power <= 0)
            return cardElement;

        return ElementHelper.GetCombinedElementFromElements(new[] { cardElement, perm.Element });
    }
}
