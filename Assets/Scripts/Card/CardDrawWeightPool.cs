using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CardData ごとの重みを展開した抽選プールを構築し、テンプレートを1枚選ぶ。
/// </summary>
public static class CardDrawWeightPool
{
    public const int UseRarityDefaultWeight = -1;

    public static int ResolveDrawWeight(CardData template, CardDrawTableSO table)
    {
        if (template == null) return 0;
        if (template.customDrawWeight >= 0)
            return template.customDrawWeight;
        if (table == null) return 0;
        return Mathf.Max(0, table.GetDefaultWeight(template.rarity));
    }

    /// <summary>
    /// Ultimate 除外。allCards は名前順ソート済みであること（オンライン同期用）。
    /// </summary>
    public static List<CardData> BuildExpandedTemplatePool(CardData[] allCards, CardDrawTableSO table)
    {
        var pool = new List<CardData>();
        if (allCards == null || allCards.Length == 0) return pool;

        foreach (var template in allCards)
        {
            if (template == null || template.cardType == CardType.Ultimate) continue;

            int weight = ResolveDrawWeight(template, table);
            for (int i = 0; i < weight; i++)
                pool.Add(template);
        }

        return pool;
    }

    public static CardData PickTemplate(List<CardData> expandedPool, PlayerType forSide)
    {
        if (expandedPool == null || expandedPool.Count == 0)
            return null;

        int index = BattleRandom.DrawRange(forSide, 0, expandedPool.Count);
        return expandedPool[index];
    }
}
