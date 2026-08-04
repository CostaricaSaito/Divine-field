using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 100万ドルバズーカ：GP を全て使い、その倍数を攻撃力に加える（カード ATK は 0 想定）。
/// </summary>
[CreateAssetMenu(fileName = "MillionDollarBazookaRule", menuName = "DivineField/Special Attack/Million Dollar Bazooka Rule")]
public class MillionDollarBazookaRuleSO : SpecialAttackRuleSO
{
    [Header("GP cost to damage")]
    [Tooltip("Consumed GP is multiplied by this value (rounded) for bonus attack power.")]
    [Min(0f)]
    public float damageMultiplier = 2f;
}

/// <summary>
/// 100万ドルバズーカの判定と、コンボ内魔法 MP 消費後の GP 全消費に沿った攻撃力合算。
/// GP 全喪失後の計算は <see cref="BattleManager.TryGetMillionDollarBazookaComboGpPoolSnapshot"/> を参照する。
/// </summary>
public static class MillionDollarBazookaRules
{
    public static bool IsMillionDollarBazookaCard(CardData c) =>
        c != null && c.specialAttackRule is MillionDollarBazookaRuleSO;

    public static bool ContainsMillionDollarBazooka(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsMillionDollarBazookaCard(cards[i]))
                return true;
        }
        return false;
    }

    public static bool TryGetFirstMillionDollarBazookaRule(IReadOnlyList<CardData> cards, out MillionDollarBazookaRuleSO rule)
    {
        rule = null;
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i]?.specialAttackRule is MillionDollarBazookaRuleSO r)
            {
                rule = r;
                return true;
            }
        }
        return false;
    }

    public static CardData FindFirstMillionDollarBazookaCard(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsMillionDollarBazookaCard(cards[i]))
                return cards[i];
        }
        return null;
    }

    public static int ComputeDamageBonusFromGp(int gpConsumed, MillionDollarBazookaRuleSO rule)
    {
        if (gpConsumed <= 0 || rule == null) return 0;
        return Mathf.RoundToInt(gpConsumed * rule.damageMultiplier);
    }

    public static int SumAttackPowerExcludingMillionDollarBazooka(IReadOnlyList<CardData> cards)
    {
        int s = 0;
        if (cards == null) return 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || IsMillionDollarBazookaCard(c)) continue;
            s += c.attackPower;
        }
        return s;
    }

    /// <summary>
    /// コンボのカード攻撃力合算。魔法 MP はリスト順に先に差し引いたうえで、残 GP を全消費して倍数加算。
    /// </summary>
    public static int SumCardAttackPowerForMillionDollarBazookaCombo(IReadOnlyList<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || attacker == null) return 0;
        if (!ContainsMillionDollarBazooka(cards))
        {
            int plain = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c != null) plain += c.attackPower;
            }
            return plain + MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        }

        if (BattleManager.I != null && BattleManager.I.TryGetMillionDollarBazookaComboGpPoolSnapshot(out int snapGp))
            return SumWithBazookaBonusFromPool(cards, snapGp, attacker);

        int gpPool = attacker.currentGP;
        return SumWithBazookaBonusFromPool(cards, gpPool, attacker);
    }

    private static int SumWithBazookaBonusFromPool(
        IReadOnlyList<CardData> cards,
        int gpPoolBeforeDrain,
        PlayerStatus attacker)
    {
        int sumAtk = SumAttackPowerExcludingMillionDollarBazooka(cards);
        if (attacker != null)
            sumAtk += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        if (ContainsMillionDollarBazooka(cards)
            && TryGetFirstMillionDollarBazookaRule(cards, out var rule))
            sumAtk += ComputeDamageBonusFromGp(Mathf.Max(0, gpPoolBeforeDrain), rule);
        return sumAtk;
    }
}
