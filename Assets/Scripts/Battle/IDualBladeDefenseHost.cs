using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Host callbacks for <see cref="DualBladeDefenseCoordinator"/>.
/// </summary>
public interface IDualBladeDefenseHost
{
    PlayerType Attacker { get; }
    PlayerType Defender { get; }
    PlayerStatus PlayerStatus { get; }
    PlayerStatus EnemyStatus { get; }
    CardData CurrentAttackCard { get; }
    List<CardData> PlayerHand { get; }
    List<CardData> GetAttackCardsForCombat();

    bool IsProcessingUseButton { get; set; }

    void SetSelectedDefenseCard(CardData card);
    void ResetPlayerDefenseUseButtonLocks();
    void RefreshPlayerDefensePhaseInteractivity();
    void TryAutoPassPlayerDefenseIfChantingArchMagic();
    void ClearCardStatsSequence();
    void SetEnemyAttackSequenceDisplay(List<CardData> attackCards);
    void UpdateCardStatsDisplay();
}
