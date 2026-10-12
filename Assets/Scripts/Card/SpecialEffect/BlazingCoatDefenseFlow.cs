using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Blazing Coat: thaw freeze on the defender when used in defense.
/// </summary>
public static class BlazingCoatDefenseFlow
{
    public static async Task TryThawDefenderOnUseAsync(
        PlayerStatus defender,
        IReadOnlyList<CardData> defenseCards,
        CancellationToken cancellationToken = default)
    {
        if (defender == null || !BlazingCoatRules.ContainsBlazingCoat(defenseCards))
            return;
        if (!defender.HasFreezeEffect())
            return;

        cancellationToken.ThrowIfCancellationRequested();

        defender.RemoveStatusEffectsOfType(StatusEffectType.Freeze);

        var bm = BattleManager.I;
        if (bm != null)
            BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());

        if (BattleUIManager.I == null) return;

        float fadeSec = BattleUIManager.I.ShowStyledMessagePopup(defender, MessagePopupKind.FreezeMelted);
        if (fadeSec > 0f)
            await MessagePopup.WaitAfterPopupLifetimeAsync(fadeSec, cancellationToken);
    }
}
