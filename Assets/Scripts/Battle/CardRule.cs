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
    /// <summary>
    /// 防御フェーズを挟まず、使用後すぐ効果解決するカード（回復・②の状態異常単体カードなど）。
    /// </summary>
    public static bool IsImmediateAction(CardData c)
    {
        if (c == null) return false;
        if (c.cardType == CardType.Recovery || c.isRecovery) return true;
        return c.canApplyStatusEffect
            && c.statusEffectToApply != StatusEffectType.None
            && c.statusEffectApplyTiming == StatusEffectApplyTiming.OnCardEffectResolve;
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
        return c.cardType == CardType.Recovery || c.isRecovery;
    }

    /// <summary>
    /// 魔法カードかどうか
    /// </summary>
    public static bool IsMagicCard(CardData c)
    {
        if (c == null) return false;
        return c.cardType == CardType.Magic;
    }

    /// <summary>
    /// 攻撃魔法かどうか（単独型 or 組み合わせ型で、回復ではない）
    /// </summary>
    public static bool IsAttackMagic(CardData c)
    {
        if (c == null || c.cardType != CardType.Magic) return false;
        return !c.isRecovery;
    }

    /// <summary>
    /// 回復魔法かどうか
    /// </summary>
    public static bool IsRecoveryMagic(CardData c)
    {
        if (c == null || c.cardType != CardType.Magic) return false;
        return c.isRecovery;
    }

    public static List<CardData> GetAttackChoices(List<CardData> hand) => hand.FindAll(IsUsableInAttackPhase);
    public static List<CardData> GetDefenseChoices(List<CardData> hand) => hand.FindAll(IsUsableInDefensePhase);

    /// <summary>
    /// 攻撃属性を考慮した防御候補を返す。
    /// 無属性攻撃なら全防御カード、属性攻撃なら対応属性+光のみ。
    /// </summary>
    public static List<CardData> GetDefenseChoicesForElement(List<CardData> hand, ElementType attackElement)
    {
        var all = GetDefenseChoices(hand);
        if (attackElement == ElementType.None) return all;
        return all.FindAll(c => ElementHelper.CanDefendAgainst(attackElement, c));
    }

    /// <summary>
    /// 拘束中は防御カードを1枚まで。既に1枚選んでいるときはそのカードだけを許可リストに残す。
    /// 候補は手札側の参照で揃え、選択との照合は InstanceID でも行う（キャンセル後の選び直しで参照がずれるのを防ぐ）。
    /// </summary>
    public static List<CardData> ApplyRestraintDefenseFilter(
        List<CardData> defenseChoices,
        List<CardData> selectedDefenseCards,
        bool defenderHasRestraint)
    {
        if (defenseChoices == null) return new List<CardData>();
        if (!defenderHasRestraint || selectedDefenseCards == null || selectedDefenseCards.Count == 0)
            return defenseChoices;

        var filtered = new List<CardData>();
        foreach (var sel in selectedDefenseCards)
        {
            if (sel == null || !IsDefenseCard(sel)) continue;

            CardData matchInChoices = null;
            foreach (var ch in defenseChoices)
            {
                if (ch == null) continue;
                if (ch == sel || ch.GetInstanceID() == sel.GetInstanceID())
                {
                    matchInChoices = ch;
                    break;
                }
            }

            if (matchInChoices != null)
                filtered.Add(matchInChoices);
        }

        // 照合できなければ制限しない（選択残りと候補の不整合時は選び直し可にする）
        if (filtered.Count == 0)
            return defenseChoices;

        return filtered;
    }

    /// <summary>
    /// 攻撃コンボが魔法カードのみか。魔法単体攻撃は衰弱の対象外。
    /// </summary>
    public static bool IsMagicOnlyAttackCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        bool any = false;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            any = true;
            if (c.cardType != CardType.Magic)
                return false;
        }
        return any;
    }
}
