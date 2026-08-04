using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DeadlyChainPostDeathEffect",
    menuName = "DivineField/Post Death Card Effects/Deadly Chain")]
public sealed class DeadlyChainPostDeathEffectSO : PostDeathCardEffectSO
{
    [Tooltip("Fixed attack power (all blessing / weakness modifiers ignored).")]
    public int fixedAttackPower = 30;

    public ElementType attackElement = ElementType.Dark;

    public override Task ResolvePostDeathAsync(
        CardData card,
        PlayerStatus deadOwner,
        PlayerStatus opponent,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        CancellationToken cancellationToken) =>
        DeadlyChainFlow.ResolveSingleChainAsync(
            card,
            deadOwner,
            opponent,
            this,
            battleManager,
            battleProcessor,
            handRefill,
            enemyAI,
            cancellationToken);
}
