using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ShiningBarriarSpecialEffect",
    menuName = "DivineField/Special Card Effects/Shining Barrier (光のバリア)")]
public sealed class ShiningBarrierSpecialEffectSO : SpecialCardEffectSO
{
    public override Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
