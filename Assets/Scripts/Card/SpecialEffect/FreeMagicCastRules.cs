using System.Collections.Generic;

/// <summary>
/// 魔法無料詠唱：フラグ付きカードを魔法と同時選択すると、その魔法の消費MPを0にする。
/// 触媒は ATK/DEF に加算せず、カードは消費される。大魔法は対象外。同時に1枚まで。
/// </summary>
public static class FreeMagicCastRules
{
    public const string HandLabel = "FREE CAST";

    public static bool IsFreeMagicCastCard(CardData card)
    {
        if (card == null) return false;
        if (!card.enablesFreeMagicCast) return false;
        if (CardRules.IsPassiveHandOnly(card)) return false;
        return true;
    }

    public static bool IsEligibleMagic(CardData card)
    {
        if (card == null) return false;
        if (card.cardType != CardType.Magic) return false;
        if (ArchMagicRules.IsArchMagicCard(card)) return false;
        return true;
    }

    public static bool SelectionHasEligibleMagic(IReadOnlyList<CardData> selection)
    {
        if (selection == null) return false;
        for (int i = 0; i < selection.Count; i++)
        {
            if (IsEligibleMagic(selection[i])) return true;
        }
        return false;
    }

    public static bool SelectionHasFreeCast(IReadOnlyList<CardData> selection)
    {
        if (selection == null) return false;
        for (int i = 0; i < selection.Count; i++)
        {
            if (IsFreeMagicCastCard(selection[i])) return true;
        }
        return false;
    }

    public static bool AllSelectedAreFreeCast(IReadOnlyList<CardData> selection)
    {
        if (selection == null || selection.Count == 0) return true;
        for (int i = 0; i < selection.Count; i++)
        {
            var c = selection[i];
            if (c == null) continue;
            if (!IsFreeMagicCastCard(c)) return false;
        }
        return true;
    }

    public static bool IsCombatCatalyst(CardData card, IReadOnlyList<CardData> selection)
        => IsFreeMagicCastCard(card) && SelectionHasEligibleMagic(selection);

    public static bool ShouldShowFreeCastLabel(CardData card, IReadOnlyList<CardData> selection)
        => IsFreeMagicCastCard(card) && SelectionHasEligibleMagic(selection);

    public static bool WaivesMagicMp(IReadOnlyList<CardData> selection)
        => SelectionHasEligibleMagic(selection) && SelectionHasFreeCast(selection);

    public static int GetEffectiveMagicMpCost(IReadOnlyList<CardData> selection, PlayerStatus status)
    {
        if (status == null) return 0;
        if (WaivesMagicMp(selection)) return 0;
        return status.GetTotalEffectiveMagicMpForCards(selection);
    }

    public static bool CanAddFreeCastToSelection(CardData card, IReadOnlyList<CardData> selection)
    {
        if (!IsFreeMagicCastCard(card)) return false;
        if (!SelectionHasEligibleMagic(selection)) return false;
        if (ContainsInstance(selection, card)) return false;
        if (SelectionHasFreeCast(selection)) return false;
        return true;
    }

    public static bool HandHasAvailableCatalyst(IReadOnlyList<CardData> hand)
    {
        return FindAvailableCatalyst(hand) != null;
    }

    public static CardData FindAvailableCatalyst(IReadOnlyList<CardData> hand)
    {
        if (hand == null) return null;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsFreeMagicCastCard(c)) continue;
            if (!EnemyAI.IsEnemyHandCardAvailable(c)) continue;
            return c;
        }
        return null;
    }

    public static void AppendSelectableFreeCastCards(
        List<CardData> allowed,
        IReadOnlyList<CardData> hand,
        IReadOnlyList<CardData> selection)
    {
        if (allowed == null || hand == null) return;
        if (!SelectionHasEligibleMagic(selection)) return;
        if (SelectionHasFreeCast(selection)) return;

        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsFreeMagicCastCard(c)) continue;
            if (ContainsInstance(allowed, c)) continue;
            allowed.Add(c);
        }
    }

    /// <summary>
    /// 即時効果1枚専用フロー（状態異常単体・Special 等）。回復は CardDisplay 掲示の通常攻撃シーケンスへ。
    /// 無料詠唱と併用時は通常の攻撃シーケンスへ。
    /// </summary>
    public static bool ShouldUseImmediateAttackSingleCardFlow(IReadOnlyList<CardData> fullSelection)
    {
        var combat = ExcludeCatalystsFromCombatStats(fullSelection);
        if (combat.Count != 1 || !CardRules.IsImmediateAction(combat[0]))
            return false;
        if (CardRules.IsRecoveryCard(combat[0]))
            return false;
        return !SelectionHasFreeCast(fullSelection);
    }

    public static List<CardData> ExcludeCatalystsFromCombatStats(IReadOnlyList<CardData> cards)
    {
        var result = new List<CardData>();
        if (cards == null) return result;
        bool exclude = SelectionHasEligibleMagic(cards);
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (exclude && IsFreeMagicCastCard(c)) continue;
            result.Add(c);
        }
        return result;
    }

    public static List<CardData> PruneInvalidFreeCastSelections(
        List<CardData> selectedCards,
        bool attackContext,
        bool defenseContext)
    {
        var removed = new List<CardData>();
        if (selectedCards == null) return removed;
        if (SelectionHasEligibleMagic(selectedCards)) return removed;

        for (int i = selectedCards.Count - 1; i >= 0; i--)
        {
            var c = selectedCards[i];
            if (!IsFreeMagicCastCard(c)) continue;

            bool independentlyLegal = attackContext
                ? CardRules.IsUsableInAttackPhase(c)
                : defenseContext && CardRules.IsUsableInDefensePhase(c);
            if (independentlyLegal) continue;

            removed.Add(c);
            selectedCards.RemoveAt(i);
        }

        return removed;
    }

    public static IReadOnlyList<CardData> GetCurrentSelectionOrEmpty()
    {
        if (CardSelectionManager.I == null) return System.Array.Empty<CardData>();
        var sel = CardSelectionManager.I.GetSelectedCards();
        return sel ?? (IReadOnlyList<CardData>)System.Array.Empty<CardData>();
    }

    public static bool ContainsInstance(IReadOnlyList<CardData> list, CardData card)
    {
        if (list == null || card == null) return false;
        int id = card.GetInstanceID();
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (c != null && c.GetInstanceID() == id) return true;
        }
        return false;
    }
}
