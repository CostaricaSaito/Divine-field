using System.Collections.Generic;
using UnityEngine;

/// <summary>経済アクション用ダミー攻撃カード名（CPU / オンライン共通）。</summary>
public static class EconomicActionNames
{
    public const string Buy = "経済アクション";
    public const string Sell = "経済アクション（売却）";

    public static bool IsEconomicAttack(string cardName)
    {
        return cardName == Buy || cardName == Sell;
    }

    public static CardData CreateBuyDummy()
    {
        var dummy = new CardData();
        dummy.cardName = Buy;
        dummy.cardType = CardType.Attack;
        return dummy;
    }

    public static CardData CreateSellDummy()
    {
        var dummy = ScriptableObject.CreateInstance<CardData>();
        dummy.cardName = Sell;
        dummy.cardType = CardType.Attack;
        return dummy;
    }

    public static CardData FindFirstByName(IReadOnlyList<CardData> hand, string cardName)
    {
        if (hand == null || string.IsNullOrEmpty(cardName)) return null;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c != null && c.cardName == cardName)
                return c;
        }
        return null;
    }
}
