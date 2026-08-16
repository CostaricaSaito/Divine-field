using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Judgement Thunder ultimate: sequential hand reveal and dissolve on victim panel (no message popup).
/// </summary>
public static class JudgementThunderUltimatePresentation
{
    public const int PreDissolveHoldMs = ArrowOfIndraPresentation.PreDissolveHoldMs;
    public const int BetweenRevealMs = ArrowOfIndraPresentation.BetweenRevealMs;
    public const int BetweenDissolveMs = ArrowOfIndraPresentation.BetweenDissolveMs;

    public static async Task PlayDestroySequenceAsync(
        PlayerStatus victimStatus,
        Side victimDisplaySide,
        IReadOnlyList<CardData> targetCards,
        CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || targetCards == null || targetCards.Count == 0) return;

        var ordered = new List<CardData>(targetCards.Count);
        for (int i = 0; i < targetCards.Count; i++)
        {
            if (targetCards[i] != null)
                ordered.Add(targetCards[i]);
        }
        if (ordered.Count == 0) return;

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
}
