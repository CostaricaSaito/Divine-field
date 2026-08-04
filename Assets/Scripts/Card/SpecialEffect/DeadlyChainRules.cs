using System.Collections.Generic;
using UnityEngine;

public static class DeadlyChainRules
{
    public static bool IsDeadlyChainCard(CardData card) =>
        card != null && card.postDeathCardEffect is DeadlyChainPostDeathEffectSO;

    /// <summary>PostDeath キュー中の道連れ incoming は通常の属性攻撃として防御する。</summary>
    public static bool IsActivePostDeathIncoming(IReadOnlyList<CardData> incoming)
    {
        var ctx = PostDeathCombatContext.Active;
        return ctx != null && ctx.MatchesIncoming(incoming);
    }

    public static bool TryGetDeadlyChainEffect(CardData card, out DeadlyChainPostDeathEffectSO effect)
    {
        effect = card?.postDeathCardEffect as DeadlyChainPostDeathEffectSO;
        return effect != null;
    }

    /// <summary>Hand order: lower sibling index first (left / younger in layout).</summary>
    public static List<CardData> CollectPostDeathCardsInHandOrder(List<CardData> hand)
    {
        var list = new List<CardData>();
        if (hand == null) return list;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c != null && c.postDeathCardEffect != null)
                list.Add(c);
        }
        list.Sort(CompareHandOrder);
        return list;
    }

    private static int CompareHandOrder(CardData a, CardData b)
    {
        int ia = GetHandSlotIndex(a);
        int ib = GetHandSlotIndex(b);
        return ia.CompareTo(ib);
    }

    private static int GetHandSlotIndex(CardData card)
    {
        if (card?.cardUI != null)
            return card.cardUI.transform.GetSiblingIndex();
        return int.MaxValue;
    }
}
