using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 物理・魔法反射：反射防御カード確定後の臨時攻撃〜連鎖反射〜ダメージ解決（フロー共通）。
/// ・プレイヤー防御（敵の攻撃を跳ね返す）
/// ・敵防御（こちらの攻撃を跳ね返す）
/// </summary>
public static class PhysicalReflectionFlow
{
    private const float SlideDurationSec = 0.5f;

    private static bool IsContinuingReflectionChain(CardData pick, IReadOnlyList<CardData> incomingAttackCards)
    {
        if (pick == null || incomingAttackCards == null || incomingAttackCards.Count == 0) return false;
        if (ReflectionRules.IsPhysicalReflectionCard(pick) && ReflectionRules.CanReflectPhysical(incomingAttackCards))
            return true;
        if (ReflectionRules.IsMagicReflectionCard(pick) && ReflectionRules.CanReflectMagic(incomingAttackCards))
            return true;
        return false;
    }

    /// <summary>
    /// 跳ね返し対象の攻撃カードと同一 CardData 参照か。全シート削除すると攻撃表示まで消えるため判定に使う。
    /// </summary>
    private static bool IncomingAttackContainsCardReference(IReadOnlyList<CardData> incoming, CardData card)
    {
        if (incoming == null || card == null) return false;
        for (int i = 0; i < incoming.Count; i++)
        {
            if (ReferenceEquals(incoming[i], card)) return true;
        }
        return false;
    }

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

        battleManager.ClearReflectionAttackTotalDisplay();

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();
        int incomingPower = battleProcessor.ComputeReflectionIncomingAttackPower(
            incomingAttackCards, enemy, player);

        bool sessionMagic = ReflectionRules.CanReflectMagic(incomingAttackCards);
        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowReflectionBouncePopup(player, sessionMagic)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(bounceSec, cancellationToken);

        if (playerReflectionDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetForCardData(playerReflectionDefenseCard);

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingAttackCards, slideTowardPlayer: true, SlideDurationSec, cancellationToken);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
        battleManager.SetReflectionAttackTotalDisplayAfterSlide(
            incomingAttackCards, totalAtkOnPlayerSide: true, enemy, enemy);

        try
        {
            await RunReflectionChainLoopAsync(
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

        battleManager.ClearReflectionAttackTotalDisplay();

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();

        if (enemyReflectionDefenseCard != null)
        {
            handRefill?.RecordEnemyUse(enemyReflectionDefenseCard);
            battleProcessor.UseCard(enemyReflectionDefenseCard, battleManager.cpuHand);
        }

        int incomingPower = battleProcessor.ComputeReflectionIncomingAttackPower(
            incomingPlayerAttackCards, player, enemy);

        bool sessionMagic = ReflectionRules.CanReflectMagic(incomingPlayerAttackCards);
        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowReflectionBouncePopup(enemy, sessionMagic)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(bounceSec, cancellationToken);

        if (enemyReflectionDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetForCardData(enemyReflectionDefenseCard);

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingPlayerAttackCards, slideTowardPlayer: false, SlideDurationSec, cancellationToken);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
        battleManager.SetReflectionAttackTotalDisplayAfterSlide(
            incomingPlayerAttackCards, totalAtkOnPlayerSide: false, player, player);

        try
        {
            await RunReflectionChainLoopAsync(
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

    public static async Task RunReflectionChainLoopAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        List<CardData> incomingAttackCards,
        int incomingPower,
        PlayerType defenderSide,
        bool sessionMagic,
        CancellationToken cancellationToken,
        PlayerStatus reflectionBlessingAttacker,
        PlayerStatus reflectionBlessingDefender)
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

                if (pick != null && BlockingRules.IsPhysicalBlockingCard(pick)
                    && BlockingRules.CanBlockPhysical(incomingAttackCards))
                {
                    BattleUIManager.I?.ShowEnemyDefenseCardPresentation(pick);
                    battleManager.SetStatsDisplaySequenceCards(
                        new List<CardData> { pick }, "防御", Side.Enemy);
                    await Task.Delay(500, cancellationToken);
                    await BlockingNullifyFlow.RunEnemyDefenderNullifiesAsync(
                        battleManager,
                        battleProcessor,
                        handRefill,
                        incomingAttackCards,
                        pick,
                        cancellationToken);
                    battleManager.ClearStatsDisplaySequenceCards();
                    return;
                }

                if (pick != null && IsContinuingReflectionChain(pick, incomingAttackCards))
                {
                    // 敵側パネルのみ削除（攻撃シートは反対側。同一 CardData で DestroyCardSheetForCardData すると攻撃も消える）
                    BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(pick, Side.Enemy);
                    BattleUIManager.I?.ShowEnemyDefenseCardPresentation(pick);
                    await Task.Delay(500, cancellationToken);

                    float sec = BattleUIManager.I != null
                        ? BattleUIManager.I.ShowReflectionBouncePopup(enemy, sessionMagic)
                        : DamagePopup.DefaultFadeDurationIfUnknown;
                    if (sec <= 0f) sec = DamagePopup.DefaultFadeDurationIfUnknown;
                    await DamagePopup.WaitAfterPopupLifetimeAsync(sec, cancellationToken);

                    BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(pick, Side.Enemy);

                    if (BattleUIManager.I != null)
                        await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                            incomingAttackCards, slideTowardPlayer: false, SlideDurationSec, cancellationToken);
                    SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
                    battleManager.SetReflectionAttackTotalDisplayAfterSlide(
                        incomingAttackCards, totalAtkOnPlayerSide: false, reflectionBlessingAttacker, reflectionBlessingDefender);

                    handRefill?.RecordEnemyUse(pick);
                    battleProcessor.UseCard(pick, battleManager.cpuHand);

                    defenderSide = PlayerType.Player;
                    continue;
                }

                if (pick != null)
                {
                    BattleUIManager.I?.ShowEnemyDefenseCardPresentation(pick);
                    battleManager.SetStatsDisplaySequenceCards(
                        new List<CardData> { pick }, "防御", Side.Enemy);
                    SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
                    await Task.Delay(500, cancellationToken);
                }

                bool showEnemyYurusu = pick == null && BattleUIManager.I != null;
                using (YurusuDisplayScope.ShowIf(showEnemyYurusu))
                {
                    await battleProcessor.ResolveReflectedCombatAsync(
                        incomingAttackCards,
                        incomingPower,
                        pick,
                        player,
                        enemy,
                        battleManager.cpuHand,
                        skipHitCheck: true);
                }

                if (pick != null)
                {
                    handRefill?.RecordEnemyUse(pick);
                    battleProcessor.UseCard(pick, battleManager.cpuHand);
                }
                battleManager.ClearStatsDisplaySequenceCards();
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
            if (IsContinuingReflectionChain(card, incomingAttackCards))
            {
                int slotIndex = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
                if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);
                battleProcessor.UseCard(card, battleManager.playerHand);

                // 攻撃と同一 CardData のときは全削除しない（攻撃シートが消える）。同パネル重複はバウンス直後に「最後の1枚」だけ消す。
                if (!IncomingAttackContainsCardReference(incomingAttackCards, card))
                    BattleUIManager.I?.DestroyCardSheetForCardData(card);

                BattleUIManager.I?.ShowCardDetail(card, Side.Player);
                await Task.Delay(500, cancellationToken);

                float sec2 = BattleUIManager.I != null
                    ? BattleUIManager.I.ShowReflectionBouncePopup(player, sessionMagic)
                    : DamagePopup.DefaultFadeDurationIfUnknown;
                if (sec2 <= 0f) sec2 = DamagePopup.DefaultFadeDurationIfUnknown;
                await DamagePopup.WaitAfterPopupLifetimeAsync(sec2, cancellationToken);

                BattleUIManager.I?.DestroyMostRecentCardSheetOnPanelForCardData(card, Side.Player);

                if (BattleUIManager.I != null)
                    await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                        incomingAttackCards, slideTowardPlayer: true, SlideDurationSec, cancellationToken);
                SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
                battleManager.SetReflectionAttackTotalDisplayAfterSlide(
                    incomingAttackCards, totalAtkOnPlayerSide: true, reflectionBlessingAttacker, reflectionBlessingDefender);

                defenderSide = PlayerType.Enemy;
                continue;
            }

            // 反射された物理攻撃に対する物理無効（ResolveReflectedCombat の通常防具扱いにしない）
            if (BlockingRules.IsPhysicalBlockingCard(card) && BlockingRules.CanBlockPhysical(incomingAttackCards))
            {
                if (card.cardType == CardType.Magic && battleManager.Sequences != null)
                {
                    await battleManager.Sequences.ApplyMagicCardToPoolForReflectionOrParryDefenseAsync(
                        card, cancellationToken);
                }
                else
                {
                    int slotB = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
                    if (slotB >= 0) handRefill?.RecordPlayerUseSlot(slotB);
                    battleProcessor.UseCard(card, battleManager.playerHand);
                }

                BattleUIManager.I?.ShowCardDetail(card, Side.Player);
                battleManager.SetStatsDisplaySequenceCards(
                    new List<CardData> { card }, "防御", Side.Player);
                await Task.Delay(500, cancellationToken);

                await BlockingNullifyFlow.RunPlayerInitiatedAsync(
                    battleManager,
                    incomingAttackCards,
                    card,
                    cancellationToken);
                battleManager.ClearStatsDisplaySequenceCards();
                return;
            }

            if (card.cardType == CardType.Magic && battleManager.Sequences != null)
            {
                await battleManager.Sequences.ApplyMagicCardToPoolForReflectionOrParryDefenseAsync(
                    card, cancellationToken);
            }
            else
            {
                int slot = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
                if (slot >= 0) handRefill?.RecordPlayerUseSlot(slot);
                battleProcessor.UseCard(card, battleManager.playerHand);
            }

            BattleUIManager.I?.ShowCardDetail(card, Side.Player);
            battleManager.SetStatsDisplaySequenceCards(
                new List<CardData> { card }, "防御", Side.Player);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            await Task.Delay(500, cancellationToken);

            await battleProcessor.ResolveReflectedCombatAsync(
                incomingAttackCards,
                incomingPower,
                card,
                enemy,
                player,
                battleManager.playerHand,
                skipHitCheck: true);
            battleManager.ClearStatsDisplaySequenceCards();
            return;
        }
    }
}
