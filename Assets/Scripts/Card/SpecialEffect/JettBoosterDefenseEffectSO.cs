using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "JettBoosterDefenseEffect",
    menuName = "DivineField/Defense Card Effects/Jett Booster (ジェットブースター)")]
public sealed class JettBoosterDefenseEffectSO : DefenseIncludedPostDamageEffectSO
{
    public override async Task RunAfterCombatDamageIncludedAsync(
        DefenseIncludedPostDamageContext context,
        CardData triggeringCard,
        CancellationToken cancellationToken)
    {
        if (context?.Defender == null || triggeringCard == null) return;

        var bm = BattleManager.I;
        if (bm?.HandRefill == null) return;

        bool defenderIsPlayer = ReferenceEquals(context.Defender, bm.GetPlayerStatus());
        Side displaySide = defenderIsPlayer ? Side.Player : Side.Enemy;
        var hand = defenderIsPlayer ? bm.playerHand : bm.cpuHand;
        if (hand == null) return;

        await DefenseIncludedPresentation.RunTriggerCardElementGlowAsync(
            triggeringCard, displaySide, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        PlayerType drawSide = defenderIsPlayer ? PlayerType.Player : PlayerType.Enemy;
        if (defenderIsPlayer)
        {
            var drawn = await bm.HandRefill.DrawCardAsync(hand, trailingDelayMs: 200, playSoundOnDraw: true);
            if (drawn != null)
                await bm.HandRefill.RevealDrawnCardAfterCombatAsync(drawn, cancellationToken);
        }
        else
        {
            bm.HandRefill.DrawCardDataOnly(hand, drawSide);
            await Task.Delay(50, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var discardRequest = JettBoosterRules.CreateDiscardRequest();
        await HandDiscardSelectionFlow.RunDiscardFromHandAsync(
            discardRequest,
            bm,
            hand,
            defenderIsPlayer,
            displaySide,
            cancellationToken);

        BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
    }
}
