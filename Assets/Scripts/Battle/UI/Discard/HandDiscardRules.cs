/// <summary>Eligibility rules for discard selection UI.</summary>
public static class HandDiscardRules
{
    public static bool IsSelectableForDiscard(CardData card)
    {
        if (card == null || card.cardUI == null) return false;
        return !card.cardUI.IsFaceDown();
    }
}

/// <summary>CPU discard heuristics.</summary>
public static class HandDiscardEnemyAI
{
    public static System.Collections.Generic.List<CardData> PickDiscardCards(
        System.Collections.Generic.List<CardData> hand,
        int count)
    {
        var result = new System.Collections.Generic.List<CardData>(count);
        if (hand == null || count <= 0) return result;

        var pool = new System.Collections.Generic.List<CardData>(hand.Count);
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null) pool.Add(hand[i]);
        }

        if (pool.Count == 0) return result;

        pool.Sort((a, b) => a.cardValue.CompareTo(b.cardValue));

        int take = System.Math.Min(count, pool.Count);
        for (int i = 0; i < take; i++)
            result.Add(pool[i]);

        return result;
    }
}
