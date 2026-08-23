using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Ragnarok: when played as Primary and qualifying damage passes, triggers a random disaster.
/// </summary>
[CreateAssetMenu(fileName = "RagnarokRule", menuName = "DivineField/Special Attack/Ragnarok Rule")]
public sealed class RagnarokRuleSO : SpecialAttackRuleSO
{
}

/// <summary>Ragnarok attack detection.</summary>
public static class RagnarokRules
{
    public static bool IsRagnarokCard(CardData c) =>
        c != null && c.specialAttackRule is RagnarokRuleSO;

    public static bool ContainsRagnarok(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsRagnarokCard(cards[i]))
                return true;
        }
        return false;
    }

    public static CardData FindRagnarokCard(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (IsRagnarokCard(c))
                return c;
        }
        return null;
    }
}

/// <summary>Post-damage disaster sequence for Ragnarok attacks.</summary>
public static class RagnarokDisasterFlow
{
    public static async Task TryRunAfterCombatDamageAsync(
        BattleProcessor battleProcessor,
        IReadOnlyList<CardData> attackCards,
        int firstPhaseDamage,
        PlayerStatus attacker,
        PlayerStatus defender,
        bool allowTrigger)
    {
        if (!allowTrigger || firstPhaseDamage <= 0) return;
        if (!RagnarokRules.ContainsRagnarok(attackCards)) return;

        var bm = BattleManager.I;
        if (bm == null || battleProcessor == null) return;
        if (bm.IsGameEndTriggered) return;
        if (bm.IsRagnarokDisasterTriggeredThisAttack) return;

        if ((attacker != null && attacker.currentHP <= 0)
            || (defender != null && defender.currentHP <= 0))
        {
            return;
        }

        CardData ragnarokCard = RagnarokRules.FindRagnarokCard(attackCards);
        if (ragnarokCard == null) return;

        PlayerStatus triggerOwner = bm.AttackerPublic == PlayerType.Player
            ? bm.GetPlayerStatus()
            : bm.GetEnemyStatus();
        if (triggerOwner == null) return;

        bm.MarkRagnarokDisasterTriggeredThisAttack();

        Debug.Log("[RagnarokDisasterFlow] Triggering disaster after qualifying damage");
        await DisasterOrchestrator.RunFromSpecialCardAsync(
            ragnarokCard,
            triggerOwner,
            battleProcessor,
            CancellationToken.None);
    }
}
