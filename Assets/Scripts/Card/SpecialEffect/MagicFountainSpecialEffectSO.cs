using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MagicFountainSpecialEffect",
    menuName = "DivineField/Special Card Effects/Magic Fountain (魔力の泉)")]
public sealed class MagicFountainSpecialEffectSO : SpecialCardEffectSO
{
    public override Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        return MagicFountainLifecycle.RunAsync(
            BattleManager.I,
            card,
            user,
            effectTarget,
            cancellationToken);
    }
}
