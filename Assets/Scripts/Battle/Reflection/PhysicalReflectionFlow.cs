using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 物理反射：反射剣確定後の臨時攻撃〜連鎖反射〜ダメージ解決。
/// ・プレイヤー防御（敵の攻撃を跳ね返す）
/// ・敵防御（こちらの攻撃を跳ね返す）
/// </summary>
public static class PhysicalReflectionFlow
{
    private const float SlideDurationSec = 0.5f;

    /// <summary>
    /// プレイヤーが反射剣のみを確定し、手札から既に使用済みのときに呼ぶ（敵の攻撃を反射）。
    /// </summary>
    public static async Task RunPlayerInitiatedAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        List<CardData> incomingAttackCards,
        CardData playerReflectionDefenseCard,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || battleProcessor == null || incomingAttackCards == null || incomingAttackCards.Count == 0)
            return;

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();
        int incomingPower = battleProcessor.ComputeReflectionIncomingAttackPower(
            incomingAttackCards, enemy, player);

        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowReflectionBouncePopup(player)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await Task.Delay(TimeSpan.FromSeconds(bounceSec), cancellationToken);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);

        if (playerReflectionDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetForCardData(playerReflectionDefenseCard);

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingAttackCards, slideTowardPlayer: true, SlideDurationSec, cancellationToken);

        await RunReflectionChainLoopAsync(
            battleManager,
            battleProcessor,
            handRefill,
            enemyAI,
            incomingAttackCards,
            incomingPower,
            PlayerType.Enemy,
            cancellationToken);
    }

    /// <summary>
    /// 敵が反射剣でこちらの攻撃を跳ね返す（プレイヤー攻撃 → 敵防御の AI 選択後）。
    /// 手札の反射剣は未使用のため、ここで RecordEnemyUse / UseCard する。
    /// </summary>
    public static async Task RunEnemyDefenderReflectsPlayerAttackAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        List<CardData> incomingPlayerAttackCards,
        CardData enemyReflectionDefenseCard,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || battleProcessor == null || incomingPlayerAttackCards == null || incomingPlayerAttackCards.Count == 0)
            return;

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();

        if (enemyReflectionDefenseCard != null)
        {
            handRefill?.RecordEnemyUse(enemyReflectionDefenseCard);
            battleProcessor.UseCard(enemyReflectionDefenseCard, battleManager.cpuHand);
        }

        int incomingPower = battleProcessor.ComputeReflectionIncomingAttackPower(
            incomingPlayerAttackCards, player, enemy);

        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowReflectionBouncePopup(enemy)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await Task.Delay(TimeSpan.FromSeconds(bounceSec), cancellationToken);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);

        if (enemyReflectionDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetForCardData(enemyReflectionDefenseCard);

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingPlayerAttackCards, slideTowardPlayer: false, SlideDurationSec, cancellationToken);

        await RunReflectionChainLoopAsync(
            battleManager,
            battleProcessor,
            handRefill,
            enemyAI,
            incomingPlayerAttackCards,
            incomingPower,
            PlayerType.Player,
            cancellationToken);
    }

    private static async Task RunReflectionChainLoopAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        List<CardData> incomingAttackCards,
        int incomingPower,
        PlayerType defenderSide,
        CancellationToken cancellationToken)
    {
        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (defenderSide == PlayerType.Enemy)
            {
                ElementType atkEl = ElementHelper.GetCombinedElement(incomingAttackCards);
                CardData pick = await enemyAI.ExecuteDefenseSelectAsync(
                    battleManager.cpuHand, atkEl, incomingAttackCards);

                if (pick != null && ReflectionRules.IsPhysicalReflectionCard(pick)
                    && ReflectionRules.CanReflectPhysical(incomingAttackCards))
                {
                    BattleUIManager.I?.ShowCardDetail(pick, Side.Enemy);
                    SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
                    await Task.Delay(500, cancellationToken);

                    float sec = BattleUIManager.I != null
                        ? BattleUIManager.I.ShowReflectionBouncePopup(enemy)
                        : DamagePopup.DefaultFadeDurationIfUnknown;
                    if (sec <= 0f) sec = DamagePopup.DefaultFadeDurationIfUnknown;
                    await Task.Delay(TimeSpan.FromSeconds(sec), cancellationToken);
                    await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);

                    BattleUIManager.I?.DestroyCardSheetForCardData(pick);

                    if (BattleUIManager.I != null)
                        await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                            incomingAttackCards, slideTowardPlayer: false, SlideDurationSec, cancellationToken);

                    handRefill?.RecordEnemyUse(pick);
                    battleProcessor.UseCard(pick, battleManager.cpuHand);

                    defenderSide = PlayerType.Player;
                    continue;
                }

                if (pick != null)
                {
                    BattleUIManager.I?.ShowCardDetail(pick, Side.Enemy);
                    SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
                    await Task.Delay(500, cancellationToken);
                }

                await battleProcessor.ResolveReflectedCombatAsync(
                    incomingAttackCards,
                    incomingPower,
                    pick,
                    player,
                    enemy,
                    battleManager.cpuHand,
                    skipHitCheck: true);

                if (pick != null)
                {
                    handRefill?.RecordEnemyUse(pick);
                    battleProcessor.UseCard(pick, battleManager.cpuHand);
                }
                return;
            }

            List<CardData> picks = await battleManager.WaitForReflectionChainDefenseAsync(
                incomingAttackCards, cancellationToken);

            if (picks == null || picks.Count == 0)
            {
                await battleProcessor.ResolveReflectedCombatAsync(
                    incomingAttackCards,
                    incomingPower,
                    null,
                    enemy,
                    player,
                    battleManager.playerHand,
                    skipHitCheck: true);
                return;
            }

            CardData card = picks[0];
            if (ReflectionRules.IsPhysicalReflectionCard(card)
                && ReflectionRules.CanReflectPhysical(incomingAttackCards))
            {
                int slotIndex = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
                if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);
                battleProcessor.UseCard(card, battleManager.playerHand);

                BattleUIManager.I?.ShowCardDetail(card, Side.Player);
                SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
                await Task.Delay(500, cancellationToken);

                float sec2 = BattleUIManager.I != null
                    ? BattleUIManager.I.ShowReflectionBouncePopup(player)
                    : DamagePopup.DefaultFadeDurationIfUnknown;
                if (sec2 <= 0f) sec2 = DamagePopup.DefaultFadeDurationIfUnknown;
                await Task.Delay(TimeSpan.FromSeconds(sec2), cancellationToken);
                await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);

                BattleUIManager.I?.DestroyCardSheetForCardData(card);

                if (BattleUIManager.I != null)
                    await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                        incomingAttackCards, slideTowardPlayer: true, SlideDurationSec, cancellationToken);

                defenderSide = PlayerType.Enemy;
                continue;
            }

            int slot = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
            if (slot >= 0) handRefill?.RecordPlayerUseSlot(slot);
            battleProcessor.UseCard(card, battleManager.playerHand);

            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            await Task.Delay(500, cancellationToken);

            await battleProcessor.ResolveReflectedCombatAsync(
                incomingAttackCards,
                incomingPower,
                card,
                enemy,
                player,
                battleManager.playerHand,
                skipHitCheck: true);
            return;
        }
    }
}
