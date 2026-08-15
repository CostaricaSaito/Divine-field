using System.Collections.Generic;

public static class ShiningBarrierRules
{
    public static bool IsShiningBarrierCard(CardData card)
        => card != null && card.specialCardEffect is ShiningBarrierSpecialEffectSO;

    /// <summary>Standalone Shining Barrier selection only.</summary>
    public static bool IsBarrierOnlySelection(IReadOnlyList<CardData> selected)
        => selected != null && selected.Count == 1 && IsShiningBarrierCard(selected[0]);

    /// <summary>Attack-type incoming only (not immediate/recovery/economic).</summary>
    public static bool CanUseAgainstIncoming(IReadOnlyList<CardData> incoming)
    {
        if (incoming == null || incoming.Count == 0) return false;
        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(incoming)) return false;
        if (incoming.Count == 1 && incoming[0] != null
            && EconomicActionNames.IsEconomicAttack(incoming[0].cardName))
            return false;
        return true;
    }
}
