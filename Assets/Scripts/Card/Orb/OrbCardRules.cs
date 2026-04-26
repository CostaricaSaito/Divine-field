using System.Collections.Generic;

/// <summary>宝玉系カード（<see cref="CardData.orbReactionRule"/> ）の列挙・判定。</summary>
public static class OrbCardRules
{
    public static bool IsOrbCard(CardData c) => c != null && c.orbReactionRule != null;

    /// <summary>防御リスト内で選んだ順に、宝玉のカードだけを取り出す。</summary>
    public static List<CardData> CollectOrbsInDefenseOrder(IReadOnlyList<CardData> defenseCards)
    {
        var r = new List<CardData>();
        if (defenseCards == null) return r;
        for (int i = 0; i < defenseCards.Count; i++)
        {
            var c = defenseCards[i];
            if (IsOrbCard(c)) r.Add(c);
        }
        return r;
    }
}
