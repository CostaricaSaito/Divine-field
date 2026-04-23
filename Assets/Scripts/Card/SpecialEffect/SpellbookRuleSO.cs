using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「Spellbook of xx」系：カードごとにアセットを分けず、<see cref="forcedAttackElement"/> で
/// 複数枚攻撃時の最終合算属性を指定する（単独1枚では通常の合算ルール）。
/// </summary>
[CreateAssetMenu(fileName = "SpellbookRule", menuName = "DivineField/Special Attack/Spellbook Rule")]
public class SpellbookRuleSO : SpecialAttackRuleSO
{
    [Tooltip("複数枚攻撃に含むと、最終の合算攻撃属性をこの属性に固定する。None のときは魔導書として扱わない。")]
    public ElementType forcedAttackElement = ElementType.None;
}

/// <summary>
/// 魔導書（SpellbookRuleSO）の判定・強制属性の解決。
/// </summary>
public static class SpellbookRules
{
    public static bool TryGetSpellbookRule(CardData c, out SpellbookRuleSO rule)
    {
        rule = c != null ? c.specialAttackRule as SpellbookRuleSO : null;
        return rule != null && rule.forcedAttackElement != ElementType.None;
    }

    /// <summary>
    /// コンボ内の最初の魔導書が指定する強制属性（複数枚ある場合は先頭のみ参照）。
    /// </summary>
    public static bool TryGetForcedComboElement(IReadOnlyList<CardData> cards, out ElementType forced)
    {
        forced = ElementType.None;
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (TryGetSpellbookRule(cards[i], out var book))
            {
                forced = book.forcedAttackElement;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 魔導書が含まれ、かつ2枚以上の攻撃コンボのとき、最終属性を <see cref="TryGetForcedComboElement"/> の値に強制する。
    /// </summary>
    public static bool ShouldForceComboElement(IReadOnlyList<CardData> cards)
    {
        return cards != null && cards.Count >= 2 && TryGetForcedComboElement(cards, out _);
    }

    /// <summary>
    /// カード表示〜属性変化の特別演出が必要か（単独・元から最終属性と一致するコンボでは不要）。
    /// </summary>
    public static bool NeedsElementRevealSequence(IReadOnlyList<CardData> cards)
    {
        if (!ShouldForceComboElement(cards)) return false;
        if (!TryGetForcedComboElement(cards, out var forced)) return false;
        return ElementHelper.GetCombinedElement(cards, applySpellbookElementForce: false) != forced;
    }
}
