using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hand destruction target selection (excludes MagicPool entries).
/// </summary>
public static class HandDestroyRules
{
    /// <summary>
    /// Pick one random destroyable card from hand. Returns null when none.
    /// </summary>
    public static CardData PickRandomDestroyableCard(List<CardData> hand, PlayerType handOwner)
    {
        if (hand == null || hand.Count == 0) return null;

        var candidates = new List<CardData>();
        foreach (var card in hand)
        {
            if (card == null) continue;
            if (MagicPoolManager.I != null && MagicPoolManager.I.IsInPool(card, handOwner))
                continue;
            candidates.Add(card);
        }

        if (candidates.Count == 0) return null;
        int index = BattleRandom.Range(0, candidates.Count);
        return candidates[index];
    }
}
