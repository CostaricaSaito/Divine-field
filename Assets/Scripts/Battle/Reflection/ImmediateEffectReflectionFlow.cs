using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 完全反射：回復・状態異常付与・Special 単体など即時効果 incoming の跳ね返し。
/// 効果は元の攻撃者へ返る（カード UI は防御側パネルへスライド）。
/// </summary>
public static class ImmediateEffectReflectionFlow
{
    private const float SlideDurationSec = 0.5f;

    /// <summary>プレイヤーが完全反射で敵の即時効果攻撃を跳ね返す。</summary>
    /// <param name="reflectionCardAlreadyConsumed">
    /// true のとき <see cref="CardSequenceManager"/> 経由で ProcessCardsAsync 済み（二重 UseCard を避ける）。
    /// </param>
    public static async Task RunPlayerInitiatedAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> incomingCards,
        CardData playerReflectionDefenseCard,
        PlayerStatus originalAttacker,
        PlayerStatus originalDefender,
        CancellationToken cancellationToken,
        bool reflectionCardAlreadyConsumed = false)
    {
        if (battleManager == null || battleProcessor == null || incomingCards == null || incomingCards.Count == 0)
            return;

        var player = battleManager.GetPlayerStatus();
        await RunImmediateBounceAsync(
            battleManager,
            battleProcessor,
            handRefill,
            incomingCards,
            playerReflectionDefenseCard,
            originalAttacker,
            slideTowardPlayer: true,
            bouncePopupOwner: player,
            consumeReflectionAsPlayer: true,
            reflectionCardAlreadyConsumed,
            cancellationToken);
    }

    /// <summary>敵が完全反射でプレイヤーの即時効果攻撃を跳ね返す。</summary>
    public static async Task RunEnemyDefenderReflectsPlayerImmediateAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> incomingPlayerCards,
        CardData enemyReflectionDefenseCard,
        PlayerStatus originalAttacker,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || battleProcessor == null || incomingPlayerCards == null || incomingPlayerCards.Count == 0)
            return;

        var enemy = battleManager.GetEnemyStatus();
        await RunImmediateBounceAsync(
            battleManager,
            battleProcessor,
            handRefill,
            incomingPlayerCards,
            enemyReflectionDefenseCard,
            originalAttacker,
            slideTowardPlayer: false,
            bouncePopupOwner: enemy,
            consumeReflectionAsPlayer: false,
            reflectionCardAlreadyConsumed: false,
            cancellationToken);
    }

    /// <summary>連鎖反射中の即時効果跳ね返し（反射カード消費は呼び出し側で済みのことがある）。</summary>
    public static async Task RunChainBounceAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        List<CardData> incomingCards,
        PlayerStatus originalAttacker,
        bool slideTowardPlayer,
        PlayerStatus bouncePopupOwner,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || battleProcessor == null || incomingCards == null || incomingCards.Count == 0)
            return;

        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowReflectionBouncePopup(bouncePopupOwner, magicReflection: false)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(bounceSec, cancellationToken);

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingCards, slideTowardPlayer, SlideDurationSec, cancellationToken);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);

        await ResolveBouncedImmediateEffectAsync(battleProcessor, incomingCards, originalAttacker, cancellationToken);
    }

    private static async Task RunImmediateBounceAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> incomingCards,
        CardData reflectionDefenseCard,
        PlayerStatus originalAttacker,
        bool slideTowardPlayer,
        PlayerStatus bouncePopupOwner,
        bool consumeReflectionAsPlayer,
        bool reflectionCardAlreadyConsumed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowReflectionBouncePopup(bouncePopupOwner, magicReflection: false)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(bounceSec, cancellationToken);

        if (reflectionDefenseCard != null)
        {
            if (!reflectionCardAlreadyConsumed)
            {
                if (consumeReflectionAsPlayer)
                {
                    int slotIndex = reflectionDefenseCard.cardUI != null
                        ? reflectionDefenseCard.cardUI.transform.GetSiblingIndex()
                        : -1;
                    if (slotIndex >= 0) handRefill?.RecordPlayerUseSlot(slotIndex);
                    battleProcessor.UseCard(reflectionDefenseCard, battleManager.playerHand);
                }
                else
                {
                    handRefill?.RecordEnemyUse(reflectionDefenseCard);
                    battleProcessor.UseCard(reflectionDefenseCard, battleManager.cpuHand);
                }
            }

            BattleUIManager.I?.DestroyCardSheetForCardData(reflectionDefenseCard);
        }

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingCards, slideTowardPlayer, SlideDurationSec, cancellationToken);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);

        await ResolveBouncedImmediateEffectAsync(battleProcessor, incomingCards, originalAttacker, cancellationToken);
    }

    private static async Task ResolveBouncedImmediateEffectAsync(
        BattleProcessor battleProcessor,
        List<CardData> incomingCards,
        PlayerStatus originalAttacker,
        CancellationToken cancellationToken)
    {
        if (incomingCards == null || incomingCards.Count == 0 || originalAttacker == null) return;

        if (incomingCards.Count == 1 && incomingCards[0] != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await battleProcessor.ResolveImmediateEffectAsync(
                incomingCards[0], originalAttacker, originalAttacker);
            return;
        }

        for (int i = 0; i < incomingCards.Count; i++)
        {
            var card = incomingCards[i];
            if (card == null) continue;
            cancellationToken.ThrowIfCancellationRequested();
            await battleProcessor.ResolveImmediateEffectAsync(card, originalAttacker, originalAttacker);
        }
    }
}
