using System.Collections.Generic;
using UnityEngine;

/// <summary>CPU hand reload: same-definition 3+, Common/Uncommon only.</summary>
public static class EnemyCpuHandReloadPlanner
{
    public static bool CanReload(List<CardData> hand)
    {
        return TryPickReloadDefinition(hand, out _);
    }

    public static bool Execute(List<CardData> hand, HandRefillService handRefill)
    {
        if (hand == null || handRefill == null) return false;
        if (!TryPickReloadDefinition(hand, out CardData reference)) return false;

        var toReplace = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c == null) continue;
            if (!CardRules.CanParticipateInHandReload(c)) continue;
            if (!CardDefinitionIdentity.IsSameDefinition(c, reference))
                continue;
            if (!EnemyAI.IsEnemyHandCardAvailable(c, handRefill)) continue;
            toReplace.Add(c);
        }

        if (toReplace.Count < 3) return false;

        handRefill.ReplaceEnemyHandCardsForMulligan(toReplace, hand);
        Debug.Log($"[EnemyCpuHandReload] Reloaded {toReplace.Count} cards ({reference.cardName})");
        return true;
    }

    private static bool TryPickReloadDefinition(List<CardData> hand, out CardData reference)
    {
        reference = null;
        if (hand == null) return false;

        var eligible = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c == null) continue;
            if (c.rarity != CardRarity.Common && c.rarity != CardRarity.Uncommon) continue;
            if (!CardRules.CanParticipateInHandReload(c)) continue;
            if (CardDefinitionIdentity.CountSameInHandForReload(c, hand) >= 3)
                eligible.Add(c);
        }

        if (eligible.Count == 0) return false;
        reference = eligible[BattleRandom.Range(0, eligible.Count)];
        return true;
    }
}
