using System.Collections.Generic;

public static class CyberExoskeletonRules
{
    public static bool IsCyberExoskeletonCard(CardData card) =>
        card != null && card.specialCardEffect is CyberExoskeletonDefenseEffectSO;

    public static bool ContainsCyberExoskeleton(IReadOnlyList<CardData> defenseCards)
    {
        if (defenseCards == null) return false;
        for (int i = 0; i < defenseCards.Count; i++)
        {
            if (IsCyberExoskeletonCard(defenseCards[i])) return true;
        }
        return false;
    }
}
