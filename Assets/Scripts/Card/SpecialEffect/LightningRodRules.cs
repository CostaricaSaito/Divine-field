using System.Collections.Generic;

/// <summary>
/// Lightning Rod (避雷針): normal armor, except a thunder-element attack is nullified
/// (BLOCKING) whether that attack is physical or magic.
/// </summary>
public static class LightningRodRules
{
    public static bool IsLightningRodCard(CardData card) =>
        card != null && card.specialCardEffect is LightningRodDefenseEffectSO;

    /// <summary>
    /// Incoming combined element is Thunder. Grand magic and full-only reactive attacks stay on their existing paths.
    /// </summary>
    public static bool IsThunderAttack(IReadOnlyList<CardData> incoming)
    {
        if (incoming == null || incoming.Count == 0) return false;
        if (GrandMagicRules.ContainsGrandMagicStyleAttack(incoming)) return false;
        if (CardRules.IncomingRequiresFullOnlyReactiveDefense(incoming)) return false;
        return ElementHelper.GetIncomingAttackElement(incoming) == ElementType.Thunder;
    }

    public static bool CanNullifyIncoming(CardData defense, IReadOnlyList<CardData> incoming) =>
        IsLightningRodCard(defense) && IsThunderAttack(incoming);
}
