using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 防御側の「宝玉」臨時効果：第1段ダメージ通過かつ
/// 闇2段・状態異常付与の後。CardDisplay クリア前に SE+点滅し、その後単体表示で効果解決。
/// </summary>
public static class OrbDefenseReactionFlow
{
    public const string OrbGaugeRecoverySe = "Assets/SE/ゲージ回復2.mp3";
    public const float OrbTintFadeInSec = 0.5f;
    public const float OrbTintFadeOutSec = 0.5f;
    public const int AquatideInterstitialDelayMs = 500;

    public static async Task PresentReactionsAsync(
        BattleManager bm,
        BattleProcessor battleProcessor,
        IReadOnlyList<CardData> orbs,
        int firstPhaseDamageB,
        PlayerStatus originalAttacker,
        PlayerStatus originalDefender,
        CancellationToken cancellationToken = default)
    {
        if (bm == null || battleProcessor == null || orbs == null || orbs.Count == 0) return;
        if (firstPhaseDamageB <= 0) return;

        bool defenderIsPlayer = ReferenceEquals(originalDefender, bm.GetPlayerStatus());
        Side displaySide = defenderIsPlayer ? Side.Player : Side.Enemy;

        for (int i = 0; i < orbs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var orb = orbs[i];
            if (orb == null) continue;
            if (orb.orbReactionRule == null) continue;

            SoundEffectPlayer.I?.Play(OrbGaugeRecoverySe);

            if (BattleUIManager.I != null
                && BattleUIManager.I.TryGetCardSheetDisplayForCardData(orb, out var sh))
            {
                Color c = ElementHelper.GetElementColor(orb.element);
                try
                {
                    await sh.PlayOrbElementTintFlashAsync(c, OrbTintFadeInSec, OrbTintFadeOutSec, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            if (orb.orbReactionRule is OrbOfHellfireRuleSO)
            {
                var one = new List<CardData> { orb };
                int atkDisplay = battleProcessor.GetOrbCounterDisplayedAttackPower(
                    one, firstPhaseDamageB, originalDefender, originalAttacker);
                bool totalAtkOnPlayer = ReferenceEquals(originalDefender, bm.GetPlayerStatus());
                bm.SetReflectionAttackTotalDisplayAfterSlide(
                    one, totalAtkOnPlayer, originalDefender, originalAttacker, atkDisplay);
                if (ReferenceEquals(originalDefender, bm.GetPlayerStatus()))
                    bm.SetSuppressEnemyStaleAttackerInTotalByOrb(true);
            }

            // Destroy が同一フレーム内で溜まると古いシートが残るため即時クリア
            BattleUIManager.I?.ClearAllCardDisplaysAndSelectionImmediate();
            BattleUIManager.I?.ShowCardSheetsVisualOnlyBatch(new List<CardData> { orb }, displaySide);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            bm.SetStatsDisplaySequenceCards(new List<CardData> { orb }, "防御", displaySide);

            if (orb.orbReactionRule is OrbOfHellfireRuleSO)
            {
                await RunHellfireCounterAsync(
                    bm, battleProcessor, orb, firstPhaseDamageB, originalAttacker, originalDefender, cancellationToken);
            }
            else if (orb.orbReactionRule is OrbOfAquatideRuleSO)
            {
                await Task.Delay(AquatideInterstitialDelayMs, cancellationToken);
                if (originalDefender != null && !originalDefender.IsDead())
                {
                    int heal = Mathf.Min(originalDefender.maxHP, firstPhaseDamageB * 2);
                    await battleProcessor.ApplyOrbHpRecoveryAsync(orb, originalDefender, heal, cancellationToken);
                }
            }

            BattleUIManager.I?.HideAllCardDetails();
            bm.ClearStatsDisplaySequenceCards();
            bm.ClearReflectionAttackTotalDisplay();
        }
    }

    private static async Task RunHellfireCounterAsync(
        BattleManager bm,
        BattleProcessor battleProcessor,
        CardData orb,
        int firstPhaseDamageB,
        PlayerStatus originalAttacker,
        PlayerStatus originalDefender,
        CancellationToken cancellationToken)
    {
        if (originalAttacker == null || originalDefender == null || orb == null)
            return;

        var counterAtt = originalDefender;
        var target = originalAttacker;
        var attackSnap = new List<CardData> { orb };

        if (ReferenceEquals(target, bm.GetPlayerStatus()))
        {
            if (target != null && target.IsDead())
            {
                await battleProcessor.ResolveOrbCounterCombatAsync(
                    attackSnap, firstPhaseDamageB, null, counterAtt, target, bm.playerHand, false);
                return;
            }

            List<CardData> picks;
            try
            {
                picks = await bm.WaitForReflectionChainDefenseAsync(attackSnap, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (picks == null || picks.Count == 0)
            {
                await battleProcessor.ResolveOrbCounterCombatAsync(
                    attackSnap, firstPhaseDamageB, null, counterAtt, target, bm.playerHand, false);
                return;
            }

            CardData card = picks[0];
            if (card == null) return;
            if (card.cardType == CardType.Magic && bm.Sequences != null)
                await bm.Sequences.ApplyMagicCardToPoolForReflectionOrParryDefenseAsync(card, cancellationToken);
            else
            {
                int slotIndex = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
                if (slotIndex >= 0) bm.HandRefill?.RecordPlayerUseSlot(slotIndex);
                battleProcessor.UseCard(card, bm.playerHand);
            }

            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            bm.SetStatsDisplaySequenceCards(new List<CardData> { card }, "防御", Side.Player);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            await battleProcessor.ResolveOrbCounterCombatAsync(
                attackSnap, firstPhaseDamageB, card, counterAtt, target, bm.playerHand, false);
            bm.ClearStatsDisplaySequenceCards();
        }
        else
        {
            if (bm.GetEnemyAI() == null) return;
            var pick = await bm.GetEnemyAI().ExecuteDefenseSelectAsync(
                bm.cpuHand, ElementHelper.GetCombinedElement(attackSnap), attackSnap);

            if (pick != null)
            {
                bm.HandRefill?.RecordEnemyUse(pick);
                battleProcessor.UseCard(pick, bm.cpuHand);
                BattleUIManager.I?.ShowEnemyDefenseCardPresentation(pick);
                bm.SetStatsDisplaySequenceCards(new List<CardData> { pick }, "防御", Side.Enemy);
                SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            }
            else
                bm.SetStatsDisplaySequenceCards(new List<CardData>(), "防御", Side.Enemy);
            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            bool showEnemyYurusu = pick == null && BattleUIManager.I != null;
            if (showEnemyYurusu)
                BattleUIManager.I.ShowYurusuDisplay();
            try
            {
                await battleProcessor.ResolveOrbCounterCombatAsync(
                    attackSnap, firstPhaseDamageB, pick, counterAtt, target, bm.cpuHand, false);
            }
            finally
            {
                if (showEnemyYurusu)
                    BattleUIManager.I?.HideYurusuButton();
            }

            bm.ClearStatsDisplaySequenceCards();
        }
    }
}
