using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ChaosAttractorDisasterEffect",
    menuName = "DivineField/Disaster Effects/Chaos Attractor (原初の混沌)")]
public sealed class ChaosAttractorDisasterEffectSO : DisasterCardEffectSO
{
    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleManager == null)
            return;

        var bm = context.BattleManager;
        var handRefill = bm.HandRefill;
        if (handRefill == null)
        {
            Debug.LogWarning("[ChaosAttractorDisasterEffectSO] HandRefillService is null");
            return;
        }

        await handRefill.RunChaosAttractorAsync(bm.playerHand, bm.cpuHand, cancellationToken);
    }
}
