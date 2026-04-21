using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// マジカルエクスプロージョンの判定と、MP 消費順に沿った攻撃力合算。
/// 先に他カードの魔法消費 MP を差し引き、残りを全て ME が吸い取り 2 倍の攻撃力となる。
/// MP 全喪失後の計算は <see cref="BattleManager"/> のスナップショットを参照する。
/// </summary>
public static class MagicalExplosionRules
{
    public static bool IsMagicalExplosionCard(CardData c)
    {
        return c != null && c.specialAttackRule is MagicalExplosionRuleSO;
    }

    public static bool ContainsMagicalExplosion(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsMagicalExplosionCard(cards[i]))
                return true;
        }
        return false;
    }

    /// <summary>ME 以外のカードの attackPower 合計。</summary>
    public static int SumAttackPowerExcludingMagicalExplosion(IReadOnlyList<CardData> cards)
    {
        int s = 0;
        if (cards == null) return 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || IsMagicalExplosionCard(c)) continue;
            s += c.attackPower;
        }
        return s;
    }

    /// <summary>
    /// ME を含むコンボのカード攻撃力合算（イフリート等の前段）。
    /// 魔法の MP はリスト順に先に差し引いた残りを ME が 2 倍する。
    /// </summary>
    public static int SumCardAttackPowerForMagicalExplosionCombo(IReadOnlyList<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null || attacker == null) return 0;
        if (!ContainsMagicalExplosion(cards))
        {
            int plain = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c != null) plain += c.attackPower;
            }
            return plain;
        }

        if (BattleManager.I != null && BattleManager.I.TryGetMagicalExplosionComboMpPoolSnapshot(out int snapMp))
            return SumWithMeBonusFromPool(cards, snapMp);

        int mpPool = attacker.currentMP;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || IsMagicalExplosionCard(c)) continue;
            if (c.cardType == CardType.Magic && c.mpCost > 0)
                mpPool -= attacker.GetEffectiveMagicMpCost(c.mpCost);
        }
        mpPool = Mathf.Max(0, mpPool);
        return SumWithMeBonusFromPool(cards, mpPool);
    }

    private static int SumWithMeBonusFromPool(IReadOnlyList<CardData> cards, int mpPoolAfterOtherCosts)
    {
        int sumAtk = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || IsMagicalExplosionCard(c)) continue;
            sumAtk += c.attackPower;
        }
        if (ContainsMagicalExplosion(cards))
            sumAtk += mpPoolAfterOtherCosts * 2;
        return sumAtk;
    }
}
