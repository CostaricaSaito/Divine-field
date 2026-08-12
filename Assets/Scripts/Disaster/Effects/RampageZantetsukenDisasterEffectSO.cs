using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RampageZantetsukenDisasterEffect",
    menuName = "DivineField/Disaster Effects/Rampage Zantetsuken (暴走斬鉄剣)")]
public sealed class RampageZantetsukenDisasterEffectSO : DisasterCardEffectSO
{
    public override async Task ResolveAsync(DisasterResolveContext context, CancellationToken cancellationToken)
    {
        if (context?.BattleManager == null || context.CombatCardTemplate == null || context.DisplayCard == null)
            return;

        var bm = context.BattleManager;
        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        if (player == null || enemy == null || context.TriggerOwner == null)
            return;

        var opponent = ReferenceEquals(context.TriggerOwner, player) ? enemy : player;
        PlayerType rollSide = ReferenceEquals(context.TriggerOwner, player)
            ? PlayerType.Player
            : PlayerType.Enemy;
        bool targetSelf = BattleRandom.DrawRange(rollSide, 0, 2) == 0;

        PlayerStatus attacker = context.TriggerOwner;
        PlayerStatus defender = targetSelf ? context.TriggerOwner : opponent;

        var combatCard = Object.Instantiate(context.CombatCardTemplate);
        combatCard.cardUI = null;
        try
        {
            await DisasterCombatRunner.RunRampageStrikeAsync(
                bm,
                context.Sequences,
                context.BattleProcessor,
                attacker,
                defender,
                context.DisplayCard,
                combatCard,
                context.TriggerSide,
                targetSelf,
                cancellationToken);
        }
        finally
        {
            Object.Destroy(combatCard);
        }
    }
}
