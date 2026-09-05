using System.Collections.Generic;

/// <summary>
/// Resolves full enemy defense picks from RemotePlayerAgent.LastDefenseSelection (online multi-card).
/// </summary>
public static class EnemyDefenseSelectionHelper
{
    public static List<CardData> GetDefensePicks(EnemyAI enemyAI, CardData fallback)
    {
        if (enemyAI is RemotePlayerAgent remote
            && remote.LastDefenseSelection != null
            && remote.LastDefenseSelection.Count > 0)
            return new List<CardData>(remote.LastDefenseSelection);
        if (fallback != null)
            return new List<CardData> { fallback };
        return new List<CardData>();
    }

    public static void ConsumeEnemyDefenseCards(
        List<CardData> cards,
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill)
    {
        if (cards == null || cards.Count == 0 || battleManager == null) return;

        bool skipOnlineMagic = battleManager.IsOnlineMatch;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            if (skipOnlineMagic && card.cardType == CardType.Magic) continue;
            handRefill?.RecordEnemyUse(card);
            battleProcessor?.UseCard(card, battleManager.cpuHand);
        }
    }
}
