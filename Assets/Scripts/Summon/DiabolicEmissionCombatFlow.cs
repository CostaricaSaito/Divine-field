using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Diabolic Emission combat: dark flash after hit, consume buff, force incoming attack element to Dark.
/// </summary>
public static class DiabolicEmissionCombatFlow
{
    private const float PreFlashDelayMs = 500f;
    private const float DarkFlashMs = 50f;

    /// <summary>
    /// After a successful hit on an opponent-target strike, play dark flash and apply forced Dark element.
    /// </summary>
    public static async Task<bool> TryApplyDarkEmissionAfterHitAsync(
        BattleManager bm,
        List<CardData> attackCards,
        PlayerStatus atk,
        PlayerStatus def,
        CardData primaryAttackCard,
        CancellationToken ct,
        int dualBladeStrikeIndex = 0)
    {
        if (dualBladeStrikeIndex > 0) return false;
        if (!DiabolosUltimateRules.CanApplyDarkEmission(atk, def, attackCards, primaryAttackCard))
            return false;

        ElementType naturalElement = ElementHelper.GetCombinedElement(attackCards);
        if (naturalElement != ElementType.Dark)
        {
            try
            {
                await Task.Delay((int)PreFlashDelayMs, ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            Color darkFlashColor = ElementHelper.GetElementColor(ElementType.Dark);
            SoundEffectPlayer.I?.Play("Assets/SE/power19.wav");
            BattleUIManager.I?.PlayFullscreenColorFlashMs(darkFlashColor, DarkFlashMs);
            try
            {
                await Task.Delay((int)DarkFlashMs, ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        atk.ConsumeDiabolicEmissionEffect();
        bm?.SetIncomingAttackForceDarkElement(true);
        BattleUIManager.I?.UpdateStatus(bm?.GetPlayerStatus(), bm?.GetEnemyStatus());
        bm?.UpdateTotalATKDEFDisplay();
        return true;
    }
}
