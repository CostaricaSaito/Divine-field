using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 打ち払い：跳ね返しに似るが、攻撃の行き先が50%で元の防御側／攻撃側に分岐する。
/// </summary>
public static class ParryFlow
{
    private const float SlideDurationSec = 0.5f;
    /// <summary>
    /// プレイヤーが打ち払いのみを確定したあとのフロー。
    /// </summary>
    /// <returns>true のとき呼び出し元の <see cref="CardSequenceManager.StartCardSequenceAsync"/> は共有後処理をスキップする。</returns>
    public static async Task<bool> RunPlayerInitiatedAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        List<CardData> incomingAttackCards,
        CardData playerParryDefenseCard,
        CardSequenceManager cardSequenceManager,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || battleProcessor == null || incomingAttackCards == null || incomingAttackCards.Count == 0)
            return false;

        battleManager.ClearReflectionAttackTotalDisplay();

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();
        int incomingPower = battleProcessor.ComputeReflectionIncomingAttackPower(
            incomingAttackCards, enemy, player);

        bool sessionMagic = ReflectionRules.CanReflectMagic(incomingAttackCards);

        float parrySec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowParryIntroPopup(player)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (parrySec <= 0f) parrySec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(parrySec, cancellationToken);

        await Task.Delay(1000, cancellationToken);

        bool redirectToOriginalDefender = BattleRandom.Range(0, 2) == 0;

        if (redirectToOriginalDefender)
        {
            float sec2 = BattleUIManager.I != null
                ? BattleUIManager.I.ShowParryReturnToSelfPopup(player)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            if (sec2 <= 0f) sec2 = DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(sec2, cancellationToken);

            if (playerParryDefenseCard != null)
                BattleUIManager.I?.DestroyCardSheetForCardData(playerParryDefenseCard);

            List<CardData> secondPicks;
            try
            {
                secondPicks = await battleManager.WaitForParryRerunDefenseSubmitAsync(cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }

            if (secondPicks == null || secondPicks.Count == 0)
            {
                bool skipHit = battleManager.AttackerPublic == PlayerType.Enemy;
                await battleProcessor.ResolveCombatAsync(
                    incomingAttackCards, (CardData)null, enemy, player, battleManager.playerHand, skipHit);
                if (cardSequenceManager != null)
                    await cardSequenceManager.RunAfterCombatSharedCleanupAsync(cancellationToken);
                return true;
            }

            await cardSequenceManager.StartCardSequenceAsync(secondPicks, "防御", Side.Player, cancellationToken);
            return true;
        }

        if (playerParryDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetForCardData(playerParryDefenseCard);

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingAttackCards, slideTowardPlayer: true, SlideDurationSec, cancellationToken);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
        battleManager.SetReflectionAttackTotalDisplayAfterSlide(
            incomingAttackCards, totalAtkOnPlayerSide: true, enemy, enemy, incomingPower);

        try
        {
            await PhysicalReflectionFlow.RunReflectionChainLoopAsync(
                battleManager,
                battleProcessor,
                handRefill,
                enemyAI,
                incomingAttackCards,
                incomingPower,
                PlayerType.Enemy,
                sessionMagic,
                cancellationToken,
                enemy,
                enemy);
        }
        finally
        {
            battleManager.ClearReflectionAttackTotalDisplay();
        }

        return false;
    }

    /// <summary>敵が打ち払いでプレイヤー攻撃を処理する。</summary>
    public static async Task RunEnemyDefenderParriesPlayerAttackAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        List<CardData> incomingPlayerAttackCards,
        CardData enemyParryDefenseCard,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || battleProcessor == null || incomingPlayerAttackCards == null || incomingPlayerAttackCards.Count == 0)
            return;

        battleManager.ClearReflectionAttackTotalDisplay();

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();

        if (enemyParryDefenseCard != null)
        {
            handRefill?.RecordEnemyUse(enemyParryDefenseCard);
            battleProcessor.UseCard(enemyParryDefenseCard, battleManager.cpuHand);
        }

        int incomingPower = battleProcessor.ComputeReflectionIncomingAttackPower(
            incomingPlayerAttackCards, player, enemy);

        bool sessionMagic = ReflectionRules.CanReflectMagic(incomingPlayerAttackCards);

        float parrySec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowParryIntroPopup(enemy)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (parrySec <= 0f) parrySec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(parrySec, cancellationToken);

        await Task.Delay(1000, cancellationToken);

        bool redirectToOriginalDefender = BattleRandom.Range(0, 2) == 0;

        if (redirectToOriginalDefender)
        {
            float sec2 = BattleUIManager.I != null
                ? BattleUIManager.I.ShowParryReturnToSelfPopup(enemy)
                : DamagePopup.DefaultFadeDurationIfUnknown;
            if (sec2 <= 0f) sec2 = DamagePopup.DefaultFadeDurationIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(sec2, cancellationToken);

            if (enemyParryDefenseCard != null)
                BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(enemyParryDefenseCard, Side.Enemy);

            ElementType atkEl = ElementHelper.GetCombinedElement(incomingPlayerAttackCards);
            CardData second = await enemyAI.ExecuteParryRerunDefenseSelectAsync(
                battleManager.cpuHand, atkEl, incomingPlayerAttackCards, enemyParryDefenseCard);

            if (second != null && ParryRules.RequiresParryExclusiveLock(second, incomingPlayerAttackCards))
            {
                await RunEnemyDefenderParriesPlayerAttackAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    enemyAI,
                    incomingPlayerAttackCards,
                    second,
                    cancellationToken);
                return;
            }

            if (second != null)
            {
                BattleUIManager.I?.ShowEnemyDefenseCardPresentation(second);
                SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
                await Task.Delay(500, cancellationToken);
            }

            bool showEnemyYurusu = second == null && BattleUIManager.I != null;
            using (YurusuDisplayScope.ShowIf(showEnemyYurusu))
            {
                await battleProcessor.ResolveCombatAsync(
                    incomingPlayerAttackCards,
                    second,
                    player,
                    enemy,
                    battleManager.cpuHand,
                    skipHitCheck: true);
            }

            if (second != null)
            {
                handRefill?.RecordEnemyUse(second);
                battleProcessor.UseCard(second, battleManager.cpuHand);
            }
            return;
        }

        if (enemyParryDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(enemyParryDefenseCard, Side.Enemy);

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingPlayerAttackCards, slideTowardPlayer: false, SlideDurationSec, cancellationToken);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
        battleManager.SetReflectionAttackTotalDisplayAfterSlide(
            incomingPlayerAttackCards, totalAtkOnPlayerSide: false, player, player, incomingPower);

        try
        {
            await PhysicalReflectionFlow.RunReflectionChainLoopAsync(
                battleManager,
                battleProcessor,
                handRefill,
                enemyAI,
                incomingPlayerAttackCards,
                incomingPower,
                PlayerType.Player,
                sessionMagic,
                cancellationToken,
                player,
                player);
        }
        finally
        {
            battleManager.ClearReflectionAttackTotalDisplay();
        }
    }
}
