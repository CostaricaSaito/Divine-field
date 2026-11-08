using System.Collections.Generic;

/// <summary>
/// Resolves full enemy defense picks from RemotePlayerAgent.LastDefenseSelection (online multi-card).
/// </summary>
public static class EnemyDefenseSelectionHelper
{
    public static List<CardData> GetDefensePicks(EnemyAI enemyAI, CardData fallback)
    {
        if (enemyAI != null
            && enemyAI.LastDefenseSelection != null
            && enemyAI.LastDefenseSelection.Count > 0)
            return new List<CardData>(enemyAI.LastDefenseSelection);
        if (fallback != null)
            return new List<CardData> { fallback };
        return new List<CardData>();
    }

    /// <summary>
    /// 打ち払い失敗後の再防御用。消費済みパリィが <see cref="EnemyAI.LastDefenseSelection"/> に残ることがあるため fallback のみを使う。
    /// </summary>
    public static List<CardData> GetParryRerunDefensePicks(CardData selected)
    {
        if (selected == null) return new List<CardData>();
        return new List<CardData> { selected };
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
