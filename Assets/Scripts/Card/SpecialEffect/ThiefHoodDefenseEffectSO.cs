using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ThiefHoodDefenseEffect",
    menuName = "DivineField/Defense Card Effects/Thief Hood (盗人のフード)")]
public sealed class ThiefHoodDefenseEffectSO : SpecialCardEffectSO
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
