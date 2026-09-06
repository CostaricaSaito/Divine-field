using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Thief's Hood presentation: hood sheet glow, stolen card reveal on CardDisplayPanel.
/// </summary>
public static class ThiefHoodPresentation
{
    public const int StolenCardDisplayIntervalMs = 1000;
    public const float HoodTintFadeInSec = 0.5f;
    public const float HoodTintFadeOutSec = 0.5f;
    private static readonly Color HoodGlowTint = Color.white;

    public static async Task RunHoodSheetGlowAsync(
        IReadOnlyList<CardData> defenseCards,
        CancellationToken cancellationToken)
    {
        var hoods = ThiefHoodRules.CollectThiefHoodCards(defenseCards);
        if (hoods.Count == 0 || BattleUIManager.I == null) return;

        SoundEffectPlayer.I?.Play(OrbDefenseReactionFlow.OrbGaugeRecoverySe);

        for (int i = 0; i < hoods.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hood = hoods[i];
            if (hood == null) continue;

            if (!BattleUIManager.I.TryGetCardSheetDisplayForCardData(hood, out var sheet))
                continue;

            await sheet.PlayOrbElementTintFlashAsync(
                HoodGlowTint, HoodTintFadeInSec, HoodTintFadeOutSec, cancellationToken);
        }
    }

    public static async Task ShowStolenCardsOnDisplayAsync(
        IReadOnlyList<CardData> stolenCards,
        Side defenderSide,
        CancellationToken cancellationToken)
    {
        if (stolenCards == null || stolenCards.Count == 0 || BattleUIManager.I == null)
            return;

        for (int i = 0; i < stolenCards.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var card = stolenCards[i];
            if (card == null) continue;

            BattleUIManager.I.ClearCardDisplayPanelImmediate(defenderSide);
            BattleUIManager.I.ShowCardSheetVisualOnly(card, defenderSide);
            SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
            await Task.Delay(StolenCardDisplayIntervalMs, cancellationToken);
        }

        BattleUIManager.I.ClearCardDisplayPanelImmediate(defenderSide);
    }
}
