using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SolarEclipseDisasterEffect",
    menuName = "DivineField/Disaster Effects/Solar Eclipse (日蝕)")]
public sealed class SolarEclipseDisasterEffectSO : DisasterCardEffectSO
{
    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleManager == null || context.CombatCardTemplate == null)
            return;

        var bm = context.BattleManager;
        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        if (player == null || enemy == null) return;

        bool triggerIsPlayer = ReferenceEquals(context.TriggerOwner, player);
        PlayerStatus attacker = triggerIsPlayer ? player : enemy;
        PlayerStatus defender = triggerIsPlayer ? enemy : player;
        Side displaySide = triggerIsPlayer ? Side.Player : Side.Enemy;

        var combatCard = Object.Instantiate(context.CombatCardTemplate);
        combatCard.cardUI = null;
        try
        {
            await DisasterCombatRunner.RunStrikeAsync(
                bm, context.Sequences, context.BattleProcessor,
                attacker, defender, combatCard, displaySide, cancellationToken);
        }
        finally
        {
            Object.Destroy(combatCard);
        }
    }
}
