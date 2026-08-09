using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "KannadukiDisasterEffect",
    menuName = "DivineField/Disaster Effects/Kannaduki (神無月)")]
public sealed class KannadukiDisasterEffectSO : DisasterCardEffectSO
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

        var config = StatusProgressionConfig.GetRuntimeFallback();
        TryGrantKannaduki(player, config);
        TryGrantKannaduki(enemy, config);

        BattleUIManager.I?.UpdateStatus(player, enemy);
        return Task.CompletedTask;
    }

    private static void TryGrantKannaduki(PlayerStatus target, StatusProgressionConfig config)
    {
        if (target == null) return;
        target.TryApplyStatusEffect(
            StatusEffectType.Kannaduki,
            config,
            suppressGrantPopupAndSound: true);
    }
}
