using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Runs all defense-included post-damage effects in defense combo order.
/// </summary>
public static class DefenseIncludedPostDamageFlow
{
    private static readonly List<(CardData card, DefenseIncludedPostDamageEffectSO effect)> Scratch =
        new List<(CardData, DefenseIncludedPostDamageEffectSO)>(4);

    public static async Task RunAllAfterCombatDamageAsync(
        BattleProcessor battleProcessor,
        IReadOnlyList<CardData> defenseCards,
        PlayerStatus attacker,
        PlayerStatus defender,
        CancellationToken cancellationToken = default)
    {
        if (battleProcessor == null || defender == null || defenseCards == null)
            return;

        DefenseIncludedPostDamageRules.CollectTriggersInDefenseOrder(defenseCards, Scratch);
        if (Scratch.Count == 0) return;

        var context = new DefenseIncludedPostDamageContext
        {
            BattleProcessor = battleProcessor,
            Attacker = attacker,
            Defender = defender,
            DefenseCards = defenseCards,
        };

        for (int i = 0; i < Scratch.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (card, effect) = Scratch[i];
            if (effect == null) continue;
            await effect.RunAfterCombatDamageIncludedAsync(context, card, cancellationToken);
        }
    }
}
