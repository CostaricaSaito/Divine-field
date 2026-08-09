using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "InfectionDisasterEffect",
    menuName = "DivineField/Disaster Effects/Infection (感染症)")]
public sealed class InfectionDisasterEffectSO : DisasterCardEffectSO
{
    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleProcessor == null || context.BattleManager == null)
            return;

        await context.BattleProcessor.ApplyDisasterInfectionAsync(
            context.BattleManager.GetPlayerStatus(),
            context.BattleManager.GetEnemyStatus(),
            cancellationToken);
    }
}
