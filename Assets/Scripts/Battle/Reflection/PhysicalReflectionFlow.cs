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
        return ReflectionRules.CanReflectIncoming(pick, incomingAttackCards);
    }

    private static bool IsImmediateIncoming(IReadOnlyList<CardData> incomingAttackCards)
    {
        return ReflectionRules.ShouldUseImmediateEffectReflectionFlow(incomingAttackCards);
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
    /// 連鎖反射中のプレイヤー防御掲示。submit 後は <see cref="BattleManager.IsPlayerDefenseInputActive"/> が false になり
    /// <see cref="BattleUIManager.ShowCardDetail"/> の AddCardSelection が失敗しうるため、手札選択を介さない表示にする。
    /// </summary>
    private static void ShowPlayerDefenseCardPresentation(CardData card)
    {
        BattleUIManager.I?.ShowCardSheetVisualOnly(card, Side.Player);
    }

    private static void DestroyPlayerDefenseSheetIfSafe(CardData card, IReadOnlyList<CardData> incomingAttackCards)
    {
        if (card == null || IncomingAttackContainsCardReference(incomingAttackCards, card)) return;
        BattleUIManager.I?.DestroyCardSheetsForCardDataOnPanel(card, Side.Player);
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

        bool sessionMagic = ReflectionRules.CanReflectMagic(incomingAttackCards)
            || GrandMagicRules.ContainsGrandMagicStyleAttack(incomingAttackCards)
            || (playerReflectionDefenseCard != null
                && ReflectionRules.IsFullReflectionCard(playerReflectionDefenseCard)
                && CardRules.IsMagicClassifiedAttackCombo(incomingAttackCards));
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
            incomingAttackCards, totalAtkOnPlayerSide: true, enemy, enemy, incomingPower);

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

        bool sessionMagic = ReflectionRules.CanReflectMagic(incomingPlayerAttackCards)
            || GrandMagicRules.ContainsGrandMagicStyleAttack(incomingPlayerAttackCards)
            || (enemyReflectionDefenseCard != null
                && ReflectionRules.IsFullReflectionCard(enemyReflectionDefenseCard)
                && CardRules.IsMagicClassifiedAttackCombo(incomingPlayerAttackCards));
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
            incomingPlayerAttackCards, totalAtkOnPlayerSide: false, player, player, incomingPower);

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
                    BattleUIManager.I?.ShowEnemyDefenseCardPresentation(pick);
                    await Task.Delay(500, cancellationToken);

                    handRefill?.RecordEnemyUse(pick);
                    battleProcessor.UseCard(pick, battleManager.cpuHand);

                    if (IsImmediateIncoming(incomingAttackCards))
                    {
                        await ImmediateEffectReflectionFlow.RunChainBounceAsync(
                            battleManager,
                            battleProcessor,
                            incomingAttackCards,
                            player,
                            slideTowardPlayer: false,
                            enemy,
                            cancellationToken);
                        defenderSide = PlayerType.Player;
                        continue;
                    }

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
                        incomingAttackCards, totalAtkOnPlayerSide: false, reflectionBlessingAttacker, reflectionBlessingDefender, incomingPower);

                    defenderSide = PlayerType.Player;
                    continue;
                }

                if (pick != null && ParryRules.RequiresParryExclusiveLock(pick, incomingAttackCards))
                {
                    BattleUIManager.I?.ShowEnemyDefenseCardPresentation(pick);
                    await Task.Delay(500, cancellationToken);

                    await ParryFlow.RunEnemyDefenderParriesPlayerAttackAsync(
                        battleManager,
                        battleProcessor,
                        handRefill,
                        enemyAI,
                        incomingAttackCards,
                        pick,
                        cancellationToken);
                    battleManager.ClearStatsDisplaySequenceCards();
                    return;
                }

                if (pick == null && IsImmediateIncoming(incomingAttackCards))
                {
                    await ResolveImmediateIncomingOnDefenderAsync(
                        battleProcessor, incomingAttackCards, enemy, player, cancellationToken);
                    return;
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
                if (IsImmediateIncoming(incomingAttackCards))
                {
                    await ResolveImmediateIncomingOnDefenderAsync(
                        battleProcessor, incomingAttackCards, player, enemy, cancellationToken);
                    return;
                }

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

                if (!IncomingAttackContainsCardReference(incomingAttackCards, card))
                    BattleUIManager.I?.DestroyCardSheetForCardData(card);

                ShowPlayerDefenseCardPresentation(card);
                await Task.Delay(500, cancellationToken);

                if (IsImmediateIncoming(incomingAttackCards))
                {
                    await ImmediateEffectReflectionFlow.RunChainBounceAsync(
                        battleManager,
                        battleProcessor,
                        incomingAttackCards,
                        enemy,
                        slideTowardPlayer: true,
                        player,
                        cancellationToken);
                    defenderSide = PlayerType.Enemy;
                    continue;
                }

                float sec2 = BattleUIManager.I != null
                    ? BattleUIManager.I.ShowReflectionBouncePopup(player, sessionMagic)
                    : DamagePopup.DefaultFadeDurationIfUnknown;
                if (sec2 <= 0f) sec2 = DamagePopup.DefaultFadeDurationIfUnknown;
                await DamagePopup.WaitAfterPopupLifetimeAsync(sec2, cancellationToken);

                DestroyPlayerDefenseSheetIfSafe(card, incomingAttackCards);

                if (BattleUIManager.I != null)
                    await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                        incomingAttackCards, slideTowardPlayer: true, SlideDurationSec, cancellationToken);
                SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
                battleManager.SetReflectionAttackTotalDisplayAfterSlide(
                    incomingAttackCards, totalAtkOnPlayerSide: true, reflectionBlessingAttacker, reflectionBlessingDefender, incomingPower);

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

                ShowPlayerDefenseCardPresentation(card);
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

            if (ParryRules.RequiresParryExclusiveLock(card, incomingAttackCards))
            {
                int parrySlot = card.cardUI != null ? card.cardUI.transform.GetSiblingIndex() : -1;
                if (parrySlot >= 0) handRefill?.RecordPlayerUseSlot(parrySlot);
                battleProcessor.UseCard(card, battleManager.playerHand);

                ShowPlayerDefenseCardPresentation(card);
                await Task.Delay(500, cancellationToken);

                await ParryFlow.RunPlayerInitiatedAsync(
                    battleManager,
                    battleProcessor,
                    handRefill,
                    enemyAI,
                    incomingAttackCards,
                    card,
                    battleManager.Sequences,
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

            ShowPlayerDefenseCardPresentation(card);
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

    private static async Task ResolveImmediateIncomingOnDefenderAsync(
        BattleProcessor battleProcessor,
        List<CardData> incomingAttackCards,
        PlayerStatus defender,
        PlayerStatus attacker,
        CancellationToken cancellationToken)
    {
        if (battleProcessor == null || incomingAttackCards == null || incomingAttackCards.Count == 0 || defender == null)
            return;

        if (incomingAttackCards.Count == 1 && incomingAttackCards[0] != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await battleProcessor.ResolveImmediateEffectAsync(incomingAttackCards[0], attacker, defender);
            return;
        }

        for (int i = 0; i < incomingAttackCards.Count; i++)
        {
            var c = incomingAttackCards[i];
            if (c == null) continue;
            cancellationToken.ThrowIfCancellationRequested();
            await battleProcessor.ResolveImmediateEffectAsync(c, attacker, defender);
        }
    }
}
