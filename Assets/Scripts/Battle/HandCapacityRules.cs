using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hand overflow trim when count exceeds <see cref="BattleManager.MaxHandCards"/>.
/// Uses shared <see cref="BattleRandom"/> so online simulations stay aligned.
/// </summary>
public static class HandCapacityRules
{
    public static int TrimOverflowRandom(List<CardData> hand, int maxCount, bool destroyPlayerUi)
    {
        if (hand == null || maxCount < 0) return 0;

        int removed = 0;
        while (hand.Count > maxCount)
        {
            int idx = BattleRandom.Range(0, hand.Count);
            RemoveAt(hand, idx, destroyPlayerUi);
            removed++;
        }

        return removed;
    }

    private static void RemoveAt(List<CardData> hand, int index, bool destroyPlayerUi)
    {
        if (hand == null || index < 0 || index >= hand.Count) return;

        var card = hand[index];
        hand.RemoveAt(index);

        if (card == null) return;

        if (destroyPlayerUi && card.cardUI != null)
        {
            Object.Destroy(card.cardUI.gameObject);
            card.cardUI = null;
        }

        Object.Destroy(card);
    }
}
