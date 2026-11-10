using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Host callbacks for <see cref="GameEndOrchestrator"/>.
/// </summary>
public interface IGameEndOrchestratorHost
{
    BattleManager Manager { get; }
    MonoBehaviour HostBehaviour { get; }
    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    BattleProcessor BattleProcessor { get; }
    HandRefillService HandRefill { get; }
    EnemyAI EnemyAI { get; }
    CardStatsDisplay CardStatsDisplay { get; }
    GameObject GameResultPrefab { get; }
    GameObject NpcResultPrefab { get; }
    bool IsOnlineMatch { get; }

    bool IsPostDeathSequenceActive { get; set; }

    void SetGameState(GameState state);
    void SetCurrentStateDirect(GameState state);
    void SetCurrentTurnOwner(PlayerType owner);
    void ResetDefenseInputFlags();

    void ClearPlayerSelfAttackTargetMode();
    void ClearReflectionAttackTotalDisplay();
    void ClearPostDeathChainAttackDisplay();
    void ClearStatsDisplaySequenceCards();
    void SetCurrentAttackCard(CardData card);
    void ClearPlayerAttackComboForCombat();
    void ClearEnemyAttackComboForCombat();
    void ResetPlayerDefenseUseButtonLocks();
    void ClearSelectedCards();
    void UpdateTotalATKDEFDisplay();
}
