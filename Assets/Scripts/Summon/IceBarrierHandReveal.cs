using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Hand reveal with optional Shiva ice barrier transform (fade swap to FROZEN).
/// </summary>
public static class IceBarrierHandReveal
{
    public static async Task RevealCardAsync(
        CardData card,
        CardUI ui,
        HandRevealBatchContext batch,
        CancellationToken ct,
        bool enableInteractable = true,
        PlayerType drawerSide = PlayerType.Player)
    {
        if (ui == null) return;

        if (card == null
            || !IceBarrierTransformRegistry.TryGetOriginalTemplateAssetName(card, out var originalAsset))
        {
            await HandRevealPresentation.RevealCardCoreAsync(card, ui, batch, ct, enableInteractable);
            return;
        }

        var bm = BattleManager.I;
        var dealer = bm?.cardDealer;
        if (dealer == null)
        {
            await HandRevealPresentation.RevealCardCoreAsync(card, ui, batch, ct, enableInteractable);
            return;
        }

        var originalTemplate = dealer.FindTemplateByDisplayOrAssetName(originalAsset);
        if (originalTemplate == null)
        {
            await HandRevealPresentation.RevealCardCoreAsync(card, ui, batch, ct, enableInteractable);
            return;
        }

        await RunIceBarrierPresentationSyncAsync(drawerSide, originalAsset, ct);
        if (ct.IsCancellationRequested) return;

        batch ??= new HandRevealBatchContext();
        await Task.Delay(HandRevealPresentation.PreRevealDelayMs, ct);
        if (ct.IsCancellationRequested) return;

        var displayOriginal = dealer.InstantiateCardFromTemplate(originalTemplate);
        ui.Setup(displayOriginal, dealer.CardBackSprite, playerHandRareBackPresentation: true);
        if (enableInteractable && ui.button != null)
            ui.button.interactable = true;

        CardDealAudio.Play(displayOriginal, true);
        ui.Reveal();

        await IceBarrierHandCardTransition.ReplaceFaceUpWithFrozenAsync(
            ui, card, dealer.CardBackSprite, displayOriginal, ct);

        IceBarrierTransformRegistry.Clear(card);

        await Task.Delay(HandRevealPresentation.PostRevealDelayMs, ct);
    }

    private sealed class OpeningIceBarrierSlot
    {
        public CardUI Ui;
        public CardData FrozenCard;
        public CardData TempOriginalDisplay;
        public string OriginalAssetName;
    }

    static List<OpeningIceBarrierSlot> _openingIceBarrierPending;

    public static void PrepareOpeningHandForMassReveal(IReadOnlyList<CardUI> activeCardUIs)
    {
        _openingIceBarrierPending = CollectOpeningIceBarrierSlots(activeCardUIs);
    }

    public static bool HasAnyOpeningIceBarrierTransformPending()
        => _openingIceBarrierPending != null && _openingIceBarrierPending.Count > 0;

    public static async Task RunOpeningIceBarrierFollowUpAsync(CancellationToken ct)
    {
        var pending = _openingIceBarrierPending;
        _openingIceBarrierPending = null;
        if (pending == null || pending.Count == 0) return;

        pending.Sort((a, b) => a.Ui.transform.GetSiblingIndex().CompareTo(b.Ui.transform.GetSiblingIndex()));

        foreach (var slot in pending)
        {
            if (ct.IsCancellationRequested) return;
            if (slot.Ui == null || slot.FrozenCard == null) continue;

            await RunTransformOnFaceUpCardAsync(
                slot.FrozenCard,
                slot.Ui,
                slot.OriginalAssetName,
                slot.TempOriginalDisplay,
                ct,
                drawerSide: PlayerType.Player);
        }
    }

    static List<OpeningIceBarrierSlot> CollectOpeningIceBarrierSlots(IReadOnlyList<CardUI> activeCardUIs)
    {
        var result = new List<OpeningIceBarrierSlot>();
        var bm = BattleManager.I;
        var dealer = bm?.cardDealer;
        if (dealer == null) return result;

        for (int i = 0; i < activeCardUIs.Count; i++)
        {
            var ui = activeCardUIs[i];
            if (ui == null) continue;

            var frozen = ui.GetCardData();
            if (frozen == null
                || !IceBarrierTransformRegistry.TryGetOriginalTemplateAssetName(frozen, out var originalAsset))
                continue;

            var originalTemplate = dealer.FindTemplateByDisplayOrAssetName(originalAsset);
            if (originalTemplate == null) continue;

            var tempOriginal = dealer.InstantiateCardFromTemplate(originalTemplate);
            if (tempOriginal == null) continue;

            ui.Setup(tempOriginal, dealer.CardBackSprite, playerHandRareBackPresentation: true);

            result.Add(new OpeningIceBarrierSlot
            {
                Ui = ui,
                FrozenCard = frozen,
                TempOriginalDisplay = tempOriginal,
                OriginalAssetName = originalAsset,
            });
        }

        return result;
    }

    static async Task RunTransformOnFaceUpCardAsync(
        CardData frozenCard,
        CardUI ui,
        string originalAssetName,
        CardData tempOriginalDisplay,
        CancellationToken ct,
        PlayerType drawerSide)
    {
        var dealer = BattleManager.I?.cardDealer;
        if (ui == null || frozenCard == null || dealer == null) return;

        await RunIceBarrierPresentationSyncAsync(drawerSide, originalAssetName, ct);
        if (ct.IsCancellationRequested) return;

        await IceBarrierHandCardTransition.ReplaceFaceUpWithFrozenAsync(
            ui, frozenCard, dealer.CardBackSprite, tempOriginalDisplay, ct);

        IceBarrierTransformRegistry.Clear(frozenCard);
    }

    static async Task RunIceBarrierPresentationSyncAsync(
        PlayerType drawerSide,
        string originalAsset,
        CancellationToken ct)
    {
        bool drawerIsHost = IceBarrierOnlineSync.ResolveDrawerIsHostPlayer(drawerSide);
        await IceBarrierOnlineSync.CoordinateDrawPresentationAsync(
            drawerIsHost, transformed: true, originalAsset, ct);
    }
}
