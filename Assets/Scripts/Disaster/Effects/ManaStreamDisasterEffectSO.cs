using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ManaStreamDisasterEffect",
    menuName = "DivineField/Disaster Effects/Mana Stream (マナの奔流)")]
public sealed class ManaStreamDisasterEffectSO : DisasterCardEffectSO
{
    private const int MpRecover = 50;

    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.TriggerOwner == null || context.BattleProcessor == null)
            return;

        await context.BattleProcessor.ApplyDisasterTriggerOwnerRecoveryAsync(
            context.TriggerOwner,
            hpRecover: 0,
            mpRecover: MpRecover,
            cancellationToken);
    }
}
