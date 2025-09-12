using System.Collections.Generic;

public class EnemyAI
{
    // 攻撃カードの選び方：PrimaryAttack を優先、無ければ使える中から先頭
    public CardData SelectAttackCard(List<CardData> enemyHand)
    {
        foreach (var c in enemyHand)
            if (CardRules.IsUsableInAttackPhase(c) && (c.isPrimaryAttack || c.cardType == CardType.Attack))
                return c;

        foreach (var c in enemyHand)
            if (CardRules.IsUsableInAttackPhase(c))
                return c;

        return null;
    }

    // 防御カードの選び方：PrimaryDefense を優先、無ければ使える中から先頭
    public CardData SelectDefenseCard(List<CardData> enemyHand)
    {
        foreach (var c in enemyHand)
            if (CardRules.IsUsableInDefensePhase(c) && (c.isPrimaryDefense || c.cardType == CardType.Defense))
                return c;

        foreach (var c in enemyHand)
            if (CardRules.IsUsableInDefensePhase(c))
                return c;

        return null;
    }
}
