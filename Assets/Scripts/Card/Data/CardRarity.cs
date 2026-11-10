/// <summary>手札抽選・演出用のカードレア度。</summary>
public enum CardRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    SuperRare = 3,
    UltraRare = 4,
}

public static class CardRarityExtensions
{
    /// <summary>裏面虹・レアSE 等、従来 isRare 相当の演出対象か。</summary>
    public static bool HasPremiumHandPresentation(this CardData card) =>
        card != null && card.rarity >= CardRarity.SuperRare;
}
