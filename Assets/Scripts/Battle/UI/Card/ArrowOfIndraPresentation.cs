using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Arrow of Indra (インドラの矢): message, sequential hand reveal, dissolve destroy.
/// </summary>
public static class ArrowOfIndraPresentation
{
    public const string EffectMessage = "その時手札に電撃走る...!";
    public const string NoTargetMessage = "ああ！手札がない！";
    public const string MessageSoundEffectPath = CardDestroyPresentation.Presets.CardDestroySoundEffectPath;
    public const int PreDissolveHoldMs = 500;
    public const int BetweenRevealMs = 200;
    public const int BetweenDissolveMs = 200;

    public static readonly Color MessageColor = new Color(1f, 0.92f, 0.35f);

    public static async Task PlayAsync(
        CardData arrowCard,
        Side arrowDisplaySide,
        PlayerStatus messageAnchor,
        Side victimDisplaySide,
        PlayerStatus victimStatus,
        IReadOnlyList<CardData> targetCards,
        bool noTarget,
        CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || messageAnchor == null) return;

        if (noTarget || targetCards == null || targetCards.Count == 0)
        {
            RemoveArrowSheet(ui, arrowCard, arrowDisplaySide);
            float noTargetFade = ui.ShowMessagePopupForTarget(
                messageAnchor, NoTargetMessage, MessageColor);
            await DamagePopup.WaitAfterPopupLifetimeAsync(noTargetFade, ct);
            return;
        }

        var ordered = new List<CardData>(targetCards.Count);
        for (int i = 0; i < targetCards.Count; i++)
        {
            if (targetCards[i] != null)
                ordered.Add(targetCards[i]);
        }
        if (ordered.Count == 0)
        {
            RemoveArrowSheet(ui, arrowCard, arrowDisplaySide);
            float noTargetFade = ui.ShowMessagePopupForTarget(
                messageAnchor, NoTargetMessage, MessageColor);
            await DamagePopup.WaitAfterPopupLifetimeAsync(noTargetFade, ct);
            return;
        }

        SoundEffectPlayer.I?.Play(MessageSoundEffectPath);
        float fadeSec = ui.ShowMessagePopupForTarget(messageAnchor, EffectMessage, MessageColor);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
        if (ct.IsCancellationRequested) return;

        RemoveArrowSheet(ui, arrowCard, arrowDisplaySide);
        ui.ClearCardDisplayPanelImmediate(victimDisplaySide);

        var revealed = new List<CardData>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            revealed.Add(ordered[i]);
            ui.AppendCardSheetVisualOnly(ordered[i], victimDisplaySide, revealed, victimStatus);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            if (ct.IsCancellationRequested) return;
            if (i < ordered.Count - 1 && BetweenRevealMs > 0)
                await Task.Delay(BetweenRevealMs, ct);
        }

        await Task.Delay(PreDissolveHoldMs, ct);
        if (ct.IsCancellationRequested) return;

        for (int i = 0; i < ordered.Count; i++)
        {
            await CardDestroyPresentation.PlayDissolveOnPanelSheetAsync(
                ordered[i], victimDisplaySide, ct);
            if (ct.IsCancellationRequested) return;
            if (i < ordered.Count - 1 && BetweenDissolveMs > 0)
                await Task.Delay(BetweenDissolveMs, ct);
        }

        ui.HideAllCardDetails();
    }

    private static void RemoveArrowSheet(BattleUIManager ui, CardData arrowCard, Side arrowDisplaySide)
    {
        if (ui == null || arrowCard == null) return;
        ui.DestroyCardSheetsForCardDataOnPanel(arrowCard, arrowDisplaySide);
        ui.ClearCardDisplayPanelImmediate(arrowDisplaySide);
    }
}
