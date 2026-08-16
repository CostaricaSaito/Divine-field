using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Indra Ultimate Skill: Judgement Thunder — destroy ceil(opponent hand / 2) random cards.
/// </summary>
public static class IndraUltimateRules
{
    public const string JudgementThunderCardName = "\u88C1\u304D\u306E\u96F7";

    public static bool IsJudgementThunderCard(CardData card)
    {
        return card != null
            && card.cardType == CardType.Ultimate
            && card.cardName == JudgementThunderCardName;
    }

    /// <summary>ceil(handCount / 2); 0 when hand is empty.</summary>
    public static int ComputeDestroyCount(int handCount)
    {
        if (handCount <= 0) return 0;
        return (handCount + 1) / 2;
    }

    public static List<HandDestroyRules.Pick> BuildDestroyPicks(List<CardData> hand, PlayerType handOwner)
    {
        int count = ComputeDestroyCount(hand?.Count ?? 0);
        if (count <= 0) return new List<HandDestroyRules.Pick>();
        return HandDestroyRules.PickRandomDestroyableCards(hand, handOwner, count);
    }
}
