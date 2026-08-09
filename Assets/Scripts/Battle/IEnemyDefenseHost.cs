using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Host callbacks for <see cref="EnemyDefenseResolver"/>.
/// </summary>
public interface IEnemyDefenseHost
{
    BattleManager Manager { get; }
    PlayerType Attacker { get; }
    PlayerType Defender { get; }

    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    List<CardData> PlayerHand { get; }
    List<CardData> CpuHand { get; }

    EnemyAI EnemyAI { get; }
    BattleProcessor BattleProcessor { get; }
    HandRefillService HandRefill { get; }
    CardSequenceManager CardSequenceManager { get; }
    CardStatsDisplay CardStatsDisplay { get; }

    CardData SelectedDefenseCard { get; set; }
    CardData CurrentAttackCard { get; set; }
    bool IsOnlineMatch { get; }

    CancellationToken GetPhaseToken();

    List<CardData> GetAttackCardsForCombat();

    Task<bool> TryHandleDeathIfAnyAsync(CancellationToken ct);
    void SetGameState(GameState state);

    void ClearMagicalExplosionComboMpPoolSnapshot();
    void ClearMillionDollarBazookaComboGpPoolSnapshot();
    void ClearTributeBloodHpPaidSnapshot();
    void ClearHammadnessRollSnapshot();
    void SetSuppressEnemyStaleAttackerInTotalByOrb(bool value);
    void UpdateCardStatsDisplay();
    void ClearCardStatsSequenceAndAttackLocks();
}
