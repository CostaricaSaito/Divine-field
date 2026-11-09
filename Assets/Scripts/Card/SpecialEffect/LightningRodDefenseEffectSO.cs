using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Marker for Lightning Rod (避雷針). Resolution lives in <see cref="LightningRodRules"/>.
/// </summary>
[CreateAssetMenu(
    fileName = "LightningRodDefenseEffect",
    menuName = "DivineField/Defense Card Effects/Lightning Rod")]
public sealed class LightningRodDefenseEffectSO : SpecialCardEffectSO
{
    public override Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
