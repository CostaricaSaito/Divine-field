using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Freeze decay at turn owner's EndPhase and melt popup.
/// </summary>
public static class FreezeTurnEndProcessor
{
    public static async Task ProcessTurnOwnerDecayAsync(PlayerStatus turnOwner, CancellationToken ct)
    {
        if (turnOwner == null) return;

        var freeze = turnOwner.GetFreezeEffect();
        if (freeze == null) return;

        freeze.DecrementAtTurnEnd();
        if (!freeze.IsExpired()) return;

        turnOwner.activeEffects.Remove(freeze);
        UnityEngine.Debug.Log($"{turnOwner.DisplayName} freeze expired at turn end");

        var ui = BattleUIManager.I;
        if (ui != null)
            ui.UpdateStatus(BattleManager.I?.GetPlayerStatus(), BattleManager.I?.GetEnemyStatus());

        if (ui == null) return;

        float fadeSec = ui.ShowStyledMessagePopup(turnOwner, MessagePopupKind.FreezeMelted);
        if (fadeSec > 0f)
            await MessagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
    }
}
