using UnityEngine;

/// <summary>
/// Sealed EXO-DION (封印されし装甲神): alt-win token on magic panel right slot.
/// </summary>
public static class SealedExoDionRules
{
    public const string TemplateAssetName = "Sealed EXO-DION";
    public const string DisplayCardName = "封印されし装甲神";
    public const int RightSlotIndex = 2;
    public const int WinStackCount = 3;

    public static bool IsSealedExoDion(CardData card)
    {
        if (card == null) return false;
        if (card.name == TemplateAssetName) return true;
        return card.cardName == DisplayCardName;
    }

    /// <summary>Hand instance may be Shiva FROZEN facade.</summary>
    public static bool IsSealedExoDionHandInstance(CardData handInstance, CardDealer dealer)
    {
        if (handInstance == null) return false;
        if (IsSealedExoDion(handInstance)) return true;
        if (!IceBarrierTransformRegistry.TryGetOriginalTemplateAssetName(handInstance, out var assetName))
            return false;
        return assetName == TemplateAssetName;
    }

    public static CardData ResolvePoolTemplate(CardDealer dealer)
    {
        if (dealer == null) return null;
        return dealer.FindTemplateByDisplayOrAssetName(DisplayCardName)
               ?? dealer.FindTemplateByDisplayOrAssetName(TemplateAssetName);
    }
}
