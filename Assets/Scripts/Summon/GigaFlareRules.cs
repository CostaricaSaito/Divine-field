using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bahamut Giga Flare: attack power = current battle turn display * 2.
/// </summary>
public static class GigaFlareRules
{
    public static bool IsGigaFlareCard(CardData c)
    {
        if (c == null) return false;
        return c.name == "GigaFlare" || c.cardName == "\u30AE\u30AC\u30D5\u30EC\u30A2";
    }

    public static bool ContainsGigaFlare(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count != 1) return false;
        return IsGigaFlareCard(cards[0]);
    }

    public static int ComputeTurnScaledAttackPower(SummonTurnCounterState counters)
    {
        int turn = counters != null ? counters.CurrentBattleTurnDisplay : 1;
        return Mathf.Max(0, turn * 2);
    }

    public static int GetActivePowerBonus(IReadOnlyList<CardData> attackCards, PlayerStatus attackingPlayer)
    {
        if (attackCards == null || attackingPlayer == null || !ContainsGigaFlare(attackCards))
            return 0;
        if (BattleManager.I == null) return 0;
        if (ReferenceEquals(attackingPlayer, BattleManager.I.GetPlayerStatus()))
            return BattleManager.I.GigaFlareAttackPowerBonus;
        if (BattleManager.I.IsOnlineMatch
            && ReferenceEquals(attackingPlayer, BattleManager.I.GetEnemyStatus()))
            return BattleManager.I.GigaFlareEnemyAttackPowerBonus;
        return 0;
    }
}
