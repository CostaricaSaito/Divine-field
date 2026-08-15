public static class MagicSealerRules
{
    public static bool IsMagicSealerCard(CardData card)
        => card != null && card.specialCardEffect is MagicSealerSpecialEffectSO;
}
