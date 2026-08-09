using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Host callbacks for <see cref="BattleOpeningCoordinator"/>.
/// </summary>
public interface IBattleOpeningHost
{
    MonoBehaviour CoroutineRunner { get; }
    CardDealer CardDealer { get; }
    List<CardData> PlayerHand { get; }
    List<CardData> CpuHand { get; }
    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    CutInController CutInController { get; }
    float CutInDelaySeconds { get; }

    PlayerType GetCurrentTurnOwner();
    void SetCurrentTurnOwner(PlayerType owner);

    void SetGameState(GameState state);
    void UpdateBattleStatusUi();
    void SetIntroModeUi();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    BattleDebugTools BattleDebugTools { get; }
#endif
}
