using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// HP0 後の <see cref="PostDeathEffectProcessor"/> キューから解決される効果。
/// 攻撃／防御フェーズでは使用不可（<see cref="CardData.usableInAttackPhase"/> 等も false）。
/// </summary>
public abstract class PostDeathCardEffectSO : ScriptableObject
{
    public abstract Task ResolvePostDeathAsync(
        CardData card,
        PlayerStatus deadOwner,
        PlayerStatus opponent,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        EnemyAI enemyAI,
        CancellationToken cancellationToken);
}
