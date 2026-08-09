using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EruptionDisasterEffect",
    menuName = "DivineField/Disaster Effects/Eruption (噴火)")]
public sealed class EruptionDisasterEffectSO : DisasterCardEffectSO
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
        PlayerStatus firstAttacker = triggerIsPlayer ? player : enemy;
        PlayerStatus firstDefender = triggerIsPlayer ? enemy : player;
        Side firstDisplay = triggerIsPlayer ? Side.Player : Side.Enemy;

        PlayerStatus secondAttacker = firstDefender;
        PlayerStatus secondDefender = firstAttacker;
        Side secondDisplay = triggerIsPlayer ? Side.Enemy : Side.Player;

        var combatCard1 = Object.Instantiate(context.CombatCardTemplate);
        combatCard1.cardUI = null;
        try
        {
            if (!await DisasterCombatRunner.RunStrikeAsync(
                    bm, context.Sequences, context.BattleProcessor,
                    firstAttacker, firstDefender, combatCard1, firstDisplay, cancellationToken))
                return;

            if (bm.IsGameEndTriggered) return;

            ClearDisplaySide(firstDisplay);

            var combatCard2 = Object.Instantiate(context.CombatCardTemplate);
            combatCard2.cardUI = null;
            try
            {
                await DisasterCombatRunner.RunStrikeAsync(
                    bm, context.Sequences, context.BattleProcessor,
                    secondAttacker, secondDefender, combatCard2, secondDisplay, cancellationToken);
            }
            finally
            {
                Object.Destroy(combatCard2);
            }
        }
        finally
        {
            Object.Destroy(combatCard1);
        }
    }

    private static void ClearDisplaySide(Side side)
    {
        BattleUIManager.I?.ClearCardDisplayPanelImmediate(side);
    }
}
