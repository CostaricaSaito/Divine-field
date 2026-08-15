using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ArrowOfIndraSpecialEffect",
    menuName = "DivineField/Special Card Effects/Arrow of Indra (インドラの矢)")]
public sealed class ArrowOfIndraSpecialEffectSO : SpecialCardEffectSO
{
    public override Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        return ArrowOfIndraLifecycle.RunAsync(
            BattleManager.I,
            card,
            user,
            effectTarget,
            cancellationToken);
    }
}
