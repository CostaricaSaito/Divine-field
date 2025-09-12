using System.Collections.Generic;

public static class CardRules
{
    // 攻撃フェーズで使えるか
    public static bool IsUsableInAttackPhase(CardData c)
    {
        if (c == null) return false;
        if (c.usableInAttackPhase) return true;
        if (c.isPrimaryAttack || c.isAdditionalAttack || c.isCounterAttack) return true;
        if (c.isRecovery) return true;        // 回復は攻撃ターンOKの仕様
        if (c.isSpecialEffect) return true;

        switch (c.cardType)
        {
            case CardType.Defense: return false;
            case CardType.Attack:
            case CardType.Magic:
            case CardType.Recovery:
            case CardType.Special: return true;
            default: return false;
        }
    }

    // 防御フェーズで使えるか
    public static bool IsUsableInDefensePhase(CardData c)
    {
        if (c == null) return false;
        if (c.usableInDefensePhase) return true;
        if (c.isPrimaryDefense || c.isCounterAttack) return true;
        return c.cardType == CardType.Defense;
    }

    // 即時行動（防御フェーズを挟まない）
    public static bool IsImmediateAction(CardData c)
    {
        if (c == null) return false;
        return (c.cardType == CardType.Recovery || c.isRecovery);
    }

    public static List<CardData> GetAttackChoices(List<CardData> hand) => hand.FindAll(IsUsableInAttackPhase);
    public static List<CardData> GetDefenseChoices(List<CardData> hand) => hand.FindAll(IsUsableInDefensePhase);
}
