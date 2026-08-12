using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 天変地異の通常攻撃（防御フェーズあり）をプログラムから実行する。
/// </summary>
public static class DisasterCombatRunner
{
    private const int RampagePreCombatIntervalMs = 1000;
    private const float RampageSelfTargetSlideSec = 0.5f;

    /// <summary>
    /// 暴走斬鉄剣：Orchestrator 表示済みカードを前提に、1秒待機 →（自己対象時のみ反射スライド）→ 防御フェーズ。
    /// </summary>
    public static async Task<bool> RunRampageStrikeAsync(
        BattleManager bm,
        CardSequenceManager sequences,
        BattleProcessor processor,
        PlayerStatus attacker,
        PlayerStatus defender,
        CardData displayCard,
        CardData combatCard,
        Side triggerSide,
        bool targetSelf,
        CancellationToken cancellationToken)
    {
        if (bm == null || displayCard == null || combatCard == null || attacker == null || defender == null)
            return false;

        await Task.Delay(RampagePreCombatIntervalMs, cancellationToken);

        Side attackerSide = triggerSide;
        Side cardDisplaySide = targetSelf ? OppositeSide(triggerSide) : triggerSide;
        var visualList = new List<CardData> { displayCard };
        var combatList = new List<CardData> { combatCard };

        if (targetSelf && BattleUIManager.I != null)
        {
            bool slideTowardPlayer = triggerSide == Side.Enemy;
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                visualList, slideTowardPlayer, RampageSelfTargetSlideSec, cancellationToken);
        }

        DisasterCombatContext.SetCurrentStrike(visualList, cardDisplaySide, attackerSide);
        bm.SetStatsDisplaySequenceCards(visualList, "攻撃", cardDisplaySide);
        bm.UpdateTotalATKDEFDisplay();

        return await RunStrikeCombatCoreAsync(
            bm, sequences, processor, attacker, defender, combatList, cancellationToken);
    }

    public static async Task<bool> RunStrikeAsync(
        BattleManager bm,
        CardSequenceManager sequences,
        BattleProcessor processor,
        PlayerStatus attacker,
        PlayerStatus defender,
        CardData combatCard,
        Side cardDisplaySide,
        CancellationToken cancellationToken)
    {
        if (bm == null || combatCard == null || attacker == null || defender == null)
            return false;

        Side attackerSide = ReferenceEquals(attacker, bm.GetPlayerStatus()) ? Side.Player : Side.Enemy;
        PresentCombatCard(bm, combatCard, cardDisplaySide, attackerSide);
        await Task.Delay(500, cancellationToken);

        var atkList = new List<CardData> { combatCard };
        return await RunStrikeCombatCoreAsync(
            bm, sequences, processor, attacker, defender, atkList, cancellationToken);
    }

    private static async Task<bool> RunStrikeCombatCoreAsync(
        BattleManager bm,
        CardSequenceManager sequences,
        BattleProcessor processor,
        PlayerStatus attacker,
        PlayerStatus defender,
        List<CardData> atkList,
        CancellationToken cancellationToken)
    {
        bool attackerIsPlayer = ReferenceEquals(attacker, bm.GetPlayerStatus());
        bool selfTarget = ReferenceEquals(attacker, defender);

        DisasterCombatContext.Begin();
        try
        {
            if (selfTarget && attackerIsPlayer)
                return await RunPlayerSelfTargetStrikeAsync(bm, processor, atkList, attacker, cancellationToken);

            if (selfTarget)
                return await RunEnemySelfTargetStrikeAsync(bm, processor, atkList, cancellationToken);

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

    private static Side OppositeSide(Side side) => side == Side.Player ? Side.Enemy : Side.Player;

    private static void PresentCombatCard(
        BattleManager bm,
        CardData combatCard,
        Side cardDisplaySide,
        Side attackerSide)
    {
        var atkList = new List<CardData> { combatCard };
        DisasterCombatContext.SetCurrentStrike(atkList, cardDisplaySide, attackerSide);

        BattleUIManager.I?.ClearCardDisplayPanelImmediate(cardDisplaySide);
        BattleUIManager.I?.ShowCardSheetsVisualOnlyBatch(atkList, cardDisplaySide);
        bm?.SetStatsDisplaySequenceCards(atkList, "攻撃", cardDisplaySide);
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
    }

    private static async Task<bool> RunHitCheckAsync(
        BattleManager bm,
        List<CardData> atkList,
        PlayerStatus attacker,
        PlayerStatus defender,
        CancellationToken cancellationToken)
    {
        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(
            primary, attacker, defender, applyAttackerSmokePenalty: false);
        bool hit = HitRateRules.RollHit(finalPct);

        if (!hit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(defender);
            await DamagePopup.WaitAfterPopupLifetimeAsync(
                DamagePopup.DefaultFadeDurationIfUnknown, cancellationToken);
            return false;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float sec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(defender)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(sec, cancellationToken);
        }

        return true;
    }

    private static async Task<bool> RunPlayerSelfTargetStrikeAsync(
        BattleManager bm,
        BattleProcessor processor,
        List<CardData> atkList,
        PlayerStatus player,
        CancellationToken cancellationToken)
    {
        if (!await RunHitCheckAsync(bm, atkList, player, player, cancellationToken))
            return await bm.TryHandleDeathIfAnyAsync(cancellationToken) ? false : true;

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
                atkList, (CardData)null, player, player, bm.playerHand, skipHitCheck: true);
        }
        else if (defs.Count == 1)
        {
            await processor.ResolveCombatAsync(
                atkList, defs[0], player, player, bm.playerHand, skipHitCheck: true);
        }
        else
        {
            await processor.ResolveCombatAsync(
                atkList, defs, player, player, bm.playerHand, skipHitCheck: true);
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

    private static async Task<bool> RunEnemySelfTargetStrikeAsync(
        BattleManager bm,
        BattleProcessor processor,
        List<CardData> atkList,
        CancellationToken cancellationToken)
    {
        var enemy = bm.GetEnemyStatus();
        if (enemy == null) return false;

        if (!await RunHitCheckAsync(bm, atkList, enemy, enemy, cancellationToken))
            return await bm.TryHandleDeathIfAnyAsync(cancellationToken) ? false : true;

        ElementType attackElement = ElementHelper.GetCombinedElement(atkList);
        var defenseCard = await bm.GetEnemyAI().ExecuteDefenseSelectAsync(
            bm.cpuHand, attackElement, atkList);

        var defenseCards = new List<CardData>();
        if (defenseCard != null)
            defenseCards.Add(defenseCard);

        if (defenseCards.Count > 0)
            await BattleUIManager.I?.ShowEnemyDefenseCardsPresentationSequenceAsync(defenseCards);

        if (defenseCards.Count == 0)
        {
            await processor.ResolveCombatAsync(
                atkList, (CardData)null, enemy, enemy, bm.cpuHand, skipHitCheck: true);
        }
        else if (defenseCards.Count == 1)
        {
            await processor.ResolveCombatAsync(
                atkList, defenseCards[0], enemy, enemy, bm.cpuHand, skipHitCheck: true);
        }
        else
        {
            await processor.ResolveCombatAsync(
                atkList, defenseCards, enemy, enemy, bm.cpuHand, skipHitCheck: true);
        }

        foreach (var d in defenseCards)
        {
            if (d == null) continue;
            bm.HandRefill?.RecordEnemyUse(d);
            processor.UseCard(d, bm.cpuHand);
        }

        if (await bm.TryHandleDeathIfAnyAsync(cancellationToken))
            return false;

        return true;
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
