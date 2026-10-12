using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CyberExoskeletonDefenseEffect",
    menuName = "DivineField/Defense Card Effects/Cyber Exoskeleton (サイバネティック装甲)")]
public sealed class CyberExoskeletonDefenseEffectSO : SpecialCardEffectSO
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
