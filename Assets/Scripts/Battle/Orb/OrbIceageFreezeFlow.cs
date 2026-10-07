using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 氷結／暗黒／聖光など：即時効果 incoming として状態異常付与。完全反射のみ跳ね返し可能。
/// </summary>
public static class OrbIceageFreezeFlow
{
    public static async Task RunAsync(
        BattleManager bm,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        CardData orb,
        PlayerStatus orbOwner,
        PlayerStatus freezeTarget,
        CancellationToken ct)
    {
        if (bm == null || battleProcessor == null || orb == null || orbOwner == null || freezeTarget == null)
            return;

        var incoming = new List<CardData> { orb };
        if (!CardRules.IncomingRequiresFullOnlyReactiveDefense(incoming))
        {
            Debug.LogWarning("[OrbIceageFreezeFlow] Orb is not configured as immediate status incoming.");
            await ApplyFreezeDirectAsync(battleProcessor, orb, orbOwner, freezeTarget, ct);
            return;
        }

        if (ReferenceEquals(freezeTarget, bm.GetPlayerStatus()))
            await RunPlayerVictimAsync(bm, battleProcessor, handRefill, incoming, orb, orbOwner, freezeTarget, ct);
        else
            await RunEnemyVictimAsync(bm, battleProcessor, handRefill, incoming, orb, orbOwner, freezeTarget, ct);
    }

    private static async Task RunPlayerVictimAsync(
        BattleManager bm,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> incoming,
        CardData orb,
        PlayerStatus orbOwner,
        PlayerStatus playerVictim,
        CancellationToken ct)
    {
        CardData finalPick = null;
        while (true)
        {
            List<CardData> picks;
            try
            {
                picks = await bm.WaitForReflectionChainDefenseAsync(incoming, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (picks == null || picks.Count == 0)
            {
                await ApplyFreezeDirectAsync(battleProcessor, orb, orbOwner, playerVictim, ct);
                return;
            }

            if (picks[0] == null) return;

            if (ShiningBarrierRules.IsBarrierOnlySelection(picks))
            {
                await ShiningBarrierDefenseFlow.RunPlayerAdHocBarrierInterceptAsync(
                    bm, battleProcessor, handRefill, picks[0], ct);
                if (ct.IsCancellationRequested) return;
                continue;
            }

            finalPick = picks[0];
            break;
        }

        if (await TryRunImmediateFullReflectionAsync(
                bm, battleProcessor, handRefill, incoming, finalPick, orbOwner, playerVictim, ct))
            return;

        ConsumePlayerDefensePick(bm, battleProcessor, handRefill, finalPick);
        await ApplyFreezeDirectAsync(battleProcessor, orb, orbOwner, playerVictim, ct);
    }

    private static async Task RunEnemyVictimAsync(
        BattleManager bm,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> incoming,
        CardData orb,
        PlayerStatus orbOwner,
        PlayerStatus enemyVictim,
        CancellationToken ct)
    {
        if (bm.GetEnemyAI() == null)
        {
            await ApplyFreezeDirectAsync(battleProcessor, orb, orbOwner, enemyVictim, ct);
            return;
        }

        ElementType element = ElementHelper.GetIncomingAttackElement(incoming);
        CardData pick = await bm.GetEnemyAI().ExecuteDefenseSelectAsync(
            bm.cpuHand, element, incoming);

        while (pick != null && ShiningBarrierRules.IsShiningBarrierCard(pick))
        {
            pick = await ShiningBarrierDefenseFlow.RunEnemyStripIncomingAndReSelectAsync(
                bm,
                battleProcessor,
                handRefill,
                pick,
                incoming,
                bm.cpuHand,
                bm.GetEnemyAI(),
                skipInitialBarrierDisplay: false,
                ct);
            if (ct.IsCancellationRequested) return;
        }

        if (pick != null
            && ReflectionRules.CanReflectIncoming(pick, incoming)
            && ReflectionRules.ShouldUseImmediateEffectReflectionFlow(incoming))
        {
            await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, ct);
            await ImmediateEffectReflectionFlow.RunEnemyDefenderReflectsPlayerImmediateAsync(
                bm,
                battleProcessor,
                handRefill,
                incoming,
                pick,
                orbOwner,
                ct);
            EnemyDefenseSelectionHelper.ConsumeEnemyDefenseCards(
                new List<CardData> { pick }, bm, battleProcessor, handRefill);
            return;
        }

        if (pick != null)
        {
            EnemyDefenseSelectionHelper.ConsumeEnemyDefenseCards(
                new List<CardData> { pick }, bm, battleProcessor, handRefill);
        }

        await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, ct);
        await ApplyFreezeDirectAsync(battleProcessor, orb, orbOwner, enemyVictim, ct);
    }

    private static async Task<bool> TryRunImmediateFullReflectionAsync(
        BattleManager bm,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> incoming,
        CardData defensePick,
        PlayerStatus orbOwner,
        PlayerStatus playerVictim,
        CancellationToken ct)
    {
        if (defensePick == null) return false;
        if (!ReflectionRules.CanReflectIncoming(defensePick, incoming)) return false;
        if (!ReflectionRules.ShouldUseImmediateEffectReflectionFlow(incoming)) return false;

        await ImmediateEffectReflectionFlow.RunPlayerInitiatedAsync(
            bm,
            battleProcessor,
            handRefill,
            incoming,
            defensePick,
            orbOwner,
            playerVictim,
            ct,
            reflectionCardAlreadyConsumed: false);
        return true;
    }

    private static void ConsumePlayerDefensePick(
        BattleManager bm,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        CardData defenseCard)
    {
        if (defenseCard == null) return;
        if (defenseCard.cardType == CardType.Magic && bm.Sequences != null)
        {
            _ = bm.Sequences.ApplyMagicCardToPoolForReflectionOrParryDefenseAsync(
                defenseCard, CancellationToken.None);
            return;
        }

        int slotIndex = defenseCard.cardUI != null ? defenseCard.cardUI.transform.GetSiblingIndex() : -1;
        if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);
        battleProcessor.UseCard(defenseCard, bm.playerHand);
    }

    private static async Task ApplyFreezeDirectAsync(
        BattleProcessor battleProcessor,
        CardData orb,
        PlayerStatus user,
        PlayerStatus target,
        CancellationToken ct)
    {
        await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, ct);
        await battleProcessor.ResolveImmediateEffectAsync(orb, user, target, ct);
    }
}
