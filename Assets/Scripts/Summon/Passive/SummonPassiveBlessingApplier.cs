using System.Collections.Generic;

/// <summary>
/// <see cref="SummonData"/> に紐づく加護を、攻撃力合計に適用する（攻撃側・防御側）。
/// </summary>
public static class SummonPassiveBlessingApplier
{
    /// <summary>
    /// 攻撃者の召喚データに加護があれば、カード合計攻撃力に反映する。
    /// Ultimate Skill 攻撃は加護対象外。
    /// </summary>
    public static int ApplyAttackPowerBonus(PlayerStatus attacker, List<CardData> attackCards, int sumOfCardAttackPower)
    {
        if (attacker == null || attackCards == null || attackCards.Count == 0)
            return sumOfCardAttackPower;

        if (CardRules.ContainsUltimateSkillCard(attackCards))
            return sumOfCardAttackPower;

        if (attacker.HasCurseBindEffect())
            return sumOfCardAttackPower;

        var data = attacker.summonData;
        if (data == null) return sumOfCardAttackPower;

        var blessing = data.GetEffectivePassiveBlessing();
        if (blessing == null) return sumOfCardAttackPower;

        ElementType combined = ElementHelper.GetCombinedElement(attackCards);
        return blessing.ApplyToTotalAttackPower(sumOfCardAttackPower, combined, attacker);
    }

    /// <summary>
    /// 防御者の召喚データに、相手の合計攻撃力を抑える加護があれば反映する（命中前・ATK-DEF より前）。
    /// Ultimate Skill 攻撃は加護対象外。
    /// </summary>
    public static int ApplyDefenderOpponentAttackSuppression(
        PlayerStatus attacker,
        PlayerStatus defender,
        List<CardData> attackCards,
        int attackPowerAfterAttackerSideModifiers)
    {
        if (defender == null || attackCards == null || attackCards.Count == 0)
            return attackPowerAfterAttackerSideModifiers;

        if (CardRules.ContainsUltimateSkillCard(attackCards))
            return attackPowerAfterAttackerSideModifiers;

        if (defender.HasCurseBindEffect())
            return attackPowerAfterAttackerSideModifiers;

        var data = defender.summonData;
        if (data == null) return attackPowerAfterAttackerSideModifiers;

        var blessing = data.GetEffectivePassiveBlessing();
        if (blessing == null) return attackPowerAfterAttackerSideModifiers;

        ElementType combined = ElementHelper.GetCombinedElement(attackCards);
        return blessing.ApplyOpponentAttackPowerSuppression(
            attackPowerAfterAttackerSideModifiers,
            combined,
            attacker,
            defender);
    }
}
