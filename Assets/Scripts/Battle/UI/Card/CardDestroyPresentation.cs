using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Shared card destruction presentation: optional message, card sheet reveal, dissolve, hand cleanup.
/// Use for Indra passive, future discard/destroy card effects, etc.
/// </summary>
public static class CardDestroyPresentation
{
    /// <summary>Built-in presets for common destroy flows.</summary>
    public static class Presets
    {
        public const string CardDestroySoundEffectPath = "Assets/SE/カード破壊.mp3";

        public static class IndraHandDestroy
        {
            public const string DestroyMessage = "雷が手札を破壊する";
            public const string NoTargetMessage = "破壊する手札がない！";
            public const string SoundEffectPath = "Assets/SE/Thunder-Synthetic01-1.mp3";
            public static readonly Color MessageColor = new Color(1f, 0.92f, 0.35f);
            public const int PreDissolveHoldMs = 500;
        }

        public static class MagicPoolDestroy
        {
            public const string EffectMessage = "魔力が力を失う";
            public const string NoTargetMessage = "破壊する魔法がない！";
            public const string MessageSoundEffectPath = "Assets/SE/魔力封印の呪印.mp3";
            public static readonly Color MessageColor = new Color(0.72f, 0.55f, 0.98f);
            public const int PreDissolveHoldMs = 500;
            public const int BetweenDissolveMs = 200;
        }
    }

    /// <summary>Parameters for <see cref="PlayAsync"/>.</summary>
    public sealed class Request
    {
        public BattleManager BattleManager;
        public PlayerStatus MessageAnchor;
        public List<CardData> VictimHand;
        public bool VictimIsPlayerHand;
        public CardData TargetCard;
        public bool NoTarget;
        public string Message = "カードが破壊される";
        public string NoTargetMessage = "破壊する手札がない！";
        public Color MessageColor = new Color(1f, 0.92f, 0.35f);
        public string SoundEffectPath;
        public int PreDissolveHoldMs = 500;
        public bool RevealCardOnPanel = true;
        public bool RemoveFromHandAfterDissolve = true;
        public Side? DisplaySideOverride;
    }

    /// <summary>Indra summon passive (every 5 turn ends).</summary>
    public static Task PlayIndraHandDestroyAsync(
        BattleManager bm,
        PlayerStatus blessingOwner,
        PlayerStatus victim,
        List<CardData> victimHand,
        bool victimIsPlayerHand,
        CardData targetCard,
        bool noTarget,
        CancellationToken ct)
    {
        return PlayAsync(new Request
        {
            BattleManager = bm,
            MessageAnchor = blessingOwner,
            VictimHand = victimHand,
            VictimIsPlayerHand = victimIsPlayerHand,
            TargetCard = targetCard,
            NoTarget = noTarget,
            Message = Presets.IndraHandDestroy.DestroyMessage,
            NoTargetMessage = Presets.IndraHandDestroy.NoTargetMessage,
            MessageColor = Presets.IndraHandDestroy.MessageColor,
            SoundEffectPath = Presets.IndraHandDestroy.SoundEffectPath,
            PreDissolveHoldMs = Presets.IndraHandDestroy.PreDissolveHoldMs,
        }, ct);
    }

    /// <summary>
    /// Full sequence: message popup, optional card reveal on panel, dissolve, optional hand removal.
    /// </summary>
    public static async Task PlayAsync(Request request, CancellationToken ct)
    {
        if (request == null) return;

        var ui = BattleUIManager.I;
        if (ui == null || request.MessageAnchor == null) return;

        string message = request.NoTarget ? request.NoTargetMessage : request.Message;
        if (!string.IsNullOrEmpty(request.SoundEffectPath))
            SoundEffectPlayer.I?.Play(request.SoundEffectPath);

        float fadeSec = ui.ShowMessagePopupForTarget(request.MessageAnchor, message, request.MessageColor);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
        if (ct.IsCancellationRequested || request.NoTarget || request.TargetCard == null) return;

        Side victimSide = request.DisplaySideOverride
            ?? (request.VictimIsPlayerHand ? Side.Player : Side.Enemy);

        if (request.RevealCardOnPanel)
        {
            ui.ShowCardSheetVisualOnly(request.TargetCard, victimSide);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
        }

        GameObject sheetRoot = null;
        if (ui.TryGetCardSheetDisplayForCardData(request.TargetCard, out var sheetDisplay) && sheetDisplay != null)
            sheetRoot = sheetDisplay.gameObject;

        if (request.PreDissolveHoldMs > 0)
        {
            await Task.Delay(request.PreDissolveHoldMs, ct);
            if (ct.IsCancellationRequested) return;
        }

        if (sheetRoot != null)
        {
            PlayCardDestroySoundEffect();
            await CardDissolvePlayer.PlayAsync(sheetRoot, ct);
        }

        ui.DestroyCardSheetsForCardDataOnPanel(request.TargetCard, victimSide);

        if (request.RemoveFromHandAfterDissolve)
        {
            RemoveFromHand(
                request.BattleManager,
                request.VictimHand,
                request.TargetCard,
                request.VictimIsPlayerHand);
        }
    }

    /// <summary>
    /// Dissolve a card sheet already on the display panel, then destroy the sheet instance.
    /// Does not touch hand lists.
    /// </summary>
    public static async Task PlayDissolveOnPanelSheetAsync(
        CardData card,
        Side panelSide,
        CancellationToken ct,
        int preDissolveHoldMs = 0)
    {
        var ui = BattleUIManager.I;
        if (ui == null || card == null) return;

        GameObject sheetRoot = null;
        if (ui.TryGetCardSheetDisplayForCardData(card, out var sheetDisplay) && sheetDisplay != null)
            sheetRoot = sheetDisplay.gameObject;

        if (sheetRoot == null) return;

        if (preDissolveHoldMs > 0)
        {
            await Task.Delay(preDissolveHoldMs, ct);
            if (ct.IsCancellationRequested) return;
        }

        PlayCardDestroySoundEffect();
        await CardDissolvePlayer.PlayAsync(sheetRoot, ct);
        ui.DestroyCardSheetsForCardDataOnPanel(card, panelSide);
    }

    /// <summary>
    /// Magic Sealer style: message, reveal all pooled magic sheets, sequential dissolve.
    /// Does not modify MagicPool (caller clears after).
    /// </summary>
    public static async Task PlayMagicPoolDestroySequenceAsync(
        PlayerStatus messageAnchor,
        Side displaySide,
        IReadOnlyList<CardData> poolCards,
        bool noTarget,
        CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || messageAnchor == null) return;

        if (noTarget || poolCards == null || poolCards.Count == 0)
        {
            float noTargetFade = ui.ShowMessagePopupForTarget(
                messageAnchor,
                Presets.MagicPoolDestroy.NoTargetMessage,
                Presets.MagicPoolDestroy.MessageColor);
            await DamagePopup.WaitAfterPopupLifetimeAsync(noTargetFade, ct);
            return;
        }

        SoundEffectPlayer.I?.Play(Presets.MagicPoolDestroy.MessageSoundEffectPath);
        float fadeSec = ui.ShowMessagePopupForTarget(
            messageAnchor,
            Presets.MagicPoolDestroy.EffectMessage,
            Presets.MagicPoolDestroy.MessageColor);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
        if (ct.IsCancellationRequested) return;

        var ordered = new List<CardData>(poolCards.Count);
        for (int i = 0; i < poolCards.Count; i++)
        {
            if (poolCards[i] != null) ordered.Add(poolCards[i]);
        }
        if (ordered.Count == 0) return;

        ui.ShowCardSheetsVisualOnlyBatch(ordered, displaySide);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);

        await Task.Delay(Presets.MagicPoolDestroy.PreDissolveHoldMs, ct);
        if (ct.IsCancellationRequested) return;

        for (int i = 0; i < ordered.Count; i++)
        {
            await PlayDissolveOnPanelSheetAsync(ordered[i], displaySide, ct);
            if (ct.IsCancellationRequested) return;
            if (i < ordered.Count - 1 && Presets.MagicPoolDestroy.BetweenDissolveMs > 0)
                await Task.Delay(Presets.MagicPoolDestroy.BetweenDissolveMs, ct);
        }
    }

    private static void PlayCardDestroySoundEffect()
    {
        if (!string.IsNullOrEmpty(Presets.CardDestroySoundEffectPath))
            SoundEffectPlayer.I?.Play(Presets.CardDestroySoundEffectPath);
    }

    /// <summary>Resolve destroy target on victim hand (by index from host sync, else by name).</summary>
    public static CardData ResolveTargetCard(List<CardData> victimHand, string cardName, int handIndex)
    {
        if (victimHand == null || victimHand.Count == 0) return null;

        if (handIndex >= 0 && handIndex < victimHand.Count)
        {
            var atIndex = victimHand[handIndex];
            if (atIndex != null && (string.IsNullOrEmpty(cardName) || atIndex.cardName == cardName))
                return atIndex;
        }

        if (string.IsNullOrEmpty(cardName)) return null;

        for (int i = 0; i < victimHand.Count; i++)
        {
            var c = victimHand[i];
            if (c != null && c.cardName == cardName)
                return c;
        }

        return null;
    }

    /// <summary>Remove destroyed card from hand list and refresh player hand UI.</summary>
    public static void RemoveFromHand(
        BattleManager bm,
        List<CardData> hand,
        CardData card,
        bool isPlayerHand)
    {
        if (hand == null || card == null) return;

        if (isPlayerHand && card.cardUI != null)
        {
            var uiObj = card.cardUI.gameObject;
            card.cardUI = null;
            Object.Destroy(uiObj);
        }

        hand.Remove(card);

        if (isPlayerHand && bm != null && bm.handPanel is RectTransform rt)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        if (isPlayerHand)
            BattleUIManager.I?.SetIntroModeUI(hand);
    }
}
