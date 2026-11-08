using System.Collections.Generic;

/// <summary>
/// Detects defense cards whose <see cref="CardData.specialCardEffect"/> is a
/// <see cref="DefenseIncludedPostDamageEffectSO"/>.
/// </summary>
public static class DefenseIncludedPostDamageRules
{
    public static bool ContainsPostDamageEffect(IReadOnlyList<CardData> defenseCards)
        => TryGetFirstTriggeringCard(defenseCards, out _, out _);

    public static bool ContainsEffect<TEffect>(IReadOnlyList<CardData> defenseCards)
        where TEffect : DefenseIncludedPostDamageEffectSO
    {
        if (defenseCards == null) return false;
        for (int i = 0; i < defenseCards.Count; i++)
        {
            if (defenseCards[i]?.specialCardEffect is TEffect)
                return true;
        }

        return false;
    }

    public static bool TryGetFirstTriggeringCard(
        IReadOnlyList<CardData> defenseCards,
        out CardData card,
        out DefenseIncludedPostDamageEffectSO effect)
    {
        card = null;
        effect = null;
        if (defenseCards == null) return false;

        for (int i = 0; i < defenseCards.Count; i++)
        {
            var c = defenseCards[i];
            if (c?.specialCardEffect is not DefenseIncludedPostDamageEffectSO so) continue;
            card = c;
            effect = so;
            return true;
        }

        return false;
    }

    public static void CollectTriggersInDefenseOrder(
        IReadOnlyList<CardData> defenseCards,
        List<(CardData card, DefenseIncludedPostDamageEffectSO effect)> output)
    {
        output?.Clear();
        if (output == null || defenseCards == null) return;

        for (int i = 0; i < defenseCards.Count; i++)
        {
            var c = defenseCards[i];
            if (c?.specialCardEffect is not DefenseIncludedPostDamageEffectSO so) continue;
            output.Add((c, so));
        }
    }
}
