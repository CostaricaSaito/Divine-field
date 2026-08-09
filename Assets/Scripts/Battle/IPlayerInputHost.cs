using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Host callbacks for <see cref="PlayerInputController"/>.
/// </summary>
public interface IPlayerInputHost
{
    BattleManager Manager { get; }
    GameState CurrentState { get; }
    PlayerType Attacker { get; }
    PlayerType Defender { get; }
    bool IsOnlineMatch { get; }
    bool IsPlayerSelfAttackTargetMode { get; }

    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    List<CardData> PlayerHand { get; }
    List<CardData> CpuHand { get; }

    BattleProcessor BattleProcessor { get; }
    HandRefillService HandRefill { get; }
    CardSequenceManager CardSequenceManager { get; }
    CardStatsDisplay CardStatsDisplay { get; }
    SellFeature SellFeature { get; }

    CardData SelectedCard { get; set; }
    CardData SelectedDefenseCard { get; set; }
    CardData CurrentAttackCard { get; set; }

    CancellationToken GetPhaseToken();

    bool IsPlayerDefenseInputActive();
    bool IsReactiveAdHocDefenseWaitActive();
    bool IsAdHocDefenseWaitActive();
    void TrySubmitAdHocPlayerDefense();
    bool IsPlayerChantingArchMagicWhileDefending();
    bool TryAutoPassPlayerDefenseIfChantingArchMagic();

    IReadOnlyList<CardData> GetIncomingAttackSnapshotForDefenseUi();
    List<CardData> GetAttackCardsForCombat();

    void SetGameState(GameState state);
    Task<bool> TryHandleDeathIfAnyAsync(CancellationToken ct);
    Task<bool> TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(CancellationToken ct);

    void ClearPlayerSelfAttackTargetMode();

    Task PlayAttackConfirmPresentationAsync(CardData card, Side side, CancellationToken ct);
    void SetStatsDisplaySequenceCards(List<CardData> cards, string cardType, Side ownerSide);
    Task RunAfterCombatSharedCleanupAsync(CancellationToken ct);

    Task<CardData> DrawOneCardAsync(int trailingDelayMs, bool playSoundOnDraw);
    void DrawOneCard();
    int GetHandMaxCount();

    void UpdateTotalATKDefDisplay();
    void UpdateBattleStatusUi();

    void ClearMagicalExplosionComboMpPoolSnapshot();
    void ClearMillionDollarBazookaComboGpPoolSnapshot();
    void ClearTributeBloodHpPaidSnapshot();
    void ClearHammadnessRollSnapshot();
    void ClearCardStatsSequenceAndAttackLocks();
}
