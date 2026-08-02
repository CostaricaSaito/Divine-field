using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DiscardRestartSpecialEffect",
    menuName = "DivineField/Special Card Effects/Discard Restart (運命の宝札)")]
public sealed class DiscardRestartSpecialEffectSO : SpecialCardEffectSO
{
    public override async Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        var bm = BattleManager.I;
        if (bm == null || bm.HandRefill == null || effectTarget == null) return;

        bool targetIsPlayer = ReferenceEquals(effectTarget, bm.GetPlayerStatus());
        CardData exclude = ReferenceEquals(user, effectTarget) ? card : null;

        if (targetIsPlayer)
        {
            await bm.HandRefill.RunPlayerHandDiscardRestartAsync(
                bm.playerHand,
                effectTarget,
                exclude,
                cancellationToken);
        }
        else
        {
            await bm.HandRefill.RunEnemyHandDiscardRestartAsync(
                bm.cpuHand,
                effectTarget,
                exclude,
                cancellationToken);
        }
    }
}
