using System.Collections.Generic;

/// <summary>
/// Host callbacks for <see cref="AdHocDefenseCoordinator"/> (implemented by <see cref="BattleManager"/>).
/// </summary>
public interface IAdHocDefenseHost
{
    GameState CurrentState { get; }
    PlayerType Defender { get; }
    PlayerStatus PlayerStatus { get; }
    IReadOnlyList<CardData> PlayerHand { get; }
    bool IsOnlineMatch { get; }

    bool IsProcessingUseButton { get; set; }

    void ResetPlayerDefenseUseButtonLocks();
    void ClearSelectedCards();
    void ClearStatsDisplaySequenceCards();
    void SetSelectedDefenseCard(CardData card);
    void RefreshReflectionChainInteractivity(List<CardData> attackSnapshot);
    void RefreshPlayerDefensePhaseInteractivity();
    void UpdateTotalATKDEFDisplay();
    List<CardData> GetAttackCardsForCombat();
    bool TryAutoPassPlayerDefenseIfChantingArchMagic();
}
