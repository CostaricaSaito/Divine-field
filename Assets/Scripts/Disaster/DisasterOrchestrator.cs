using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 天変地異の演出シーケンスと効果解決を統括する。
/// トリガー源（Special カード・将来のラグナロク/自然発生等）はここへ集約する。
/// </summary>
public static class DisasterOrchestrator
{
    /// <summary>Special カード（混沌のスイッチ等）から発動。</summary>
    public static Task RunFromSpecialCardAsync(
        CardData triggerCard,
        PlayerStatus triggerOwner,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        DisasterKind kind = DisasterCatalog.RollKind(triggerOwner);
        return RunAsync(kind, triggerOwner, battleProcessor, cancellationToken);
    }

    /// <summary>将来拡張：種別指定または抽選で天変地異を発動。</summary>
    public static async Task RunAsync(
        DisasterKind kind,
        PlayerStatus triggerOwner,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        var bm = BattleManager.I;
        if (bm == null || triggerOwner == null || battleProcessor == null) return;

        var opponent = ReferenceEquals(triggerOwner, bm.GetPlayerStatus())
            ? bm.GetEnemyStatus()
            : bm.GetPlayerStatus();
        if (opponent == null) return;

        Side triggerSide = ReferenceEquals(triggerOwner, bm.GetPlayerStatus()) ? Side.Player : Side.Enemy;
        var effect = DisasterCatalog.GetEffect(kind);
        if (effect == null) return;

        CardData displayCard = DisasterCardFactory.CreateDisplayCard(kind, bm.cardDealer);
        CardData combatTemplate = DisasterCardFactory.CreateCombatTemplate(kind, bm.cardDealer, displayCard);

        bool handWasClickable = true;
        BattleUIManager.I?.SetHandClickable(false);

        try
        {
            ImportantPopup introPopup = BattleUIManager.I?.ShowImportantPopup(
                DisasterCatalog.ImportantPopupMessage,
                DisasterCatalog.ImportantPopupColor,
                triggerSide);
            float introLife = introPopup != null
                ? introPopup.SequenceLifetimeSeconds
                : ImportantPopup.DefaultSequenceLifetimeIfUnknown;
            await DamagePopup.WaitAfterPopupLifetimeAsync(introLife, cancellationToken);

            await Task.Delay(1000, cancellationToken);
            BattleUIManager.I?.PlayFullscreenWhiteFlashMs(50f);

            ClearTriggerSideDisplayImmediate(triggerSide);

            BattleUIManager.I?.ShowCardSheetsVisualOnlyBatch(new List<CardData> { displayCard }, triggerSide);
            bm.SetStatsDisplaySequenceCards(new List<CardData> { displayCard }, "攻撃", triggerSide);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
            await Task.Delay(500, cancellationToken);

            float msgLife = BattleUIManager.I != null
                ? BattleUIManager.I.ShowDisasterMessagePopup(
                    triggerOwner,
                    effect.MessagePopupKind,
                    effect.NotificationMessage)
                : 0f;
            await MessagePopup.WaitAfterPopupLifetimeAsync(msgLife, cancellationToken);

            var context = new DisasterResolveContext
            {
                BattleManager = bm,
                BattleProcessor = battleProcessor,
                Sequences = bm.Sequences,
                TriggerOwner = triggerOwner,
                Opponent = opponent,
                TriggerSide = triggerSide,
                DisplayCard = displayCard,
                CombatCardTemplate = combatTemplate,
            };

            await effect.ResolveAsync(context, cancellationToken);
        }
        finally
        {
            BattleUIManager.I?.SetHandClickable(handWasClickable);
        }
    }

    private static void ClearTriggerSideDisplayImmediate(Side triggerSide)
    {
        BattleUIManager.I?.ClearCardDisplayPanelImmediate(triggerSide);
    }
}

/// <summary>Disaster 表示用 CardData の生成。</summary>
public static class DisasterCardFactory
{
    public static CardData CreateDisplayCard(DisasterKind kind, CardDealer dealer)
    {
        if (DisasterCatalog.TryGetCardTemplate(kind, out var template) && dealer != null)
            return dealer.InstantiateCardFromTemplate(template);

        var card = ScriptableObject.CreateInstance<CardData>();
        card.cardType = CardType.Disaster;
        card.cardName = DisasterCatalog.GetDisplayName(kind);
        card.description = DisasterCatalog.GetDescription(kind);
        return card;
    }

    /// <summary>戦闘解決用テンプレート（Attack として解釈）。</summary>
    public static CardData CreateCombatTemplate(DisasterKind kind, CardDealer dealer, CardData displayCard)
    {
        if (DisasterCatalog.TryGetCardTemplate(kind, out var template) && dealer != null)
        {
            var instance = dealer.InstantiateCardFromTemplate(template);
            instance.name = template.name + " (DisasterCombat)";
            instance.cardType = CardType.Attack;
            return instance;
        }

        if (displayCard != null)
        {
            var clone = Object.Instantiate(displayCard);
            clone.cardUI = null;
            clone.cardType = CardType.Attack;
            return clone;
        }

        return null;
    }
}
