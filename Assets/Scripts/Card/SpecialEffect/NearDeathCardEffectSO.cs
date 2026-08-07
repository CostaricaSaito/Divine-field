using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// HP0 検出後「往生」表示の直前に解決される効果（不死鳥の尾羽根など）。
/// 攻撃／防御フェーズでは手動使用不可（<see cref="CardData.passiveHandOnly"/> 推奨）。
/// </summary>
public abstract class NearDeathCardEffectSO : ScriptableObject
{
    public abstract Task ResolveNearDeathAsync(
        CardData card,
        PlayerStatus owner,
        PlayerStatus opponent,
        PlayerType ownerSide,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        CancellationToken cancellationToken);
}
