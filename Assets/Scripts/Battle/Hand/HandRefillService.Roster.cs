using System.Collections.Generic;
using UnityEngine;

public partial class HandRefillService
{
    /// <summary>
    /// Align used-this-turn flags to a received hand roster.
    /// Card instances in <paramref name="hand"/> are already the roster order.
    /// </summary>
    public void ApplyHandRosterPresentation(
        List<CardData> hand,
        List<NetworkBattleBridge.HandRosterSlot> slots,
        bool playerHand)
    {
        if (hand == null || slots == null) return;

        if (playerHand)
        {
            if (!PlayerUsedMatches(hand, slots))
                RebuildPlayerUsedSlots(hand, slots);
            return;
        }

        if (!EnemyUsedMatches(hand, slots))
            RebuildEnemyUsed(hand, slots);
    }

    private bool PlayerUsedMatches(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots)
    {
        int usedCount = 0;
        int limit = Mathf.Min(hand.Count, slots.Count);
        for (int i = 0; i < limit; i++)
        {
            if (!slots[i].Used) continue;
            usedCount++;
            var card = hand[i];
            if (card == null || !BackSlotMatches(i, card))
                return false;
        }

        return usedCount == _playerBackSlotsThisTurn.Count;
    }

    private bool BackSlotMatches(int index, CardData card)
    {
        int id = card.GetInstanceID();
        for (int i = 0; i < _playerBackSlotsThisTurn.Count; i++)
        {
            var slot = _playerBackSlotsThisTurn[i];
            if (slot.index != index || slot.usedCard == null) continue;
            if (slot.usedCard.GetInstanceID() == id)
                return true;
        }

        return false;
    }

    private void RebuildPlayerUsedSlots(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots)
    {
        _playerBackSlotsThisTurn.Clear();
        int limit = Mathf.Min(hand.Count, slots.Count);
        for (int i = 0; i < limit; i++)
        {
            if (!slots[i].Used) continue;
            var card = hand[i];
            if (card == null) continue;

            var ui = card.cardUI;
            if (ui != null && ui.GetCardData() != null)
            {
                ui.Setup(null, cardBackSprite, playerHandRareBackPresentation: false);
                if (ui.button != null)
                    ui.button.interactable = false;
            }

            _playerBackSlotsThisTurn.Add(new BackSlot { index = i, ui = ui, usedCard = card });
        }

        for (int i = 0; i < limit; i++)
        {
            if (slots[i].Used) continue;
            var card = hand[i];
            var ui = card != null ? card.cardUI : null;
            if (ui == null || ui.GetCardData() != null) continue;
            ui.Setup(card, cardBackSprite, playerHandRareBackPresentation: true);
            ui.Reveal();
        }
    }

    private bool EnemyUsedMatches(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots)
    {
        int usedCount = 0;
        int limit = Mathf.Min(hand.Count, slots.Count);
        for (int i = 0; i < limit; i++)
        {
            if (!slots[i].Used) continue;
            usedCount++;
            if (hand[i] == null || !IsEnemyCardUsedThisTurn(hand[i]))
                return false;
        }

        return usedCount == _enemyUsedCardsThisTurn.Count;
    }

    private void RebuildEnemyUsed(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots)
    {
        _enemyUsedCardsThisTurn.Clear();
        int limit = Mathf.Min(hand.Count, slots.Count);
        for (int i = 0; i < limit; i++)
        {
            if (slots[i].Used && hand[i] != null)
                _enemyUsedCardsThisTurn.Add(hand[i]);
        }
    }
}
