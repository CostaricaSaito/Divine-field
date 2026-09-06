using System.Collections.Generic;

public static class ThiefHoodRules
{
    public static bool IsThiefHoodCard(CardData card)
        => card != null && card.specialCardEffect is ThiefHoodDefenseEffectSO;

    public static int CountThiefHoods(IReadOnlyList<CardData> defenseCards)
    {
        if (defenseCards == null) return 0;

        int count = 0;
        for (int i = 0; i < defenseCards.Count; i++)
        {
            if (IsThiefHoodCard(defenseCards[i]))
                count++;
        }

        return count;
    }

    public static List<CardData> CollectThiefHoodCards(IReadOnlyList<CardData> defenseCards)
    {
        var hoods = new List<CardData>();
        if (defenseCards == null) return hoods;

        for (int i = 0; i < defenseCards.Count; i++)
        {
            var card = defenseCards[i];
            if (IsThiefHoodCard(card))
                hoods.Add(card);
        }

        return hoods;
    }

    /// <summary>
    /// Attack combo cards that may be copied into the defender's hand.
    /// </summary>
    public static bool IsStealableAttackCard(CardData card)
    {
        if (card == null) return false;
        if (card.cardType == CardType.Ultimate || card.cardType == CardType.Disaster) return false;
        if (card.cardType == CardType.Special) return false;
        if (EconomicActionNames.IsEconomicAttack(card.cardName)) return false;
        if (CardRules.IsImmediateAction(card)) return false;
        return true;
    }

    public static List<CardData> CollectStealableAttackCards(IReadOnlyList<CardData> attackCards)
    {
        var pool = new List<CardData>();
        if (attackCards == null) return pool;

        for (int i = 0; i < attackCards.Count; i++)
        {
            var card = attackCards[i];
            if (IsStealableAttackCard(card))
                pool.Add(card);
        }

        return pool;
    }

    public static List<string> PickStealTemplateNames(
        IReadOnlyList<CardData> stealablePool,
        int pickCount)
    {
        var names = new List<string>();
        if (stealablePool == null || stealablePool.Count == 0 || pickCount <= 0)
            return names;

        for (int i = 0; i < pickCount; i++)
        {
            int idx = BattleRandom.Range(0, stealablePool.Count);
            var picked = stealablePool[idx];
            if (picked == null) continue;
            string name = !string.IsNullOrEmpty(picked.cardName) ? picked.cardName : picked.name;
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        return names;
    }
}
