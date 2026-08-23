using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Millenium Kingdom: skip defense and nullify qualifying incoming attacks on the buff holder.
/// </summary>
public static class MilleniumKingdomCombatFlow
{
    /// <summary>DefensePhase entry (hit already confirmed for strike attacks).</summary>
    public static async Task<bool> TryResolveDefensePhaseNullifyAsync(
        BattleManager bm,
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        CardData currentAttackCard,
        CancellationToken ct)
    {
        if (!ArcadiasUltimateRules.ShouldNullifyIncoming(atk, def, attackCards, currentAttackCard))
            return false;
        if (attackCards == null || attackCards.Count == 0)
            return false;

        await Task.Delay(1000, ct);
        if (ct.IsCancellationRequested) return false;

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す13.mp3");

        await PlayNullifyPresentationAsync(def, ct);
        if (ct.IsCancellationRequested) return false;

        if (await bm.TryHandleDeathIfAnyAsync(ct))
            return true;

        if (await bm.TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(ct))
            return true;

        await FinishNullifyAsync(bm, ct);
        return true;
    }

    /// <summary>ResolvePlayerAttackCombatAsync path after a successful hit roll.</summary>
    public static async Task<bool> TryResolveNullifyAfterHitAsync(
        BattleManager bm,
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        CardData currentAttackCard,
        CancellationToken ct,
        int dualBladeStrikeIndex = 0)
    {
        if (dualBladeStrikeIndex > 0) return false;
        if (!ArcadiasUltimateRules.ShouldNullifyIncoming(atk, def, attackCards, currentAttackCard))
            return false;
        if (attackCards == null || attackCards.Count == 0)
            return false;

        await PlayNullifyPresentationAsync(def, ct);
        return !ct.IsCancellationRequested;
    }

    private static async Task PlayNullifyPresentationAsync(PlayerStatus defender, CancellationToken ct)
    {
        if (defender == null) return;

        SoundEffectPlayer.I?.Play(BlockingNullifyAudio.Physical);

        float fadeSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowMessagePopupForTarget(
                defender,
                ArcadiasUltimateRules.LightProtectionMessage,
                ArcadiasUltimateRules.LightProtectionMessageColor)
            : DamagePopup.DefaultFadeDurationIfUnknown;

        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
    }

    private static async Task FinishNullifyAsync(BattleManager bm, CancellationToken ct)
    {
        if (bm == null) return;

        bm.ClearMagicalExplosionComboMpPoolSnapshot();
        bm.ClearMillionDollarBazookaComboGpPoolSnapshot();
        bm.ClearTributeBloodHpPaidSnapshot();
        bm.ClearHammadnessRollSnapshot();
        BattleUIManager.I?.HideAllCardDetails();
        bm.ClearIncomingAttackElementOverrides();
        bm.ClearStatsDisplaySequenceCards();
        bm.SetCurrentAttackCard(null);
        bm.SetSelectedDefenseCard(null);
        bm.UpdateTotalATKDEFDisplay();
        bm.SetGameState(GameState.CombatResolvePhase);
        await Task.Yield();
    }
}
