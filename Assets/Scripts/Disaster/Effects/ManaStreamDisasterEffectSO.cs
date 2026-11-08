using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ManaStreamDisasterEffect",
    menuName = "DivineField/Disaster Effects/Mana Stream (マナの奔流)")]
public sealed class ManaStreamDisasterEffectSO : DisasterCardEffectSO
{
    public override Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleManager == null)
            return Task.CompletedTask;

        var bm = context.BattleManager;
        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        if (player == null || enemy == null)
            return Task.CompletedTask;

        InfiniteManasourceRules.TryGrant(player);
        InfiniteManasourceRules.TryGrant(enemy);

        BattleUIManager.I?.UpdateStatus(player, enemy);
        return Task.CompletedTask;
    }
}
