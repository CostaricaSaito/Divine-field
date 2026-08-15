public static class ArrowOfIndraRules
{
    public const int MaxDestroyCount = 3;

    public static bool IsArrowOfIndraCard(CardData card)
        => card != null && card.specialCardEffect is ArrowOfIndraSpecialEffectSO;
}
