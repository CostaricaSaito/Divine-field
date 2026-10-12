using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// サイバネティック装甲: 防御解決後に電脳サイコシスを防御者へ付与。
/// </summary>
public static class CyberExoskeletonDefenseFlow
{
    public static async Task TryGrantCyberPsychosisAfterDefenseAsync(
        IReadOnlyList<CardData> defenseCards,
        PlayerStatus defender,
        CancellationToken cancellationToken = default)
    {
        if (defender == null || !CyberExoskeletonRules.ContainsCyberExoskeleton(defenseCards))
            return;

        cancellationToken.ThrowIfCancellationRequested();

        var (result, fadeSec) = defender.TryApplyStatusEffect(StatusEffectType.CyberPsychosis, null);
        if (fadeSec > 0f)
            await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (result == ProgressiveApplyResult.Applied)
            BattleUIManager.I?.UpdateStatus(
                BattleManager.I?.GetPlayerStatus(),
                BattleManager.I?.GetEnemyStatus());
    }
}
