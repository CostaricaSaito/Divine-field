using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BlazingCoatDefenseEffect",
    menuName = "DivineField/Defense Card Effects/Blazing Coat (ブレイジングコート)")]
public sealed class BlazingCoatDefenseEffectSO : SpecialCardEffectSO
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
