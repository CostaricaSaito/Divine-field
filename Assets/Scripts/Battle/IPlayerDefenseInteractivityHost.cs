using System.Collections.Generic;

/// <summary>
/// Host callbacks for <see cref="PlayerDefenseInteractivityService"/>.
/// </summary>
public interface IPlayerDefenseInteractivityHost
{
    PlayerStatus PlayerStatus { get; }
    List<CardData> PlayerHand { get; }
    bool IsOnlineMatch { get; }

    AdHocDefenseCoordinator AdHocDefense { get; }
    PlayerInputController PlayerInput { get; }

    bool IsPlayerDefenseInputActive();
    List<CardData> GetIncomingAttackSnapshotForDefenseUi();

    void HandleNoDefenseCard();
    void CompleteAdHocDefenseSubmit(List<CardData> selectedDefenseCards);
    void UpdateTotalATKDEFDisplay();
}
