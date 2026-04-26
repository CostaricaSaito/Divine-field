using System.Collections.Generic;

/// <summary>
/// 攻撃フェーズの手札選択可否（組み合わせ専用カードと単独利用可能カードの区別）。
/// </summary>
public static class AttackComboSelectionRules
{
    /// <summary>
    /// いま <paramref name="card"/> を攻撃選択に追加してよいか。
    /// <see cref="AttackComboPickRule.ComboAttachmentOnly"/> は、既に攻撃カードが1枚以上選ばれているときのみ true。
    /// </summary>
    public static bool CanPickAttackCardNow(CardData card, IReadOnlyList<CardData> currentAttackSelection)
    {
        if (card == null) return false;
        if (card.attackPhaseUseRule != AttackPhaseUseRule.AddOn)
            return true;

        int n = currentAttackSelection?.Count ?? 0;
        return n >= 1;
    }

    /// <summary>
    /// 攻撃候補のうち、現在の選択状況で手札からクリック可能なカードだけを集める。
    /// </summary>
    public static List<CardData> FilterAttackChoicesForCurrentSelection(
        IReadOnlyList<CardData> attackChoicesFromHand,
        IReadOnlyList<CardData> currentAttackSelection)
    {
        var list = new List<CardData>();
        if (attackChoicesFromHand == null) return list;

        for (int i = 0; i < attackChoicesFromHand.Count; i++)
        {
            var c = attackChoicesFromHand[i];
            if (c == null) continue;
            if (CanPickAttackCardNow(c, currentAttackSelection))
                list.Add(c);
        }
        return list;
    }
}
