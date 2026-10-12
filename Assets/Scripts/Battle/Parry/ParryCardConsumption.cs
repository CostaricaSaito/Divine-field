using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a parry defense card as used for the turn (face-down / inactive). Idempotent across ProcessCards and reflection-chain paths.
/// </summary>
public static class ParryCardConsumption
{
    public static bool IsParryDefenseCardConsumed(
        CardData card,
        PlayerType owner,
        HandRefillService handRefill)
    {
        if (card == null) return true;
        if (card.cardType == CardType.Magic) return false;

        if (owner == PlayerType.Player)
        {
            if (card.cardUI != null && card.cardUI.IsFaceDown()) return true;
            return handRefill != null && handRefill.IsPlayerCardUsedThisTurn(card);
        }

        if (card.cardUI != null && card.cardUI.IsFaceDown()) return true;
        return handRefill != null && handRefill.IsEnemyCardUsedThisTurn(card);
    }

    /// <summary>
    /// Consumes the parry card once per turn. Safe to call from ParryFlow even if CardSequenceManager already consumed it.
    /// </summary>
    public static void TryConsumeParryDefenseCard(
        CardData card,
        PlayerType owner,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> hand)
    {
        if (card == null || battleProcessor == null || hand == null) return;
        if (card.cardType == CardType.Magic) return;
        if (IsParryDefenseCardConsumed(card, owner, handRefill)) return;

        if (owner == PlayerType.Player)
        {
            int slotIndex = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
            if (slotIndex >= 0)
                handRefill?.RecordPlayerUseSlot(slotIndex);
            battleProcessor.UseCard(card, hand);
            return;
        }

        handRefill?.RecordEnemyUse(card);
        battleProcessor.UseCard(card, hand);
    }
}
