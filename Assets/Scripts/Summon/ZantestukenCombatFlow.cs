using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Zantestuken combat: skip defense phase, resolve with null defense, consume buff on successful hit.
/// </summary>
public static class ZantestukenCombatFlow
{
    /// <summary>
    /// DefensePhase entry: skip selection/Ordin slash and resolve immediately after the standard pre-defense delay.
    /// </summary>
    public static async Task<bool> TryResolveDefensePhaseSkipAsync(
        BattleManager bm,
        BattleProcessor processor,
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        List<CardData> defHand,
        CardData currentAttackCard,
        CancellationToken ct)
    {
        if (!OrdinUltimateRules.CanConsumeForOpponentStrike(atk, def, currentAttackCard))
            return false;
        if (attackCards == null || attackCards.Count == 0)
            return false;

        await Task.Delay(1000, ct);
        if (ct.IsCancellationRequested) return false;

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");

        if (!await ResolveUnblockableStrikeCoreAsync(bm, processor, attackCards, atk, def, defHand, ct))
            return false;

        if (await bm.TryHandleDeathIfAnyAsync(ct))
            return true;

        if (await bm.TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(ct))
            return true;

        await FinishDefensePhaseSkipAsync(bm, ct);
        return true;
    }

    /// <summary>
    /// Player attack sequence after a successful hit roll (ResolvePlayerAttackCombatAsync path).
    /// </summary>
    public static async Task<bool> TryResolveUnblockableStrikeAsync(
        BattleManager bm,
        BattleProcessor processor,
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        List<CardData> defHand,
        CardData currentAttackCard,
        CancellationToken ct,
        int dualBladeStrikeIndex = 0)
    {
        if (dualBladeStrikeIndex > 0) return false;
        if (!OrdinUltimateRules.CanConsumeForOpponentStrike(atk, def, currentAttackCard))
            return false;
        if (attackCards == null || attackCards.Count == 0)
            return false;

        return await ResolveUnblockableStrikeCoreAsync(bm, processor, attackCards, atk, def, defHand, ct);
    }

    private static async Task<bool> ResolveUnblockableStrikeCoreAsync(
        BattleManager bm,
        BattleProcessor processor,
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        List<CardData> defHand,
        CancellationToken ct)
    {
        if (bm == null || processor == null || atk == null || def == null)
            return false;
        if (!atk.HasZantestukenEffect())
            return false;

        float fadeSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowMessagePopupForTarget(
                def, OrdinUltimateRules.UnblockableMessage, OrdinUltimateRules.UnblockableMessageColor)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
        if (ct.IsCancellationRequested) return false;

        atk.ConsumeZantestukenEffect();
        BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());

        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(attackCards)
            && attackCards.Count == 1 && attackCards[0] != null)
        {
            await Task.Delay(DamagePopup.PreImmediateEffectDelayMs, ct);
            if (ct.IsCancellationRequested) return false;
            await processor.ResolveImmediateEffectAsync(attackCards[0], atk, def, ct);
        }
        else
        {
            await processor.ResolveCombatAsync(
                attackCards, (CardData)null, atk, def, defHand, skipHitCheck: true);
        }

        return true;
    }

    private static async Task FinishDefensePhaseSkipAsync(BattleManager bm, CancellationToken ct)
    {
        if (bm == null) return;

        bm.ClearMagicalExplosionComboMpPoolSnapshot();
        bm.ClearMillionDollarBazookaComboGpPoolSnapshot();
        bm.ClearTributeBloodHpPaidSnapshot();
        bm.ClearHammadnessRollSnapshot();
        BattleUIManager.I?.HideAllCardDetails();
        bm.ClearIncomingAttackForceNoneElement();
        bm.ClearStatsDisplaySequenceCards();
        bm.SetCurrentAttackCard(null);
        bm.SetSelectedDefenseCard(null);
        bm.UpdateTotalATKDEFDisplay();
        bm.SetGameState(GameState.CombatResolvePhase);
        await Task.Yield();
    }
}
