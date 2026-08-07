using System.Collections.Generic;

/// <summary>Near-death hand cards (Phoenix Feather etc.). Hand order matches <see cref="DeadlyChainRules"/>.</summary>
public static class NearDeathCardRules
{
    public static bool IsNearDeathHandCard(CardData card) =>
        card != null && card.nearDeathCardEffect != null;

    public static bool TryGetFirstNearDeathCardInHandOrder(List<CardData> hand, out CardData card)
    {
        card = null;
        if (hand == null) return false;

        int bestOrder = int.MaxValue;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsNearDeathHandCard(c)) continue;
            int order = GetHandOrderKey(c, i);
            if (order >= bestOrder) continue;
            bestOrder = order;
            card = c;
        }

        return card != null;
    }

    private static int GetHandOrderKey(CardData card, int listIndex)
    {
        int sibling = GetHandSlotIndex(card);
        if (sibling >= 0) return sibling;
        return 1000 + listIndex;
    }

    public static List<CardData> CollectNearDeathCardsInHandOrder(List<CardData> hand)
    {
        var list = new List<CardData>();
        if (hand == null) return list;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (IsNearDeathHandCard(c))
                list.Add(c);
        }
        list.Sort(CompareHandOrder);
        return list;
    }

    public static int GetHandSlotIndex(CardData card)
    {
        if (card?.cardUI != null)
            return card.cardUI.transform.GetSiblingIndex();
        return -1;
    }

    private static int CompareHandOrder(CardData a, CardData b)
    {
        return GetHandSlotIndex(a).CompareTo(GetHandSlotIndex(b));
    }
}
