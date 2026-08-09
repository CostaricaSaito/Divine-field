using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BlackMondayDisasterEffect",
    menuName = "DivineField/Disaster Effects/Black Monday (大暴落)")]
public sealed class BlackMondayDisasterEffectSO : DisasterCardEffectSO
{
    private const int TargetGp = 0;

    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleManager == null)
            return;

        var bm = context.BattleManager;
        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        if (player == null || enemy == null) return;

        int playerGpBefore = player.currentGP;
        int enemyGpBefore = enemy.currentGP;

        player.currentGP = TargetGp;
        enemy.currentGP = TargetGp;

        var ui = BattleUIManager.I;
        if (ui == null) return;

        ui.UpdateStatus(player, enemy);

        int countdownMs = System.Math.Max(
            BattleStatCountRules.EstimateCountdownDurationMs(playerGpBefore, TargetGp),
            BattleStatCountRules.EstimateCountdownDurationMs(enemyGpBefore, TargetGp));
        if (countdownMs > 0)
            await Task.Delay(countdownMs, cancellationToken);
    }
}
