public partial class BattleManager
{
    private readonly UltimateReadyStateTracker _ultimateReadyTracker = new();

    public void SyncUltimateReadyState(PlayerStatus player)
    {
        if (player == null) return;
        _ultimateReadyTracker.Sync(player, CurrentTurnOwner, CurrentState);
    }

    public void ResetUltimateReadyTracker() => _ultimateReadyTracker.Reset();

    public bool ShouldDeferPlayerSummonGlow(PlayerStatus player)
        => _ultimateReadyTracker.ShouldDeferPlayerSummonGlow(player);

    public void ReleaseUltimateReadyPlayerSummonGlow()
    {
        _ultimateReadyTracker.ReleasePlayerSummonGlow();
        RefreshSummonSkillButtonInteractables();
        if (BattleUIManager.I != null)
            BattleUIManager.I.UpdateStatus(playerStatus, enemyStatus);
    }

    bool IBattlePhaseControllerHost.TryConsumeUltimateReadyPresentation()
        => _ultimateReadyTracker.TryConsumePendingPresentation(playerStatus);
}
