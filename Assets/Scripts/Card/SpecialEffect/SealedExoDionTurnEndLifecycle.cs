using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// EndPhase: move pending Sealed EXO-DION from hand to magic panel right slot.
/// </summary>
public static class SealedExoDionTurnEndLifecycle
{
    public sealed class EndPhaseResult
    {
        public bool PlayerReachedWin;
        public bool EnemyReachedWin;
        public bool Stalemate;
    }

    public static async Task<EndPhaseResult> ProcessEndPhaseAsync(BattleManager bm)
    {
        var result = new EndPhaseResult();
        if (bm == null || MagicPoolManager.I == null) return result;

        var plan = BuildPlan(bm);
        await PlayPlanAsync(bm, plan);
        result.PlayerReachedWin = MagicPoolManager.I.GetSealedExoDionCounter(PlayerType.Player)
            >= SealedExoDionRules.WinStackCount;
        result.EnemyReachedWin = MagicPoolManager.I.GetSealedExoDionCounter(PlayerType.Enemy)
            >= SealedExoDionRules.WinStackCount;
        result.Stalemate = result.PlayerReachedWin && result.EnemyReachedWin;
        return result;
    }

    private static SealedExoDionEndPhasePlan BuildPlan(BattleManager bm)
    {
        var plan = new SealedExoDionEndPhasePlan();
        plan.PlayerMoves = CollectMoves(bm, PlayerType.Player);
        plan.EnemyMoves = CollectMoves(bm, PlayerType.Enemy);
        SimulateCounters(bm, plan);
        return plan;
    }

    private static List<SealedExoDionMovePlan> CollectMoves(BattleManager bm, PlayerType owner)
    {
        var hand = owner == PlayerType.Player ? bm.playerHand : bm.cpuHand;
        var moves = new List<SealedExoDionMovePlan>();
        if (hand == null) return moves;

        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            if (!SealedExoDionHandTracker.IsPendingEndPhaseMove(card, bm.cardDealer)) continue;
            moves.Add(new SealedExoDionMovePlan
            {
                HandIndex = i,
                CardInstanceId = card.GetInstanceID(),
            });
        }
        return moves;
    }

    private static void SimulateCounters(BattleManager bm, SealedExoDionEndPhasePlan plan)
    {
        int playerCounter = MagicPoolManager.I.GetSealedExoDionCounter(PlayerType.Player);
        int enemyCounter = MagicPoolManager.I.GetSealedExoDionCounter(PlayerType.Enemy);

        for (int i = 0; i < plan.PlayerMoves.Count; i++)
        {
            playerCounter++;
            if (playerCounter >= SealedExoDionRules.WinStackCount)
            {
                plan.PlayerReachedWin = true;
                break;
            }
        }

        for (int i = 0; i < plan.EnemyMoves.Count; i++)
        {
            enemyCounter++;
            if (enemyCounter >= SealedExoDionRules.WinStackCount)
            {
                plan.EnemyReachedWin = true;
                break;
            }
        }

        plan.PlayerCounterAfter = playerCounter;
        plan.EnemyCounterAfter = enemyCounter;
    }

    private static async Task PlayPlanAsync(BattleManager bm, SealedExoDionEndPhasePlan plan)
    {
        await RunOwnerMovesAsync(bm, PlayerType.Player);
        await RunOwnerMovesAsync(bm, PlayerType.Enemy);
    }

    private static async Task RunOwnerMovesAsync(BattleManager bm, PlayerType owner)
    {
        while (true)
        {
            if (MagicPoolManager.I.GetSealedExoDionCounter(owner) >= SealedExoDionRules.WinStackCount)
                break;

            CardData card = FindFirstPendingInHand(bm, owner);
            if (card == null) break;

            await ApplySingleMoveAsync(bm, owner, card);

            if (MagicPoolManager.I.GetSealedExoDionCounter(owner) >= SealedExoDionRules.WinStackCount)
                break;

            if (bm.HandRefill != null)
            {
                await bm.HandRefill.DrawOneAfterSealedExoDionPanelMoveAsync(
                    bm.playerHand, bm.cpuHand, owner);
            }
        }
    }

    private static CardData FindFirstPendingInHand(BattleManager bm, PlayerType owner)
    {
        var hand = owner == PlayerType.Player ? bm.playerHand : bm.cpuHand;
        if (hand == null) return null;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (SealedExoDionHandTracker.IsPendingEndPhaseMove(c, bm.cardDealer))
                return c;
        }

        return null;
    }

    private static async Task ApplySingleMoveAsync(BattleManager bm, PlayerType owner, CardData card)
    {
        var hand = owner == PlayerType.Player ? bm.playerHand : bm.cpuHand;
        if (hand == null || !hand.Contains(card)) return;

        SealedExoDionHandTracker.ConsumePending(card);

        var template = SealedExoDionRules.ResolvePoolTemplate(bm.cardDealer);
        var cardUi = card.cardUI;
        hand.Remove(card);

        if (owner == PlayerType.Player)
        {
            if (cardUi != null && BattleUIManager.I != null && card.cardImage != null)
            {
                RectTransform handFlyRt = cardUi.cardImage != null
                    ? cardUi.cardImage.rectTransform
                    : cardUi.transform as RectTransform;
                await BattleUIManager.I.PlayMagicFlyHandToPanelAsync(
                    card, handFlyRt, SealedExoDionRules.RightSlotIndex);
            }

            if (cardUi != null)
                Object.Destroy(cardUi.gameObject);
        }
        else if (cardUi != null)
        {
            Object.Destroy(cardUi.gameObject);
        }

        card.cardUI = null;
        MagicPoolManager.I.ApplySealedExoDionFromHand(card, owner, bm.cardDealer);
        DestroyHandInstanceIfRuntime(card, template);

        if (owner == PlayerType.Player)
            BattleUIManager.I?.UpdateMagicPanel();
        else
            BattleUIManager.I?.OnEnemyMagicPoolChanged();

        bm.UpdateTotalATKDEFDisplay();
    }

    private static void DestroyHandInstanceIfRuntime(CardData handInstance, CardData poolTemplate)
    {
        if (handInstance == null || poolTemplate == null) return;
        if (ReferenceEquals(handInstance, poolTemplate)) return;
        Object.Destroy(handInstance);
    }

    private sealed class SealedExoDionEndPhasePlan
    {
        public List<SealedExoDionMovePlan> PlayerMoves = new();
        public List<SealedExoDionMovePlan> EnemyMoves = new();
        public int PlayerCounterAfter;
        public int EnemyCounterAfter;
        public bool PlayerReachedWin;
        public bool EnemyReachedWin;
    }

    private sealed class SealedExoDionMovePlan
    {
        public int HandIndex;
        public int CardInstanceId;
    }
}
