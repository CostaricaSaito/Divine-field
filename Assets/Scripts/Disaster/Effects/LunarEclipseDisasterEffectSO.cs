using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LunarEclipseDisasterEffect",
    menuName = "DivineField/Disaster Effects/Lunar Eclipse (月蝕)")]
public sealed class LunarEclipseDisasterEffectSO : DisasterCardEffectSO
{
    private const int TargetHp = 1;

    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleManager == null)
            return;

        var bm = context.BattleManager;
        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        if (player == null || enemy == null) return;

        int playerHpBefore = player.currentHP;
        int enemyHpBefore = enemy.currentHP;

        player.currentHP = TargetHp;
        enemy.currentHP = TargetHp;

        var ui = BattleUIManager.I;
        if (ui == null) return;

        ui.UpdateStatus(player, enemy);

        int countdownMs = System.Math.Max(
            BattleStatCountRules.EstimateCountdownDurationMs(playerHpBefore, TargetHp),
            BattleStatCountRules.EstimateCountdownDurationMs(enemyHpBefore, TargetHp));
        if (countdownMs > 0)
            await Task.Delay(countdownMs, cancellationToken);
    }
}
