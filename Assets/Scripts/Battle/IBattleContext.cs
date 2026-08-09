using System.Collections.Generic;

/// <summary>
/// Read-only battle context for subsystems extracted from <see cref="BattleManager"/>.
/// Phase 0: minimal surface; expanded as coordinators are split out.
/// </summary>
public interface IBattleContext
{
    CombatSnapshotStore CombatSnapshots { get; }

    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }

    GameState CurrentState { get; }
    PlayerType CurrentTurnOwner { get; }
    PlayerType Attacker { get; }

    bool IsOnlineMatch { get; }

    IReadOnlyList<CardData> PlayerHand { get; }
    IReadOnlyList<CardData> CpuHand { get; }
}
