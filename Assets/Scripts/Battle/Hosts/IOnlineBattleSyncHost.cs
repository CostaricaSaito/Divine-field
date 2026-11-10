using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Host callbacks for <see cref="OnlineBattleSyncService"/>.
/// </summary>
public interface IOnlineBattleSyncHost
{
    BattleManager Manager { get; }
    bool IsOnlineMatch { get; }
    bool IsGameEndTriggered { get; }
    bool IsOpponentForfeitPending { get; }

    int GetOnlineTurnTag();

    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    List<CardData> PlayerHand { get; }
    List<CardData> CpuHand { get; }

    BattleProcessor BattleProcessor { get; }
    HandRefillService HandRefill { get; }
    CardDealer CardDealer { get; }
    SummonTurnCounterState SummonTurnCounters { get; }

    PlayerType CurrentTurnOwner { get; set; }
    int MaxHandCards { get; }

    Task<bool> TryHandleDeathIfAnyAsync(CancellationToken ct);

    void UpdateBattleStatusUi();
    void RefreshTurnCountDisplay();
    void RefreshArchMagicBarrierUi(PlayerStatus status);
    void UpdateTotalATKDefDisplay();
    void RefreshPlayerDefensePhaseInteractivity();
    void SetIntroModeUi();

    PlayerStatus ResolveArchMagicEffectTarget(PlayerStatus status, bool targetSelf);
}
