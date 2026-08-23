using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Diabolos Ultimate Skill: Diabolic Emission — grant next-strike Dark element buff on summoner.
/// </summary>
public static class DiabolosUltimateRules
{
    public const string DiabolicEmissionCardName = "\u95C7\u306E\u5E37\u304C\u8A2A\u308C\u308B...";
    public const string ActivationMessage = "\u95C7\u306E\u5E37\u304C\u8A2A\u308C\u308B...";

    public static readonly Color ActivationMessageColor = new Color(0.82f, 0.82f, 0.88f);

    public static bool IsDiabolicEmissionCard(CardData card)
    {
        return card != null
            && card.cardType == CardType.Ultimate
            && card.cardName == DiabolicEmissionCardName;
    }

    public static bool ApplyDiabolicEmissionBuff(PlayerStatus summoner)
    {
        if (summoner == null) return false;
        var config = StatusProgressionConfig.GetRuntimeFallback();
        var (result, _) = summoner.TryApplyStatusEffect(
            StatusEffectType.DiabolicEmission, config, suppressGrantPopupAndSound: true);
        return result == ProgressiveApplyResult.Applied;
    }

    /// <summary>
    /// True when attacker's Diabolic Emission should force Dark on this opponent-target strike.
    /// Hit must already have succeeded when applicable.
    /// </summary>
    public static bool CanApplyDarkEmission(
        PlayerStatus attacker,
        PlayerStatus defender,
        IReadOnlyList<CardData> attackCards,
        CardData primaryAttackCard)
    {
        if (attacker == null || defender == null || attackCards == null || attackCards.Count == 0)
            return false;
        if (!attacker.HasDiabolicEmissionEffect()) return false;
        if (ReferenceEquals(attacker, defender)) return false;

        if (primaryAttackCard != null && EconomicActionNames.IsEconomicAttack(primaryAttackCard.cardName))
            return false;

        if (IsExcludedArchMagicAttack(attackCards, primaryAttackCard))
            return false;

        return true;
    }

    private static bool IsExcludedArchMagicAttack(
        IReadOnlyList<CardData> attackCards,
        CardData primaryAttackCard)
    {
        if (primaryAttackCard != null && ArchMagicRules.IsArchMagicCard(primaryAttackCard))
            return true;
        return ArchMagicRules.ContainsArchMagic(attackCards);
    }
}
