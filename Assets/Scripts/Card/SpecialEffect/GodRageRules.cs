using System.Collections.Generic;

/// <summary>
/// 組み合わせ魔法「ゴッドレイジ」：<see cref="GodrageRuleSO"/> を参照するカードが他カードと組み合わせたとき、
/// カード合計攻撃力を先に 2 倍してからイフリート・リヴァイアサン等を適用する。
/// </summary>
public static class GodrageRules
{
    public static bool IsGodrageCard(CardData c)
    {
        return c != null && c.specialAttackRule is GodrageRuleSO;
    }

    /// <summary>
    /// ゴッドレイジが含まれ、かつゴッドレイジ以外のカードが1枚以上ある（組み合わせ利用）とき true。
    /// 単独のみのゴッドレイジは false。
    /// </summary>
    public static bool IsGodrageDoublingCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count < 2) return false;
        bool hasGod = false;
        bool hasNonGod = false;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (IsGodrageCard(c))
                hasGod = true;
            else
                hasNonGod = true;
        }
        return hasGod && hasNonGod;
    }

    /// <summary>カードの attackPower 合計（ゴッドレイジは 0）。</summary>
    public static int SumCardAttackPower(IReadOnlyList<CardData> cards)
    {
        int s = 0;
        if (cards == null) return 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c != null)
                s += c.attackPower;
        }
        return s;
    }
}
