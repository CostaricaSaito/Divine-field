using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Shared presentation for defense-included triggers (orb-style sheet tint flash).</summary>
public static class DefenseIncludedPresentation
{
    public static async Task RunTriggerCardElementGlowAsync(
        CardData triggeringCard,
        Side displaySide,
        CancellationToken cancellationToken)
    {
        if (triggeringCard == null || BattleUIManager.I == null) return;

        SoundEffectPlayer.I?.Play(OrbDefenseReactionFlow.OrbGaugeRecoverySe);

        if (!BattleUIManager.I.TryGetCardSheetDisplayForCardData(triggeringCard, out _))
            BattleUIManager.I.ShowCardSheetVisualOnly(triggeringCard, displaySide);

        if (!BattleUIManager.I.TryGetCardSheetDisplayForCardData(triggeringCard, out var sheet))
            return;

        Color tint = ElementHelper.GetElementColor(triggeringCard.element);
        await sheet.PlayOrbElementTintFlashAsync(
            tint,
            OrbDefenseReactionFlow.OrbTintFadeInSec,
            OrbDefenseReactionFlow.OrbTintFadeOutSec,
            cancellationToken);
    }
}
