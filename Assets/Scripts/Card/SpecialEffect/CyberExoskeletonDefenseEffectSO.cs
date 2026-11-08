using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CyberExoskeletonDefenseEffect",
    menuName = "DivineField/Defense Card Effects/Cyber Exoskeleton (サイバネティック装甲)")]
public sealed class CyberExoskeletonDefenseEffectSO : DefenseIncludedPostDamageEffectSO
{
    public override async Task RunAfterCombatDamageIncludedAsync(
        DefenseIncludedPostDamageContext context,
        CardData triggeringCard,
        CancellationToken cancellationToken)
    {
        if (context?.Defender == null) return;

        cancellationToken.ThrowIfCancellationRequested();

        var (result, fadeSec) = context.Defender.TryApplyStatusEffect(
            StatusEffectType.CyberPsychosis,
            null,
            suppressGrantPopupAndSound: false);
        if (fadeSec > 0f)
            await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (result == ProgressiveApplyResult.Applied)
        {
            BattleUIManager.I?.UpdateStatus(
                BattleManager.I?.GetPlayerStatus(),
                BattleManager.I?.GetEnemyStatus());
        }
    }
}
