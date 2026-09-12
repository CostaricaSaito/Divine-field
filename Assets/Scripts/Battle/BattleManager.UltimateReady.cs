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

    /// <summary>Roll disadvantage chance at turn owner's StandBy; latch, BGM, and Ultimate Ready on success.</summary>
    public void TryRollDisadvantageAtStandBy(PlayerType turnOwner)
    {
        var self = turnOwner == PlayerType.Player ? playerStatus : enemyStatus;
        var opponent = turnOwner == PlayerType.Player ? enemyStatus : playerStatus;
        var selfHand = turnOwner == PlayerType.Player ? playerHand : cpuHand;
        var oppHand = turnOwner == PlayerType.Player ? cpuHand : playerHand;
        int turnDisplay = _summonTurnCounters != null ? _summonTurnCounters.CurrentBattleTurnDisplay : 1;

        if (self == null) return;

        bool wasLatched = self.hasEnteredDisadvantage;
        bool latched = DisadvantageRules.TryRollAndLatchAtTurnStart(
            self,
            opponent,
            selfHand != null ? selfHand.Count : 0,
            oppHand != null ? oppHand.Count : 0,
            turnDisplay);

        if (!latched || wasLatched) return;

        if (turnOwner == PlayerType.Player)
            _ultimateReadyTracker.NotifyLatchedAtStandBy();

        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        if (turnOwner == PlayerType.Player)
        {
            BattleBgmController.Instance?.SyncFromPlayer(playerStatus);
            HandRevealPresentation.PrewarmSuperRareRevealVideo();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugForcePlayerDisadvantageLatch()
    {
        if (playerStatus == null) return;
        if (!playerStatus.hasEnteredDisadvantage)
            playerStatus.EnterDisadvantage("[Disadvantage] Debug force latch (no roll).");
        _ultimateReadyTracker.NotifyLatchedAtStandBy();
        BattleUIManager.I?.UpdateStatus(playerStatus, enemyStatus);
        BattleBgmController.Instance?.SyncFromPlayer(playerStatus);
        UnityEngine.Debug.Log("[BattleManager] Player disadvantage latched (debug force).");
    }
#endif
}
