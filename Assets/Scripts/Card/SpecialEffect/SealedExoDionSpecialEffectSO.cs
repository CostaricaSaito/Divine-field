using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SealedExoDionSpecialEffect",
    menuName = "DivineField/Special Card Effects/Sealed EXO-DION (封印されし装甲神)")]
public sealed class SealedExoDionSpecialEffectSO : SpecialCardEffectSO
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
