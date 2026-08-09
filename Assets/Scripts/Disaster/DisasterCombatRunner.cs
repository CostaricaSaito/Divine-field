using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 天変地異の通常攻撃（防御フェーズあり）をプログラムから実行する。
/// </summary>
public static class DisasterCombatRunner
{
    public static async Task<bool> RunStrikeAsync(
        BattleManager bm,
        CardSequenceManager sequences,
        BattleProcessor processor,
        PlayerStatus attacker,
        PlayerStatus defender,
        CardData combatCard,
        Side displaySide,
        CancellationToken cancellationToken)
    {
        if (bm == null || combatCard == null || attacker == null || defender == null)
            return false;

        PresentCombatCard(bm, combatCard, displaySide);
        await Task.Delay(500, cancellationToken);

        var atkList = new List<CardData> { combatCard };
        bool attackerIsPlayer = ReferenceEquals(attacker, bm.GetPlayerStatus());

        DisasterCombatContext.Begin();
        try
        {
            if (attackerIsPlayer)
            {
                if (sequences == null) return false;
                return await sequences.ResolvePlayerAttackCombatAsync(
                    atkList, attacker, defender, bm.cpuHand, cancellationToken);
            }

            return await RunEnemyStrikeVsPlayerAsync(bm, processor, atkList, cancellationToken);
        }
        finally
        {
            DisasterCombatContext.End();
        }
    }

    private static void PresentCombatCard(BattleManager bm, CardData combatCard, Side displaySide)
    {
        BattleUIManager.I?.ClearCardDisplayPanelImmediate(displaySide);
        BattleUIManager.I?.ShowCardSheetsVisualOnlyBatch(new List<CardData> { combatCard }, displaySide);
        bm?.SetStatsDisplaySequenceCards(new List<CardData> { combatCard }, "攻撃", displaySide);
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
    }

    private static async Task<bool> RunEnemyStrikeVsPlayerAsync(
        BattleManager bm,
        BattleProcessor processor,
        List<CardData> atkList,
        CancellationToken cancellationToken)
    {
        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(
            primary, bm.GetEnemyStatus(), bm.GetPlayerStatus(), applyAttackerSmokePenalty: false);
        bool hit = HitRateRules.RollHit(finalPct);

        if (!hit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(bm.GetPlayerStatus());
            await DamagePopup.WaitAfterPopupLifetimeAsync(DamagePopup.DefaultFadeDurationIfUnknown, cancellationToken);
            return await bm.TryHandleDeathIfAnyAsync(cancellationToken) ? false : true;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float sec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(bm.GetPlayerStatus())
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(sec, cancellationToken);
        }

        bm.BeginDisasterPlayerDefensePhase(atkList);
        List<CardData> defs;
        try
        {
            defs = await bm.WaitForAdHocDefenseSubmitAsync(cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            bm.ClearDisasterPlayerDefenseWait();
            return false;
        }

        if (defs.Count == 0)
        {
            await processor.ResolveCombatAsync(
                atkList, (CardData)null, bm.GetEnemyStatus(), bm.GetPlayerStatus(), bm.playerHand, skipHitCheck: true);
        }
        else if (defs.Count == 1)
        {
            await processor.ResolveCombatAsync(
                atkList, defs[0], bm.GetEnemyStatus(), bm.GetPlayerStatus(), bm.playerHand, skipHitCheck: true);
        }
        else
        {
            await processor.ResolveCombatAsync(
                atkList, defs, bm.GetEnemyStatus(), bm.GetPlayerStatus(), bm.playerHand, skipHitCheck: true);
        }

        foreach (var d in defs)
        {
            if (d != null)
                processor.UseCard(d, bm.playerHand);
        }

        if (await bm.TryHandleDeathIfAnyAsync(cancellationToken))
            return false;

        return true;
    }
}
