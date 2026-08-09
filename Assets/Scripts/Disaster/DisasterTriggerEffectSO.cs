using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DisasterTriggerEffect",
    menuName = "DivineField/Special Card Effects/Disaster Trigger (天変地異トリガー)")]
public sealed class DisasterTriggerEffectSO : SpecialCardEffectSO
{
    public override Task ResolveOnImmediatePlayAsync(
        CardData card,
        PlayerStatus user,
        PlayerStatus effectTarget,
        BattleProcessor battleProcessor,
        CancellationToken cancellationToken)
    {
        if (user == null || battleProcessor == null)
            return Task.CompletedTask;

        return DisasterOrchestrator.RunFromSpecialCardAsync(card, user, battleProcessor, cancellationToken);
    }
}
