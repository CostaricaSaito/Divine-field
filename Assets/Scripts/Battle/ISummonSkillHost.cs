using System.Collections.Generic;

using System.Threading;

using System.Threading.Tasks;

using UnityEngine;



/// <summary>

/// Host callbacks for <see cref="SummonSkillCoordinator"/>.

/// </summary>

public interface ISummonSkillHost

{

    MonoBehaviour HostBehaviour { get; }

    bool IsOnlineMatch { get; }

    GameState CurrentState { get; }

    PlayerType CurrentTurnOwner { get; }

    PlayerType Defender { get; }

    PlayerStatus PlayerStatus { get; }

    PlayerStatus EnemyStatus { get; }

    List<CardData> PlayerHand { get; }

    List<CardData> CpuHand { get; }

    BattleStatusUI StatusUI { get; }

    CardSequenceManager Sequences { get; }

    SummonTurnCounterState SummonTurnCounters { get; }

    SummonSkillButton PlayerSummonButton { get; }

    SummonSkillButton EnemySummonButton { get; }

    GameObject BahamutPopupPrefab { get; }



    bool IsEconomicActionInProgress();

    bool IsHandReloadPopupOpen();

    void EnterAttackPhase();

    void RefreshPlayerDefensePhaseInteractivity();

    void ClearAttackSelectionNeutral();

    void SetConfusionAttackTargetResolvedForDisplay(bool targetsSelf);

    void SetCurrentAttackCard(CardData card);

    void SetGameState(GameState state);

    void UpdateCardStatsDisplay();

    void ClearCardStatsSequence();

    Task RunAfterCombatSharedCleanupAsync(CancellationToken cancellationToken);

    Task<bool> ResolveConfusionSelfAttackAsync(

        List<CardData> atkList, CancellationToken cancellationToken);

}

