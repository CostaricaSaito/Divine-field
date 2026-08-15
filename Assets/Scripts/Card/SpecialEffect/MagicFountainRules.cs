public static class MagicFountainRules
{
    public const int UsesBonus = 4;

    public static bool IsMagicFountainCard(CardData card)
        => card != null && card.specialCardEffect is MagicFountainSpecialEffectSO;
}
