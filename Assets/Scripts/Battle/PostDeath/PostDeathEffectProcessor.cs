using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// HP0 検出後「往生」表示の直後〜終了演出（鐘・GAMESET）前に実行する死亡時効果キュー。
/// </summary>
public static class PostDeathEffectProcessor
{
    private readonly struct QueueEntry
    {
        public readonly CardData Card;
        public readonly PlayerStatus DeadOwner;
        public readonly PlayerStatus Opponent;
        public readonly PostDeathCardEffectSO Effect;

        public QueueEntry(CardData card, PlayerStatus deadOwner, PlayerStatus opponent, PostDeathCardEffectSO effect)
        {
            Card = card;
            DeadOwner = deadOwner;
            Opponent = opponent;
            Effect = effect;
        }
    }

    public static bool HasPendingEffects(BattleManager battleManager) =>
        battleManager != null && BuildQueue(battleManager).Count > 0;

    public static async Task RunQueueAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        CancellationToken cancellationToken)
    {
        if (battleManager == null) return;

        var queue = BuildQueue(battleManager);
        if (queue.Count == 0) return;

        Debug.Log($"[PostDeathEffectProcessor] Queue: {queue.Count} effect(s)");

        BattleUIManager.I?.HideAllCardDetails();

        foreach (var entry in queue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Card == null || entry.Effect == null || entry.DeadOwner == null || entry.Opponent == null)
                continue;

            await entry.Effect.ResolvePostDeathAsync(
                entry.Card,
                entry.DeadOwner,
                entry.Opponent,
                battleManager,
                battleProcessor,
                handRefill,
                enemyAI,
                cancellationToken);
        }
    }

    private static List<QueueEntry> BuildQueue(BattleManager battleManager)
    {
        var result = new List<QueueEntry>();
        var sides = new[]
        {
            battleManager.OpeningTurnOwner,
            battleManager.OpeningTurnOwner == PlayerType.Player ? PlayerType.Enemy : PlayerType.Player,
        };

        foreach (var side in sides)
        {
            PlayerStatus owner = side == PlayerType.Player
                ? battleManager.GetPlayerStatus()
                : battleManager.GetEnemyStatus();
            if (owner == null || !owner.IsDead()) continue;

            List<CardData> hand = side == PlayerType.Player
                ? battleManager.playerHand
                : battleManager.cpuHand;
            var cards = DeadlyChainRules.CollectPostDeathCardsInHandOrder(hand);
            PlayerStatus opponent = side == PlayerType.Player
                ? battleManager.GetEnemyStatus()
                : battleManager.GetPlayerStatus();

            foreach (var card in cards)
            {
                if (card?.postDeathCardEffect == null) continue;
                result.Add(new QueueEntry(card, owner, opponent, card.postDeathCardEffect));
            }
        }

        return result;
    }
}
