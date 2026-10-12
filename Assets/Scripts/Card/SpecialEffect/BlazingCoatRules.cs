using System.Collections.Generic;

public static class BlazingCoatRules
{
    public static bool IsBlazingCoatCard(CardData card) =>
        card != null && card.specialCardEffect is BlazingCoatDefenseEffectSO;

    public static bool ContainsBlazingCoat(IReadOnlyList<CardData> defenseCards)
    {
        if (defenseCards == null) return false;
        for (int i = 0; i < defenseCards.Count; i++)
        {
            if (IsBlazingCoatCard(defenseCards[i])) return true;
        }
        return false;
    }
}
