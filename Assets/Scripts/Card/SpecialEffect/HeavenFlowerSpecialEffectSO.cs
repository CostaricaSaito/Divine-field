using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "HeavenFlowerSpecialEffect",
    menuName = "DivineField/Special Card Effects/Heaven Flower (楽園の花)")]
public sealed class HeavenFlowerSpecialEffectSO : SpecialCardEffectSO
{
    [Range(0, 100)]
    [Tooltip("0〜100。乱数がこの値未満なら付与（OnCardEffectResolve と同じ判定）。")]
    public int statusEffectChance = 100;

    public StatusEffectType statusEffectToApply = StatusEffectType.ParadiseSickness;

    public override async Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        if (battleProcessor == null || effectTarget == null || statusEffectToApply == StatusEffectType.None)
            return;

        await battleProcessor.TryApplyStatusOnCardEffectResolveAsync(
            statusEffectToApply,
            statusEffectChance,
            effectTarget,
            cancellationToken);
    }
}
