/// <summary>Jett Booster: draw 1, discard 1 after defense-included damage.</summary>
public static class JettBoosterRules
{
    public const int DrawCount = 1;
    public const int DiscardCount = 1;

    public static bool IsJettBoosterCard(CardData card) =>
        card != null && card.specialCardEffect is JettBoosterDefenseEffectSO;

    public static HandDiscardSelectionRequest CreateDiscardRequest()
    {
        return new HandDiscardSelectionRequest
        {
            MinSelectCount = DiscardCount,
            MaxSelectCount = DiscardCount,
            AllowCancel = false,
            Title = "DISCARD",
            Message = "\u6368\u3066\u308b\u624b\u672d\u3092\u9078\u3079",
            SubMessage = "\u30c7\u30a3\u30b9\u30ab\u30fc\u30c9",
        };
    }
}
