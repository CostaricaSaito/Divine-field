using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RealityBendingDisasterEffect",
    menuName = "DivineField/Disaster Effects/Reality Bending (現実改変)")]
public sealed class RealityBendingDisasterEffectSO : DisasterCardEffectSO
{
    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleManager == null)
            return;

        var bm = context.BattleManager;
        var handRefill = bm.HandRefill;
        if (handRefill == null)
        {
            Debug.LogWarning("[RealityBendingDisasterEffectSO] HandRefillService is null");
            return;
        }

        await handRefill.RunRealityBendingAsync(bm.playerHand, bm.cpuHand, cancellationToken);
    }
}
