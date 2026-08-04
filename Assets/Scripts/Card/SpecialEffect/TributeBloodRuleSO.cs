using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tribute Blood: pay HP in a popup; deal <see cref="damageMultiplier"/> times the paid HP as attack power.
/// </summary>
[CreateAssetMenu(fileName = "TributeBloodRule", menuName = "DivineField/Special Attack/Tribute Blood Rule")]
public class TributeBloodRuleSO : SpecialAttackRuleSO
{
    [Header("HP cost to damage")]
    [Tooltip("Paid HP is multiplied by this value (rounded) for bonus attack power.")]
    [Min(0f)]
    public float damageMultiplier = 2f;
}

/// <summary>
/// Tribute Blood detection and combo attack-power summation.
/// </summary>
public static class TributeBloodRules
{
    public static bool IsTributeBloodCard(CardData c) =>
        c != null && c.specialAttackRule is TributeBloodRuleSO;

    public static bool ContainsTributeBlood(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsTributeBloodCard(cards[i]))
                return true;
        }
        return false;
    }

    public static bool TryGetFirstTributeBloodRule(IReadOnlyList<CardData> cards, out TributeBloodRuleSO rule)
    {
        rule = null;
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i]?.specialAttackRule is TributeBloodRuleSO r)
            {
                rule = r;
                return true;
            }
        }
        return false;
    }

    public static CardData FindFirstTributeBloodCard(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsTributeBloodCard(cards[i]))
                return cards[i];
        }
        return null;
    }

    public static int ComputeDamageBonusFromHpPaid(int hpPaid, TributeBloodRuleSO rule)
    {
        if (hpPaid <= 0 || rule == null) return 0;
        return Mathf.RoundToInt(hpPaid * rule.damageMultiplier);
    }

    public static int GetActiveHpPaid(IReadOnlyList<CardData> attackCards, PlayerStatus attackingPlayer)
    {
        if (attackCards == null || attackingPlayer == null || !ContainsTributeBlood(attackCards))
            return 0;

        var bm = BattleManager.I;
        if (bm != null && bm.TryGetTributeBloodHpPaidSnapshot(attackingPlayer, out int hpPaid))
            return hpPaid;

        return 0;
    }

    /// <summary>Sum attack power excluding Tribute Blood cards.</summary>
    public static int SumAttackPowerExcludingTributeBlood(IReadOnlyList<CardData> cards)
    {
        int s = 0;
        if (cards == null) return 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || IsTributeBloodCard(c)) continue;
            s += c.attackPower;
        }
        return s;
    }

    /// <summary>Combo card attack sum including paid-HP damage bonus and Magical Sword optional boost.</summary>
    public static int SumCardAttackPowerForTributeBloodCombo(IReadOnlyList<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || attacker == null) return 0;
        if (!ContainsTributeBlood(cards))
        {
            int plain = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c != null) plain += c.attackPower;
            }
            return plain + MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        }

        int sumAtk = SumAttackPowerExcludingTributeBlood(cards);
        sumAtk += MagicalSwordRules.GetActivePowerBonus(cards, attacker);

        int hpPaid = GetActiveHpPaid(cards, attacker);
        if (hpPaid > 0 && TryGetFirstTributeBloodRule(cards, out var rule))
            sumAtk += ComputeDamageBonusFromHpPaid(hpPaid, rule);

        return sumAtk;
    }
}
