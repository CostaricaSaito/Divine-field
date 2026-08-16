using UnityEngine;

/// <summary>
/// Odin Ultimate Skill: Zantestuken — grant unblockable-next-strike buff on summoner.
/// </summary>
public static class OrdinUltimateRules
{
    public const string ZantestukenCardName = "\u65AC\u9244\u5263";
    public const string ActivationMessage = "\u6B21\u306E\u653B\u6483\u306F\u5168\u3066\u3092\u65AD\u3064";
    public const string UnblockableMessage = "\u3053\u306E\u653B\u6483\u306F\u9632\u3052\u306A\u3044\uFF01";

    public static readonly Color ActivationMessageColor = new Color(0.82f, 0.82f, 0.88f);
    public static readonly Color UnblockableMessageColor = new Color(0.82f, 0.82f, 0.88f);

    public static bool IsZantestukenCard(CardData card)
    {
        return card != null
            && card.cardType == CardType.Ultimate
            && card.cardName == ZantestukenCardName;
    }

    public static bool ApplyZantestukenBuff(PlayerStatus summoner)
    {
        if (summoner == null) return false;
        var config = StatusProgressionConfig.GetRuntimeFallback();
        var (result, _) = summoner.TryApplyStatusEffect(
            StatusEffectType.Zantestuken, config, suppressGrantPopupAndSound: true);
        return result == ProgressiveApplyResult.Applied;
    }

    public static bool CanConsumeForOpponentStrike(
        PlayerStatus attacker,
        PlayerStatus defender,
        CardData currentAttackCard)
    {
        if (attacker == null || defender == null) return false;
        if (!attacker.HasZantestukenEffect()) return false;
        if (ReferenceEquals(attacker, defender)) return false;
        if (currentAttackCard != null && EconomicActionNames.IsEconomicAttack(currentAttackCard.cardName))
            return false;
        return true;
    }
}
