using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MagicSealerSpecialEffect",
    menuName = "DivineField/Special Card Effects/Magic Sealer (魔力封印の呪印)")]
public sealed class MagicSealerSpecialEffectSO : SpecialCardEffectSO
{
    public override Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        return MagicSealerLifecycle.RunAsync(
            BattleManager.I,
            user,
            effectTarget,
            cancellationToken);
    }
}
