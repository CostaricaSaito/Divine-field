using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Shining Barrier defense intercept: strip incoming element, then re-open defense select.
/// </summary>
public static class ShiningBarrierDefenseFlow
{
    public static async Task RunPlayerInterceptAsync(
        BattleManager bm,
        CardData barrierCard,
        CancellationToken ct)
    {
        if (bm == null || barrierCard == null) return;

        var defender = bm.GetPlayerStatus();
        if (defender == null) return;

        await ShiningBarrierPresentation.RunAsync(defender, ct);
        if (ct.IsCancellationRequested) return;

        BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(barrierCard, Side.Player);

        bm.SetIncomingAttackForceNoneElement(true);
        if (bm.IsOnlineMatch)
        {
            int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded + bm.SummonTurnCounters.EnemyOwnTurnsEnded;
            NetworkBattleBridge.SendShiningBarrierApplied(turnTag);
        }

        bm.SetSelectedDefenseCard(null);
        BattleUIManager.I?.ClearAllSelections();
        bm.UpdateTotalATKDEFDisplay();

        await BeginPlayerReDefenseAsync(bm, ct);
    }

    /// <summary>
    /// Reflection-chain / hellfire counter: player uses Shining Barrier, then re-opens ad-hoc defense.
    /// </summary>
    public static async Task RunPlayerAdHocBarrierInterceptAsync(
        BattleManager bm,
        BattleProcessor processor,
        HandRefillService handRefill,
        CardData barrierCard,
        CancellationToken ct)
    {
        if (bm == null || barrierCard == null) return;

        var defender = bm.GetPlayerStatus();
        if (defender == null) return;

        BattleUIManager.I?.ShowCardDetail(barrierCard, Side.Player);
        await ShiningBarrierPresentation.RunAsync(defender, ct);
        if (ct.IsCancellationRequested) return;

        BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(barrierCard, Side.Player);

        bm.SetIncomingAttackForceNoneElement(true);
        if (bm.IsOnlineMatch)
        {
            int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded + bm.SummonTurnCounters.EnemyOwnTurnsEnded;
            NetworkBattleBridge.SendShiningBarrierApplied(turnTag);
        }

        if (barrierCard.cardType == CardType.Magic && bm.Sequences != null)
            await bm.Sequences.ApplyMagicCardToPoolForReflectionOrParryDefenseAsync(barrierCard, ct);
        else
        {
            int slotIndex = barrierCard.cardUI != null ? barrierCard.cardUI.transform.GetSiblingIndex() : -1;
            if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);
            processor?.UseCard(barrierCard, bm.playerHand);
        }

        bm.ClearStatsDisplaySequenceCards();
        BattleUIManager.I?.ClearAllSelections();
        bm.UpdateTotalATKDEFDisplay();
    }

    /// <summary>
    /// Enemy Shining Barrier: strip incoming element and AI re-selects (no combat resolve).
    /// </summary>
    public static async Task<CardData> RunEnemyStripIncomingAndReSelectAsync(
        BattleManager bm,
        BattleProcessor processor,
        HandRefillService handRefill,
        CardData barrierCard,
        List<CardData> attackCards,
        List<CardData> defHand,
        EnemyAI enemyAI,
        bool skipInitialBarrierDisplay,
        CancellationToken ct)
    {
        if (bm == null || barrierCard == null || attackCards == null) return null;

        var defender = bm.GetEnemyStatus();
        if (defender == null) return null;

        if (!skipInitialBarrierDisplay)
        {
            await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(
                new List<CardData> { barrierCard });
            if (ct.IsCancellationRequested) return null;
        }

        await ShiningBarrierPresentation.RunAsync(defender, ct);
        if (ct.IsCancellationRequested) return null;

        BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(barrierCard, Side.Enemy);

        bm.SetIncomingAttackForceNoneElement(true);
        if (bm.IsOnlineMatch)
        {
            int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded + bm.SummonTurnCounters.EnemyOwnTurnsEnded;
            NetworkBattleBridge.SendShiningBarrierApplied(turnTag);
        }

        handRefill?.RecordEnemyUse(barrierCard);
        processor?.UseCard(barrierCard, defHand);
        bm.UpdateTotalATKDEFDisplay();

        ElementType atkEl = ElementHelper.GetIncomingAttackElement(attackCards);
        return await enemyAI.ExecuteDefenseSelectAsync(defHand, atkEl, attackCards);
    }

    public static async Task<bool> RunEnemyInterceptAsync(
        BattleManager bm,
        BattleProcessor processor,
        HandRefillService handRefill,
        CardData barrierCard,
        List<CardData> attackCards,
        List<CardData> defHand,
        EnemyAI enemyAI,
        CancellationToken ct,
        bool skipInitialBarrierDisplay = false)
    {
        if (bm == null || barrierCard == null || attackCards == null) return false;

        var defender = bm.GetEnemyStatus();
        if (defender == null) return false;

        CardData second = await RunEnemyStripIncomingAndReSelectAsync(
            bm, processor, handRefill, barrierCard, attackCards, defHand, enemyAI,
            skipInitialBarrierDisplay, ct);
        if (ct.IsCancellationRequested) return false;

        bm.SetSelectedDefenseCard(second);

        if (second != null)
        {
            await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(
                new List<CardData> { second });
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            await Task.Delay(500, ct);
        }

        var atk = bm.GetPlayerStatus();
        bool showYurusu = second == null && BattleUIManager.I != null;
        using (YurusuDisplayScope.ShowIf(showYurusu))
        {
            await processor.ResolveCombatAsync(attackCards, second, atk, defender, defHand, skipHitCheck: true);
        }

        if (second != null)
        {
            handRefill?.RecordEnemyUse(second);
            processor.UseCard(second, defHand);
        }

        return true;
    }

    public static void ApplyForceNoneFromNetwork()
    {
        BattleManager.I?.SetIncomingAttackForceNoneElement(true);
        BattleManager.I?.UpdateTotalATKDEFDisplay();
    }

    private static async Task BeginPlayerReDefenseAsync(BattleManager bm, CancellationToken ct)
    {
        BattleUIManager.I?.HidePlayerCardDetails();
        bm.ClearCardStatsSequenceOnly();
        bm.UpdateTotalATKDEFDisplay();

        await Task.Delay(300, ct);
        if (ct.IsCancellationRequested) return;

        var incoming = bm.GetAttackCardsForCombatPublic();
        if (incoming != null && incoming.Count > 0)
            bm.SetEnemyIncomingAttackDisplay(incoming);

        await Task.Delay(500, ct);
        if (ct.IsCancellationRequested) return;

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");
        Debug.Log("[ShiningBarrierDefenseFlow] Re-open defense select after Shining Barrier");
        BattleUIManager.I?.SyncRestraintHeavyOverlay();

        bm.SetSelectedDefenseCard(null);
        bm.ResetPlayerDefenseUseButtonLocks();
        BattleUIManager.I?.SetHandClickable(true);
        bm.RefreshPlayerDefensePhaseInteractivity();
        BattleUIManager.I?.RefreshMagicCardInteractivity(bm.playerHand);
        bm.TryAutoPassPlayerDefenseIfChantingArchMagic();
    }
}
