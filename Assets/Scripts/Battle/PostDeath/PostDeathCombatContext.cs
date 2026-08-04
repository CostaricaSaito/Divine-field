using System;
using System.Collections.Generic;

/// <summary>
/// Scoped override for post-death attacks (fixed ATK, no damage modifiers).
/// </summary>
public sealed class PostDeathCombatContext : IDisposable
{
    public static PostDeathCombatContext Active { get; private set; }

    public CardData ChainCard { get; }
    public int FixedAttackPower { get; }
    public ElementType AttackElement { get; }

    private PostDeathCombatContext(CardData chainCard, int fixedAttackPower, ElementType attackElement)
    {
        ChainCard = chainCard;
        FixedAttackPower = fixedAttackPower;
        AttackElement = attackElement;
    }

    public static PostDeathCombatContext Begin(CardData chainCard, int fixedAttackPower, ElementType attackElement)
    {
        Active?.Dispose();
        Active = new PostDeathCombatContext(chainCard, fixedAttackPower, attackElement);
        return Active;
    }

    public bool MatchesIncoming(IReadOnlyList<CardData> attackCards)
    {
        if (ChainCard == null || attackCards == null) return false;
        for (int i = 0; i < attackCards.Count; i++)
        {
            if (ReferenceEquals(attackCards[i], ChainCard)) return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (Active == this)
            Active = null;
    }
}
