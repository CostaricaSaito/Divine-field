using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Host callbacks for <see cref="EnemyTurnRunner"/>.
/// </summary>
public interface IEnemyTurnHost
{
    BattleManager Manager { get; }
    GameState CurrentState { get; }
    bool IsOnlineMatch { get; }

    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    List<CardData> CpuHand { get; }

    EnemyAI EnemyAI { get; }
    BattleProcessor BattleProcessor { get; }
    HandRefillService HandRefill { get; }
    CardSequenceManager CardSequenceManager { get; }
    CardStatsDisplay CardStatsDisplay { get; }
    CombatSnapshotStore CombatSnapshots { get; }
    DualBladeDefenseCoordinator DualBladeDefense { get; }

    CancellationToken GetPhaseToken();

    CardData CurrentAttackCard { get; set; }

    void SetGameState(GameState state);

    Task PlayAttackConfirmPresentationAsync(CardData card, Side side, CancellationToken ct);
    Task RunAfterCombatSharedCleanupAsync(CancellationToken ct);
    Task<bool> ResolveSelfTargetAttackAsync(List<CardData> atkList, CancellationToken ct);

    void SetConfusionAttackTargetResolvedForDisplay(bool targetsSelf);
    void ClearMagicalExplosionComboMpPoolSnapshot();
    void ClearMillionDollarBazookaComboGpPoolSnapshot();
    void ClearTributeBloodHpPaidSnapshot();
    void ClearHammadnessRollSnapshot();
    void ClearMagicalSwordEnemyAttackState();
    void UpdateBattleStatusUi();
    void UpdateCardStatsDisplay();
    void ClearCardStatsSequenceAndAttackLocks();
}
