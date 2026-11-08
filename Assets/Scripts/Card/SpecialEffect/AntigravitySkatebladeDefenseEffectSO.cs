using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AntigravitySkatebladeDefenseEffect",
    menuName = "DivineField/Defense Card Effects/Antigravity Skateblade (反重力ローラースケート)")]
public sealed class AntigravitySkatebladeDefenseEffectSO : DefenseIncludedPostDamageEffectSO
{
    public override async Task RunAfterCombatDamageIncludedAsync(
        DefenseIncludedPostDamageContext context,
        CardData triggeringCard,
        CancellationToken cancellationToken)
    {
        if (context?.Defender == null || triggeringCard == null) return;

        var bm = BattleManager.I;
        if (bm == null) return;

        bool defenderIsPlayer = ReferenceEquals(context.Defender, bm.GetPlayerStatus());
        Side displaySide = defenderIsPlayer ? Side.Player : Side.Enemy;

        await DefenseIncludedPresentation.RunTriggerCardElementGlowAsync(
            triggeringCard, displaySide, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        float fadeSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowStyledMessagePopup(context.Defender, MessagePopupKind.AntigravityHandRenew)
            : 0f;
        if (fadeSec > 0f)
            await MessagePopup.WaitAfterPopupLifetimeAsync(fadeSec, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var hand = defenderIsPlayer ? bm.playerHand : bm.cpuHand;
        if (hand == null || bm.HandRefill == null) return;

        PlayerType drawSide = defenderIsPlayer ? PlayerType.Player : PlayerType.Enemy;
        await bm.HandRefill.RunDefenseIncludedHandRenewalToCountAsync(
            hand,
            context.Defender,
            defenderIsPlayer,
            AntigravitySkatebladeRules.TargetHandCount,
            drawSide,
            cancellationToken);

        BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
        if (defenderIsPlayer)
            bm.RefreshPlayerDefensePhaseInteractivity();
    }
}
