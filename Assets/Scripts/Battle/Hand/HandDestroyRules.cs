using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hand destruction target selection (excludes MagicPool entries).
/// </summary>
public static class HandDestroyRules
{
    public readonly struct Pick
    {
        public readonly CardData Card;
        public readonly int HandIndex;

        public Pick(CardData card, int handIndex)
        {
            Card = card;
            HandIndex = handIndex;
        }
    }

    /// <summary>
    /// Pick one random destroyable card from hand. Returns null when none.
    /// </summary>
    public static CardData PickRandomDestroyableCard(List<CardData> hand, PlayerType handOwner)
    {
        var picks = PickRandomDestroyableCards(hand, handOwner, 1);
        return picks.Count > 0 ? picks[0].Card : null;
    }

    /// <summary>
    /// Pick up to <paramref name="maxCount"/> distinct random destroyable cards (no MagicPool entries).
    /// </summary>
    public static List<Pick> PickRandomDestroyableCards(List<CardData> hand, PlayerType handOwner, int maxCount)
    {
        var result = new List<Pick>();
        if (hand == null || hand.Count == 0 || maxCount <= 0) return result;

        var candidates = new List<Pick>();
        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            if (card == null) continue;
            if (MagicPoolManager.I != null && MagicPoolManager.I.IsInPool(card, handOwner))
                continue;
            candidates.Add(new Pick(card, i));
        }

        int take = Mathf.Min(maxCount, candidates.Count);
        for (int n = 0; n < take; n++)
        {
            int idx = BattleRandom.Range(0, candidates.Count);
            result.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }

        return result;
    }
}
