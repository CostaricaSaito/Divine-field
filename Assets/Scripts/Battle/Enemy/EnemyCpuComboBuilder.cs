using System.Collections.Generic;

/// <summary>Builds CPU attack combos without mixing elements into unintended neutral.</summary>
public static class EnemyCpuComboBuilder
{
    public static bool ComboPreservesElementIntent(IReadOnlyList<CardData> combo)
    {
        if (combo == null || combo.Count == 0) return true;

        bool anyNonNone = false;
        for (int i = 0; i < combo.Count; i++)
        {
            var c = combo[i];
            if (c != null && c.element != ElementType.None)
            {
                anyNonNone = true;
                break;
            }
        }

        if (!anyNonNone) return true;
        return ElementHelper.GetCombinedElement(combo) != ElementType.None;
    }

    public static List<CardData> BuildGreedy(
        CardData primary,
        List<CardData> handCandidates,
        List<CardData> enemyHand,
        List<CardData> poolCandidates,
        PlayerStatus attackerStatus,
        bool forbidNeutralMix)
    {
        if (primary == null) return new List<CardData>();

        var hand = handCandidates ?? new List<CardData>();
        var pool = poolCandidates ?? new List<CardData>();

        var seed = new List<CardData> { primary };
        FreeMagicCastRules.AppendSelectableFreeCastCards(hand, enemyHand ?? hand, seed);

        var combo = AttackComboSelectionRules.BuildGreedyAttackCombo(
            hand,
            pool,
            primary,
            attackerStatus,
            PlayerType.Enemy);

        if (!forbidNeutralMix || ComboPreservesElementIntent(combo))
            return combo;

        return new List<CardData> { primary };
    }

    public static List<CardData> FilterHandForAttack(
        List<CardData> enemyHand,
        PlayerStatus enemyStatus,
        PlayerStatus playerStatus,
        HandRefillService handRefill)
    {
        var handCandidates = CardRules.GetAttackChoices(enemyHand, PlayerType.Enemy);
        handCandidates.RemoveAll(c =>
            c == null || ArchMagicRules.IsArchMagicCard(c) || !EnemyAI.IsEnemyHandCardAvailable(c, handRefill));
        handCandidates.RemoveAll(c =>
        {
            if (c == null || c.cardType != CardType.Magic) return false;
            if (CardRules.IsRecoveryCard(c))
                return !EnemyMagicAiRules.IsEnemySelfRecoveryMagicUseful(c, enemyStatus);
            return !EnemyMagicAiRules.IsEnemyMagicUsefulAgainstOpponent(c, enemyStatus, playerStatus, enemyHand);
        });
        return handCandidates;
    }

    public static List<CardData> GetPoolCandidates(
        PlayerStatus enemyStatus,
        PlayerStatus playerStatus,
        List<CardData> enemyHand)
    {
        return EnemyMagicAiRules.CollectUsefulPoolMagic(enemyStatus, playerStatus, enemyHand);
    }
}
