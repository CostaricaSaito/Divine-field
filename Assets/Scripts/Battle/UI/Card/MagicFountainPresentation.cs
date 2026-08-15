using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Magic Fountain (魔力の泉): message, sequential pool card reveal, animated use-count refill.
/// </summary>
public static class MagicFountainPresentation
{
    public const string EffectMessage = "魔力がうるおう";
    public const string NoTargetMessage = "マグルだ！";
    public const string MessageSoundEffectPath = "Assets/SE/マジカルエクスプロージョン.mp3";
    public const string CountUpLoopSoundEffectPath = "Assets/SE/電子ルーレット回転中.mp3";
    public const string CountUpStopSoundEffectPath = "Assets/SE/電子ルーレット停止ボタンを押す.mp3";
    public const int PreCountUpHoldMs = 500;
    public const int PostCountUpHoldMs = 500;
    public const int CountUpStepMs = 180;
    public const int BetweenRevealMs = 200;

    public static readonly Color MessageColor = new Color(0.45f, 0.82f, 0.95f);

    public sealed class EntrySnapshot
    {
        public CardData Card;
        public int StartUses;
    }

    public static async Task PlayAsync(
        CardData fountainCard,
        Side fountainDisplaySide,
        PlayerStatus messageAnchor,
        Side poolDisplaySide,
        PlayerStatus poolOwnerStatus,
        IReadOnlyList<EntrySnapshot> entries,
        bool noTarget,
        CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || messageAnchor == null) return;

        if (noTarget || entries == null || entries.Count == 0)
        {
            RemoveFountainSheet(ui, fountainCard, fountainDisplaySide);
            float noTargetFade = ui.ShowMessagePopupForTarget(
                messageAnchor, NoTargetMessage, MessageColor);
            await DamagePopup.WaitAfterPopupLifetimeAsync(noTargetFade, ct);
            return;
        }

        var validEntries = new List<EntrySnapshot>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i]?.Card != null)
                validEntries.Add(entries[i]);
        }
        if (validEntries.Count == 0)
        {
            RemoveFountainSheet(ui, fountainCard, fountainDisplaySide);
            float noTargetFade = ui.ShowMessagePopupForTarget(
                messageAnchor, NoTargetMessage, MessageColor);
            await DamagePopup.WaitAfterPopupLifetimeAsync(noTargetFade, ct);
            return;
        }

        SoundEffectPlayer.I?.Play(MessageSoundEffectPath);
        float fadeSec = ui.ShowMessagePopupForTarget(messageAnchor, EffectMessage, MessageColor);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
        if (ct.IsCancellationRequested) return;

        RemoveFountainSheet(ui, fountainCard, fountainDisplaySide);
        ui.ClearCardDisplayPanelImmediate(poolDisplaySide);

        var orderedCards = new List<CardData>(validEntries.Count);
        var displays = new List<CardSheetDisplay>(validEntries.Count);

        for (int i = 0; i < validEntries.Count; i++)
        {
            var entry = validEntries[i];
            orderedCards.Add(entry.Card);
            var display = ui.AppendCardSheetVisualOnly(entry.Card, poolDisplaySide, orderedCards, poolOwnerStatus);
            if (display != null)
            {
                display.SetPoolRemainingUsesDisplay(entry.StartUses);
                displays.Add(display);
            }

            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            if (ct.IsCancellationRequested) return;
            if (i < validEntries.Count - 1 && BetweenRevealMs > 0)
                await Task.Delay(BetweenRevealMs, ct);
        }

        if (displays.Count == 0) return;

        await Task.Delay(PreCountUpHoldMs, ct);
        if (ct.IsCancellationRequested) return;

        for (int i = 0; i < validEntries.Count; i++)
        {
            var entry = validEntries[i];
            CardSheetDisplay display = i < displays.Count ? displays[i] : null;
            if (display == null
                && ui.TryGetCardSheetDisplayForCardData(entry.Card, out var found))
            {
                display = found;
            }

            if (SoundEffectPlayer.I != null)
                await SoundEffectPlayer.I.StartLoopingAsync(CountUpLoopSoundEffectPath);

            try
            {
                for (int step = 1; step <= MagicFountainRules.UsesBonus; step++)
                {
                    display?.SetPoolRemainingUsesDisplay(entry.StartUses + step);
                    await Task.Delay(CountUpStepMs, ct);
                    if (ct.IsCancellationRequested) return;
                }
            }
            finally
            {
                SoundEffectPlayer.I?.StopLooping();
            }

            SoundEffectPlayer.I?.Play(CountUpStopSoundEffectPath);
        }

        await Task.Delay(PostCountUpHoldMs, ct);
        if (ct.IsCancellationRequested) return;

        ui.HideAllCardDetails();
    }

    private static void RemoveFountainSheet(BattleUIManager ui, CardData fountainCard, Side fountainDisplaySide)
    {
        if (ui == null || fountainCard == null) return;
        ui.DestroyCardSheetsForCardDataOnPanel(fountainCard, fountainDisplaySide);
        ui.ClearCardDisplayPanelImmediate(fountainDisplaySide);
    }
}
