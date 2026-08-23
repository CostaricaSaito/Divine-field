using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arcadias Ultimate Skill: Millenium Kingdom — persistent magic/attribute attack nullify on summoner.
/// </summary>
public static class ArcadiasUltimateRules
{
    public const string MilleniumKingdomCardName = "\u5343\u5E74\u738B\u56FD";
    public const string LightProtectionMessage = "\u5149\u306E\u52A0\u8B77";

    public static readonly Color LightProtectionMessageColor = new Color(1f, 0.95f, 0.55f);

    public static bool IsMilleniumKingdomCard(CardData card)
    {
        return card != null
            && card.cardType == CardType.Ultimate
            && card.cardName == MilleniumKingdomCardName;
    }

    public static bool ApplyMilleniumKingdomBuff(PlayerStatus summoner)
    {
        if (summoner == null) return false;
        var config = StatusProgressionConfig.GetRuntimeFallback();
        var (result, _) = summoner.TryApplyStatusEffect(
            StatusEffectType.MilleniumKingdom, config, suppressGrantPopupAndSound: true);
        return result == ProgressiveApplyResult.Applied;
    }

    /// <summary>
    /// True when defender's Millenium Kingdom should auto-nullify incoming opponent attack.
    /// Hit check must already have succeeded when applicable.
    /// </summary>
    public static bool ShouldNullifyIncoming(
        PlayerStatus attacker,
        PlayerStatus defender,
        IReadOnlyList<CardData> attackCards,
        CardData primaryAttackCard)
    {
        if (attacker == null || defender == null || attackCards == null || attackCards.Count == 0)
            return false;
        if (!defender.HasMilleniumKingdomEffect()) return false;
        if (ReferenceEquals(attacker, defender)) return false;

        if (primaryAttackCard != null && EconomicActionNames.IsEconomicAttack(primaryAttackCard.cardName))
            return false;

        if (IsExcludedArchMagicAttack(attackCards, primaryAttackCard))
            return false;

        if (ContainsExcludedSpecialCard(attackCards))
            return false;

        if (IsMagicNullifyTarget(attackCards))
            return true;

        return ElementHelper.GetIncomingAttackElement(attackCards) != ElementType.None;
    }

    private static bool IsExcludedArchMagicAttack(
        IReadOnlyList<CardData> attackCards,
        CardData primaryAttackCard)
    {
        if (primaryAttackCard != null && ArchMagicRules.IsArchMagicCard(primaryAttackCard))
            return true;
        return ArchMagicRules.ContainsArchMagic(attackCards);
    }

    private static bool ContainsExcludedSpecialCard(IReadOnlyList<CardData> attackCards)
    {
        for (int i = 0; i < attackCards.Count; i++)
        {
            if (attackCards[i] != null && attackCards[i].cardType == CardType.Special)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Magic-only / magic-classified combos, or single magic (incl. immediate status magic).
    /// Mixed Attack+Magic with final None element is NOT magic-nullify (physical combo).
    /// </summary>
    private static bool IsMagicNullifyTarget(IReadOnlyList<CardData> attackCards)
    {
        if (CardRules.IsMagicClassifiedAttackCombo(attackCards))
            return true;

        if (attackCards.Count == 1)
        {
            var sole = attackCards[0];
            if (sole != null && sole.cardType == CardType.Magic && !CardRules.IsRecoveryCard(sole))
                return true;
        }

        return false;
    }
}
