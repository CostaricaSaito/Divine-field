using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ランタイム <see cref="CardData"/> インスタンスは <see cref="ScriptableObject.Instantiate"/> により参照が分かれるため、
/// 「同じカードの定義」一致に用いる比較。
/// </summary>
public static class CardDefinitionIdentity
{
    public static bool IsSameDefinition(CardData a, CardData b)
    {
        if (a == null || b == null) return false;
        if (a.cardType != b.cardType) return false;
        if (a.element != b.element) return false;
        return a.cardName == b.cardName;
    }

    /// <summary>手札内で同一定義の枚数。</summary>
    public static int CountSameInHand(CardData reference, IReadOnlyList<CardData> hand)
    {
        if (reference == null || hand == null) return 0;
        int n = 0;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c != null && IsSameDefinition(reference, c))
                n++;
        }
        return n;
    }

    /// <summary>手札リロード用：同一定義のうち <see cref="CardRules.CanParticipateInHandReload"/> な枚数。</summary>
    public static int CountSameInHandForReload(CardData reference, IReadOnlyList<CardData> hand)
    {
        if (reference == null || hand == null) return 0;
        if (!CardRules.CanParticipateInHandReload(reference)) return 0;
        int n = 0;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c != null && CardRules.CanParticipateInHandReload(c) && IsSameDefinition(reference, c))
                n++;
        }
        return n;
    }

    /// <summary>
    /// いずれかの定義について3枚以上ある（リロードの入口条件）。氷結（FROZEN）は数えない。
    /// </summary>
    public static bool HandHasAnyTripletOrMore(IReadOnlyList<CardData> hand)
    {
        if (hand == null) return false;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c == null || !CardRules.CanParticipateInHandReload(c)) continue;
            if (CountSameInHandForReload(c, hand) >= 3)
                return true;
        }
        return false;
    }
}
