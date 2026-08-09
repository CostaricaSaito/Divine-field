using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Host callbacks for <see cref="BattlePhaseController"/>.
/// </summary>
public interface IBattlePhaseControllerHost
{
    BattleManager Manager { get; }
    GameState CurrentState { get; }
    void SetCurrentState(GameState state);
    PlayerType CurrentTurnOwner { get; set; }
    PlayerType Attacker { get; }
    PlayerType Defender { get; }

    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    List<CardData> PlayerHand { get; }
    List<CardData> CpuHand { get; }
    Transform HandPanel { get; }

    bool IsOnlineMatch { get; }
    bool ShouldGrayOutCards { get; set; }
    bool IsProcessingUseButton { get; set; }

    CardData CurrentAttackCard { get; set; }
    CardData SelectedDefenseCard { get; set; }

    EnemyAI EnemyAI { get; }
    EnemyTurnRunner EnemyTurn { get; }
    EnemyDefenseResolver EnemyDefense { get; }
    OnlineBattleSyncService OnlineSync { get; }
    GameEndOrchestrator GameEnd { get; }
    CardSequenceManager CardSequenceManager { get; }
    CardStatsDisplay CardStatsDisplay { get; }
    HandRefillService HandRefill { get; }
    BuyFeature BuyFeature { get; }
    SellFeature SellFeature { get; }
    SummonTurnCounterState SummonTurnCounters { get; }

    void ClearPlayerSelfAttackTargetMode();
    void ClearConfusionAttackTargetResolvedForDisplay();
    void ClearOnlineEnemyAttackCombo();
    void ClearMagicalSwordEnemyAttackState();
    void SetSuppressEnemyStaleAttackerInTotalByOrb(bool value);
    void ClearReflectionAttackTotalDisplay();
    void UpdateTotalATKDEFDisplay();
    void RefreshPlayerDefensePhaseInteractivity();
    void RefreshSummonSkillButtonInteractables();
    bool TryAutoPassPlayerDefenseIfChantingArchMagic();
    List<CardData> GetAttackCardsForCombat();
    void ToggleTurnOwner();
    Task<bool> TryHandleDeathIfAnyAsync(CancellationToken ct);
}
