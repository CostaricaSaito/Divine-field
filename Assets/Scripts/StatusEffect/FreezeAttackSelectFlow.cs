using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Frozen turn owner skips AttackSelect (no attack/recovery/magic/economy/reload).
/// </summary>
public static class FreezeAttackSelectFlow
{
    public static bool IsTurnOwnerFrozen(PlayerStatus turnOwner)
    {
        return turnOwner != null && turnOwner.HasFreezeEffect();
    }

    public static async Task RunSkipFrozenTurnAsync(PlayerStatus frozenOwner, CancellationToken ct)
    {
        if (frozenOwner == null) return;

        var ui = BattleUIManager.I;
        if (ui != null)
        {
            ui.SetHandClickable(false);
            ui.SetUseButtonInteractable(false);
            ui.DisableEconomicActionButtonsTemporarily();
            ui.SetHandGrayedOut(BattleManager.I?.playerHand, grayedOut: true);
            ui.RefreshMagicCardInteractivity(BattleManager.I?.playerHand);
            HandReloadController.I?.RefreshReloadEntryButton();
            BattleManager.I?.RefreshSummonSkillButtonInteractables();
        }

        if (ui == null) return;

        float fadeSec = ui.ShowStyledMessagePopup(frozenOwner, MessagePopupKind.FreezeCannotMove);
        if (fadeSec > 0f)
            await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
    }
}
