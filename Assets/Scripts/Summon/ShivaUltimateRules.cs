using UnityEngine;

/// <summary>
/// Shiva Ultimate Skill: Glacial Diamond — grant Infinite Manasource on summoner.
/// </summary>
public static class ShivaUltimateRules
{
    public const string GlacialDiamondCardName = "\u30B0\u30EC\u30A4\u30B7\u30E3\u30EB\u30C0\u30A4\u30A2\u30E2\u30F3\u30C9";
    public const string ActivationMessage = "\u9B54\u529B\u304C\u6C38\u9060\u306B\u51CD\u308A\u4ED8\u304F...";

    public static readonly Color ActivationMessageColor = new Color(0.55f, 0.85f, 1f);

    public static bool IsGlacialDiamondCard(CardData card)
    {
        if (card == null || card.cardType != CardType.Ultimate) return false;
        if (card.cardName == GlacialDiamondCardName) return true;
        return card.name == "GlacialDiamond" || card.name == "DiamondDust";
    }

    public static bool ApplyInfiniteManasourceBuff(PlayerStatus summoner)
        => InfiniteManasourceRules.TryGrant(summoner);
}
