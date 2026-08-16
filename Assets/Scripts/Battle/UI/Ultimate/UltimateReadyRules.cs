using UnityEngine;

/// <summary>
/// Ultimate Skill availability for the player summon button and Ultimate Ready presentation.
/// </summary>
public static class UltimateReadyRules
{
    public static bool HasUltimateSkill(SummonData summon)
    {
        if (summon == null) return false;
        if (summon.ultimateSkillCard != null) return true;
        if (BahamutRules.IsBahamut(summon)) return true;
        return !string.IsNullOrWhiteSpace(summon.ultimateSkillName);
    }

    public static bool IsAvailable(PlayerStatus ps)
    {
        if (ps == null || ps.hasUsedUltimateSkill) return false;
        if (!HasUltimateSkill(ps.summonData)) return false;
        return DisadvantageRules.IsDisadvantaged(ps);
    }
}

/// <summary>
/// Tracks when the ultimate-ready presentation should play on the next player StandBy.
/// </summary>
public sealed class UltimateReadyStateTracker
{
    private bool _wasAvailable;
    private bool _pendingPresentation;
    private bool _waitingForEnemyTurnBeforeShow;
    private bool _deferPlayerSummonGlow;

    public bool ShouldDeferPlayerSummonGlow(PlayerStatus player)
    {
        if (player == null || !UltimateReadyRules.IsAvailable(player))
            return false;
        return _deferPlayerSummonGlow || _pendingPresentation;
    }

    public void Reset()
    {
        _wasAvailable = false;
        _pendingPresentation = false;
        _waitingForEnemyTurnBeforeShow = false;
        _deferPlayerSummonGlow = false;
    }

    public void Sync(PlayerStatus player, PlayerType currentTurnOwner, GameState currentState)
    {
        bool available = UltimateReadyRules.IsAvailable(player);

        if (currentTurnOwner == PlayerType.Enemy)
            _waitingForEnemyTurnBeforeShow = false;

        if (available && !_wasAvailable)
        {
            _pendingPresentation = true;
            if (currentTurnOwner == PlayerType.Player && currentState != GameState.StandByPhase)
                _waitingForEnemyTurnBeforeShow = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                "[UltimateReady] Conditions met. " +
                $"Pending presentation after enemy turn: {_waitingForEnemyTurnBeforeShow}");
#endif
        }

        if (!available)
        {
            _pendingPresentation = false;
            _waitingForEnemyTurnBeforeShow = false;
            _deferPlayerSummonGlow = false;
        }

        _wasAvailable = available;
    }

    public bool TryConsumePendingPresentation(PlayerStatus player)
    {
        if (!_pendingPresentation) return false;
        if (_waitingForEnemyTurnBeforeShow) return false;
        if (!UltimateReadyRules.IsAvailable(player)) return false;

        _pendingPresentation = false;
        _deferPlayerSummonGlow = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[UltimateReady] Playing presentation on player StandBy.");
#endif
        return true;
    }

    public void ReleasePlayerSummonGlow()
    {
        _deferPlayerSummonGlow = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[UltimateReady] Player summon glow released.");
#endif
    }
}
