﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Garuda Ultimate Skill: Ascendant Shade — draw to hand cap, then optional mulligan.
/// </summary>
public static class GarudaUltimateRules
{
    public const string AscendantShadeCardName = "\u30A2\u30BB\u30F3\u30C0\u30F3\u30C8\u30B7\u30A7\u30A4\u30C9";

    public static bool IsAscendantShadeCard(CardData card)
    {
        return card != null
            && card.cardType == CardType.Ultimate
            && card.cardName == AscendantShadeCardName;
    }

    /// <summary>Draw until hand cap; player cards are revealed one-by-one face-up.</summary>
    public static async Task DrawHandToMaxFaceUpAsync(
        BattleManager bm,
        HandRefillService handRefill,
        List<CardData> hand,
        bool isPlayerHand,
        CancellationToken ct)
    {
        if (bm == null || hand == null) return;

        int cap = isPlayerHand ? bm.GetHandMaxCount() : bm.GetEnemyHandCapacity();
        if (hand.Count >= cap) return;

        var drawn = new List<CardData>();
        while (hand.Count < cap)
        {
            ct.ThrowIfCancellationRequested();
            CardData card = isPlayerHand
                ? bm.cardDealer?.DrawRandomCard(PlayerType.Player)
                : bm.cardDealer?.DrawRandomCard(PlayerType.Enemy);
            if (card == null) break;

            hand.Add(card);
            drawn.Add(card);

            if (isPlayerHand)
                bm.cardDealer?.CreateCardUIForHand(card);
        }

        if (!isPlayerHand || handRefill == null) return;

        for (int i = 0; i < drawn.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await handRefill.RevealDrawnCardAfterCombatAsync(drawn[i], ct);
        }
    }

    /// <summary>CPU: prefer low GP value / low rarity; pick 3–5 cards at random.</summary>
    public static List<CardData> PickEnemyMulliganCards(List<CardData> hand)
    {
        var result = new List<CardData>();
        if (hand == null || hand.Count == 0) return result;

        int want = BattleRandom.Range(3, 6);
        want = Mathf.Min(want, hand.Count);

        var pool = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null) pool.Add(hand[i]);
        }

        for (int pick = 0; pick < want && pool.Count > 0; pick++)
        {
            int totalWeight = 0;
            var weights = new int[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                int w = Mathf.Max(1, 500 - MulliganPriorityScore(pool[i]));
                weights[i] = w;
                totalWeight += w;
            }

            int roll = BattleRandom.Range(0, totalWeight);
            int acc = 0;
            int chosenIndex = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += weights[i];
                if (roll < acc)
                {
                    chosenIndex = i;
                    break;
                }
            }

            result.Add(pool[chosenIndex]);
            pool.RemoveAt(chosenIndex);
        }

        return result;
    }

    private static int MulliganPriorityScore(CardData card)
    {
        if (card == null) return int.MaxValue;
        return card.cardValue * 100 + (int)card.rarity;
    }
}
