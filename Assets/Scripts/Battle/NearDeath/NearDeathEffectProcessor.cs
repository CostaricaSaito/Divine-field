using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// HP0 検出後「往生」表示の直前に実行する Near-death 効果キュー（不死鳥の尾羽根等）。
/// </summary>
public static class NearDeathEffectProcessor
{
    private readonly struct QueueEntry
    {
        public readonly CardData Card;
        public readonly PlayerStatus Owner;
        public readonly PlayerStatus Opponent;
        public readonly PlayerType OwnerSide;
        public readonly NearDeathCardEffectSO Effect;

        public QueueEntry(
            CardData card,
            PlayerStatus owner,
            PlayerStatus opponent,
            PlayerType ownerSide,
            NearDeathCardEffectSO effect)
        {
            Card = card;
            Owner = owner;
            Opponent = opponent;
            OwnerSide = ownerSide;
            Effect = effect;
        }
    }

    public static bool HasPendingRevival(BattleManager battleManager) =>
        battleManager != null && BuildQueue(battleManager).Count > 0;

    /// <summary>
    /// Dead players with a near-death card revive before Ojyou. Returns true if any revival ran.
    /// </summary>
    public static async Task<bool> TryReviveDeadPlayersAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        CancellationToken cancellationToken)
    {
        if (battleManager == null) return false;

        var queue = BuildQueue(battleManager);
        if (queue.Count == 0) return false;

        Debug.Log($"[NearDeathEffectProcessor] Queue: {queue.Count} revival(s)");

        BattleUIManager.I?.HideAllCardDetails();

        if (queue.Count == 1)
        {
            await ResolveEntryAsync(queue[0], battleManager, battleProcessor, handRefill, cancellationToken);
        }
        else
        {
            var tasks = new Task[queue.Count];
            for (int i = 0; i < queue.Count; i++)
            {
                tasks[i] = ResolveEntryAsync(queue[i], battleManager, battleProcessor, handRefill, cancellationToken);
            }
            await Task.WhenAll(tasks);
        }

        BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
        return true;
    }

    /// <summary>
    /// Client: apply host-authoritative near-death card consumption without replaying VFX.
    /// </summary>
    public static void ApplySyncedCardConsumption(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        PlayerType ownerSide,
        string cardName)
    {
        if (battleManager == null || battleProcessor == null || string.IsNullOrEmpty(cardName))
            return;

        var hand = ownerSide == PlayerType.Player ? battleManager.playerHand : battleManager.cpuHand;
        if (hand == null) return;

        CardData card = FindCardInHandByName(hand, cardName);
        if (card == null) return;
        if (card.cardUI != null && card.cardUI.IsFaceDown()) return;

        if (ownerSide == PlayerType.Player)
        {
            int slot = NearDeathCardRules.GetHandSlotIndex(card);
            if (slot >= 0)
                handRefill?.RecordPlayerUseSlot(slot);
            battleProcessor.UseCard(card, hand);
        }
        else
        {
            handRefill?.RecordEnemyUse(card);
            battleProcessor.UseCard(card, hand);
        }

        Debug.Log($"[NearDeathEffectProcessor] Synced near-death consumption: {cardName} ({ownerSide})");
    }

    private static CardData FindCardInHandByName(List<CardData> hand, string cardName)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c != null && c.cardName == cardName)
                return c;
        }
        return null;
    }

    private static async Task ResolveEntryAsync(
        QueueEntry entry,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entry.Card == null || entry.Effect == null || entry.Owner == null)
            return;

        await entry.Effect.ResolveNearDeathAsync(
            entry.Card,
            entry.Owner,
            entry.Opponent,
            entry.OwnerSide,
            battleManager,
            battleProcessor,
            handRefill,
            cancellationToken);
    }

    private static List<QueueEntry> BuildQueue(BattleManager battleManager)
    {
        var result = new List<QueueEntry>();
        TryAddSide(battleManager, PlayerType.Player, result);
        TryAddSide(battleManager, PlayerType.Enemy, result);
        return result;
    }

    private static void TryAddSide(BattleManager battleManager, PlayerType side, List<QueueEntry> result)
    {
        PlayerStatus owner = side == PlayerType.Player
            ? battleManager.GetPlayerStatus()
            : battleManager.GetEnemyStatus();
        if (owner == null || !owner.IsDead()) return;

        List<CardData> hand = side == PlayerType.Player
            ? battleManager.playerHand
            : battleManager.cpuHand;
        if (!NearDeathCardRules.TryGetFirstNearDeathCardInHandOrder(hand, out var card))
            return;
        if (card.nearDeathCardEffect == null) return;

        PlayerStatus opponent = side == PlayerType.Player
            ? battleManager.GetEnemyStatus()
            : battleManager.GetPlayerStatus();

        result.Add(new QueueEntry(card, owner, opponent, side, card.nearDeathCardEffect));
    }
}
