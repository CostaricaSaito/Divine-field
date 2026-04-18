using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 物理反射：プレイヤー防御で反射剣を確定したあとの臨時攻撃〜連鎖反射〜ダメージ解決。
/// </summary>
public static class PhysicalReflectionFlow
{
    private const float SlideDurationSec = 0.5f;

    /// <summary>
    /// プレイヤーが反射剣のみを確定し、手札から既に使用済みのときに呼ぶ。
    /// </summary>
    public static async Task RunPlayerInitiatedAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        List<CardData> incomingAttackCards,
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

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingAttackCards, slideTowardPlayer: true, SlideDurationSec, cancellationToken);

        PlayerType defenderSide = PlayerType.Enemy;

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

            // 防御側がプレイヤー（敵からの再反射後）
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
