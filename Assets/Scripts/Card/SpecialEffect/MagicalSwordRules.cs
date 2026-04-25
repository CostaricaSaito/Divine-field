using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// マジカルソード：任意 MP 払い・攻撃上乗せの判断と、表示／計算用の上乗せ値の取得。
/// </summary>
public static class MagicalSwordRules
{
    public static bool IsMagicalSwordCard(CardData c) =>
        c != null && c.specialAttackRule is MagicalSwordRuleSO;

    public static bool ContainsMagicalSword(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsMagicalSwordCard(cards[i]))
                return true;
        }
        return false;
    }

    public static bool TryGetFirstMagicalSwordRule(IReadOnlyList<CardData> cards, out MagicalSwordRuleSO rule)
    {
        rule = null;
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (TryGetRule(cards[i], out var r))
            {
                rule = r;
                return true;
            }
        }
        return false;
    }

    public static bool TryGetRule(CardData c, out MagicalSwordRuleSO rule)
    {
        if (c?.specialAttackRule is MagicalSwordRuleSO r)
        {
            rule = r;
            return true;
        }

        rule = null;
        return false;
    }

    /// <summary>
    /// コンボ内の、マジカルソード上乗せ分を除き、他の魔法の MP 消費分だけ先に引いた直後の残り MP
    /// （<see cref="MagicalExplosionRules"/> の「他の魔法分を先に引く」と同顺序）。
    /// </summary>
    public static int ComputeMpRemainingAfterOtherComboMagicOnly(IReadOnlyList<CardData> cards, PlayerStatus attacker)
    {
        if (attacker == null) return 0;
        int pool = attacker.currentMP;
        if (cards == null) return pool;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (c.cardType != CardType.Magic || c.mpCost <= 0) continue;
            int pay = attacker.GetEffectiveMagicMpCost(c.mpCost);
            pool -= pay;
        }
        return Mathf.Max(0, pool);
    }

    /// <summary>
    /// n を払って上乗せ可能か（> n ならポップアップを出す。n 以下は不可）。
    /// </summary>
    public static bool CanAffordOptionalMagicalSwordAfterOtherComboMagic(
        IReadOnlyList<CardData> cards,
        PlayerStatus attacker,
        int optionalMpN)
    {
        if (optionalMpN < 0) return false;
        if (attacker == null) return false;
        return ComputeMpRemainingAfterOtherComboMagicOnly(cards, attacker) > optionalMpN;
    }

    public static CardData FindFirstMagicalSwordCard(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsMagicalSwordCard(cards[i]))
                return cards[i];
        }
        return null;
    }

    public static int GetActivePowerBonus(IReadOnlyList<CardData> attackCards, PlayerStatus attackingPlayer)
    {
        if (attackCards == null || attackingPlayer == null || !ContainsMagicalSword(attackCards)) return 0;
        if (BattleManager.I == null) return 0;
        if (!ReferenceEquals(attackingPlayer, BattleManager.I.GetPlayerStatus())) return 0;
        return BattleManager.I.MagicalSwordAttackPowerBonus;
    }
}
