using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MiracleArkDisasterEffect",
    menuName = "DivineField/Disaster Effects/Miracle Ark (奇跡の船出)")]
public sealed class MiracleArkDisasterEffectSO : DisasterCardEffectSO
{
    private const int HpRecover = 50;

    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.TriggerOwner == null || context.BattleProcessor == null)
            return;

        await context.BattleProcessor.ApplyDisasterTriggerOwnerRecoveryAsync(
            context.TriggerOwner,
            hpRecover: HpRecover,
            mpRecover: 0,
            cancellationToken);
    }
}
