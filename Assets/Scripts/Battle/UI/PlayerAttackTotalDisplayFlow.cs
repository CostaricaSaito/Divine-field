using System.Collections.Generic;

/// <summary>
/// TOTAL ATK 演出フローの窓口（実体は <see cref="TotalAtkDefDisplayState"/> / <see cref="CardStatsDisplay"/>）。
/// </summary>
public static class PlayerAttackTotalDisplayFlow
{
    public static void ResetAttackSequenceDisplayLocks(CardStatsDisplay d)
    {
        d?.ClearAllAttackSequenceDisplayLocks();
    }

    public static void EnterSequentialCardReveal_SuppressPendingModifierRamps(
        CardStatsDisplay d,
        List<CardData> selectedCards,
        int magicalSwordOptionalPowerBonusIfPaid)
    {
        d?.BeginSequentialCardRevealModifierSuppressions(selectedCards, magicalSwordOptionalPowerBonusIfPaid);
    }
}
