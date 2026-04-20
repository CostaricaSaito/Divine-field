using System.Collections.Generic;

/// <summary>
/// 組み合わせ魔法「ゴッドレイジ」（<c>&lt;&lt;GodRage&gt;&gt;</c>）：他カードと組み合わせたとき、
/// カード合計攻撃力を先に 2 倍してからイフリート・リヴァイアサン等を適用する。
/// </summary>
public static class GodRageRules
{
    /// <summary><see cref="CardData.cardName"/> の値（GodRage.asset と一致）。</summary>
    public const string GodRageCardNameToken = "<<GodRage>>";

    public static bool IsGodRageCard(CardData c)
    {
        return c != null && c.cardName == GodRageCardNameToken;
    }

    /// <summary>
    /// ゴッドレイジが含まれ、かつゴッドレイジ以外のカードが1枚以上ある（組み合わせ利用）とき true。
    /// 単独のみのゴッドレイジは false。
    /// </summary>
    public static bool IsGodRageDoublingCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count < 2) return false;
        bool hasGod = false;
        bool hasNonGod = false;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (IsGodRageCard(c))
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
