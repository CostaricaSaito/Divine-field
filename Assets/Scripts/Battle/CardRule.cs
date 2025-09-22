using System.Collections.Generic;

public static class CardRules
{
    // �U���t�F�[�Y�Ŏg���邩
    public static bool IsUsableInAttackPhase(CardData c)
    {
        if (c == null) return false;
        if (c.usableInAttackPhase) return true;
        if (c.isPrimaryAttack || c.isAdditionalAttack || c.isCounterAttack) return true;
        if (c.isRecovery) return true;        // �񕜂͍U���^�[��OK�̎d�l
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

    // �h��t�F�[�Y�Ŏg���邩
    public static bool IsUsableInDefensePhase(CardData c)
    {
        if (c == null) return false;
        if (c.usableInDefensePhase) return true;
        if (c.isPrimaryDefense || c.isCounterAttack) return true;
        return c.cardType == CardType.Defense;
    }

    // �����s���i�h��t�F�[�Y�����܂Ȃ��j
    public static bool IsImmediateAction(CardData c)
    {
        if (c == null) return false;
        return (c.cardType == CardType.Recovery || c.isRecovery);
    }

    // 攻撃カードかどうか
    public static bool IsAttackCard(CardData c)
    {
        if (c == null) return false;
        return IsUsableInAttackPhase(c) && !IsUsableInDefensePhase(c);
    }

    // 防御カードかどうか
    public static bool IsDefenseCard(CardData c)
    {
        if (c == null) return false;
        return IsUsableInDefensePhase(c) && !IsUsableInAttackPhase(c);
    }

    // 回復カードかどうか
    public static bool IsRecoveryCard(CardData c)
    {
        if (c == null) return false;
        return IsImmediateAction(c);
    }

    public static List<CardData> GetAttackChoices(List<CardData> hand) => hand.FindAll(IsUsableInAttackPhase);
    public static List<CardData> GetDefenseChoices(List<CardData> hand) => hand.FindAll(IsUsableInDefensePhase);
}
