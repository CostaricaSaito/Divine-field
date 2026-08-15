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
    /// Every card in the selection must be pickable given the other selected attack cards (AddOn needs a base).
    /// </summary>
    public static bool IsValidAttackSelection(IReadOnlyList<CardData> selection)
    {
        if (selection == null || selection.Count == 0) return false;

        for (int i = 0; i < selection.Count; i++)
        {
            var c = selection[i];
            if (c == null) continue;

            var others = new List<CardData>();
            for (int j = 0; j < selection.Count; j++)
            {
                if (j == i) continue;
                var o = selection[j];
                if (o != null) others.Add(o);
            }

            if (!CanPickAttackCardNow(c, others))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Remove attack cards that no longer satisfy <see cref="CanPickAttackCardNow"/> (e.g. AddOn after base canceled).
    /// </summary>
    public static List<CardData> PruneInvalidAttackSelections(
        List<CardData> selectedCards,
        System.Func<CardData, bool> isAttackCard)
    {
        var removed = new List<CardData>();
        if (selectedCards == null || isAttackCard == null) return removed;

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = selectedCards.Count - 1; i >= 0; i--)
            {
                var c = selectedCards[i];
                if (c == null || !isAttackCard(c)) continue;

                var others = new List<CardData>();
                for (int j = 0; j < selectedCards.Count; j++)
                {
                    if (j == i) continue;
                    var o = selectedCards[j];
                    if (o != null) others.Add(o);
                }

                if (!CanPickAttackCardNow(c, others))
                {
                    removed.Add(c);
                    selectedCards.RemoveAt(i);
                    changed = true;
                }
            }
        }

        return removed;
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
            if (!CanPickAttackCardNow(c, currentAttackSelection)) continue;
            if (ConflictsAttackMagicUseRuleMix(currentAttackSelection, c)) continue;
            list.Add(c);
        }
        return list;
    }

    public static bool IsAttackCardForComboSelection(CardData card)
    {
        if (card == null) return false;
        if (card.cardType == CardType.Magic && !CardRules.IsRecoveryCard(card))
            return CardRules.IsUsableInAttackPhase(card);
        if (card.cardType == CardType.ArchMagic) return true;
        if (card.cardType == CardType.Special) return true;
        if (card.cardType == CardType.Ultimate) return true;
        return card.cardType == CardType.Attack || CardRules.IsRecoveryCard(card);
    }

    /// <summary>
    /// Attack + magic in one combo is allowed only when every magic and every physical attack
    /// uses <see cref="AttackPhaseUseRule.Flexible"/> or <see cref="AttackPhaseUseRule.AddOn"/>.
    /// </summary>
    public static bool ConflictsAttackMagicUseRuleMix(
        IReadOnlyList<CardData> currentSelection,
        CardData adding)
    {
        if (adding == null) return false;
        var tentative = new List<CardData>();
        if (currentSelection != null)
        {
            for (int i = 0; i < currentSelection.Count; i++)
            {
                if (currentSelection[i] != null) tentative.Add(currentSelection[i]);
            }
        }
        tentative.Add(adding);
        return HasAttackMagicMix(tentative) && !IsValidAttackMagicUseRuleMix(tentative);
    }

    /// <summary>Legacy name; prefer <see cref="ConflictsAttackMagicUseRuleMix"/>.</summary>
    public static bool ConflictsMagicPrimaryWithPhysicalAttackFlexible(
        IReadOnlyList<CardData> currentSelection,
        CardData adding)
        => ConflictsAttackMagicUseRuleMix(currentSelection, adding);

    /// <summary>
    /// Player UI / AI: whether <paramref name="card"/> may join the current attack selection.
    /// </summary>
    public static bool CanAddToAttackSelection(
        IReadOnlyList<CardData> selection,
        CardData card,
        PlayerStatus attackerStatus,
        PlayerType handOwner)
    {
        if (card == null || selection == null) return false;
        if (!IsAttackCardForComboSelection(card)) return false;
        if (ArchMagicRules.IsArchMagicCard(card)) return false;

        if (card.cardType == CardType.Magic
            && !CardRules.IsUsableInAttackPhaseForHandRespectingMagicPool(card, handOwner))
        {
            if (MagicPoolManager.I == null || !MagicPoolManager.I.IsInPool(card, handOwner))
                return false;
        }
        else if (!CardRules.IsUsableInAttackPhaseForHandRespectingMagicPool(card, handOwner))
        {
            return false;
        }

        if (!CardRules.IsUsableInAttackPhase(card))
            return false;

        int pickId = card.GetInstanceID();
        for (int i = 0; i < selection.Count; i++)
        {
            var s = selection[i];
            if (s != null && s.GetInstanceID() == pickId)
                return false;
        }

        if (GrandMagicRules.ContainsGrandMagicStyleAttack(selection)
            && !GrandMagicRules.IsGrandMagicStyleAttackCard(card))
            return false;

        if (ConflictsAttackMagicUseRuleMix(selection, card))
            return false;

        SelectionRole role = card.attackPhaseRole;
        if (role is SelectionRole.Standalone or SelectionRole.Primary)
            return selection.Count == 0;

        if (role == SelectionRole.Addable)
        {
            if (HasRoleInSelection(selection, SelectionRole.Standalone))
                return false;
            if (card.cardType == CardType.Magic && HasMagicInSelection(selection))
                return false;
        }

        if (!CanPickAttackCardNow(card, selection))
            return false;

        if (attackerStatus != null && card.cardType == CardType.Magic && !CardRules.IsRecoveryCard(card))
        {
            if (attackerStatus.IsMagicUseForbidden()) return false;
            var test = new List<CardData>();
            for (int i = 0; i < selection.Count; i++)
            {
                if (selection[i] != null) test.Add(selection[i]);
            }
            test.Add(card);
            if (attackerStatus.GetTotalEffectiveMagicMpForCards(test) > attackerStatus.currentMP)
                return false;
        }

        var tentative = new List<CardData>();
        for (int i = 0; i < selection.Count; i++)
        {
            if (selection[i] != null) tentative.Add(selection[i]);
        }
        tentative.Add(card);
        return IsValidAttackSelection(tentative);
    }

    /// <summary>
    /// AI: build the largest valid combo starting from <paramref name="primary"/> (player rules).
    /// </summary>
    public static List<CardData> BuildGreedyAttackCombo(
        IReadOnlyList<CardData> handCandidates,
        IReadOnlyList<CardData> extraCandidates,
        CardData primary,
        PlayerStatus attackerStatus,
        PlayerType handOwner)
    {
        if (primary == null) return new List<CardData>();

        var combo = new List<CardData> { primary };
        if (primary.attackPhaseUseRule == AttackPhaseUseRule.Standalone)
            return combo;
        if (ArchMagicRules.IsArchMagicCard(primary))
            return combo;
        if (combo.Count == 1 && CardRules.IsImmediateAction(primary))
            return combo;

        var allCandidates = new List<CardData>();
        if (handCandidates != null) allCandidates.AddRange(handCandidates);
        if (extraCandidates != null) allCandidates.AddRange(extraCandidates);

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < allCandidates.Count; i++)
            {
                var c = allCandidates[i];
                if (c == null) continue;
                if (ContainsInstance(combo, c)) continue;
                if (!CanAddToAttackSelection(combo, c, attackerStatus, handOwner))
                    continue;
                combo.Add(c);
                changed = true;
            }
        }

        return IsValidAttackSelection(combo) ? combo : new List<CardData> { primary };
    }

    private static bool HasAttackMagicMix(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        bool hasMagic = false;
        bool hasPhysicalAttack = false;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (IsComboMagicCard(c)) hasMagic = true;
            if (IsComboPhysicalAttackCard(c)) hasPhysicalAttack = true;
        }
        return hasMagic && hasPhysicalAttack;
    }

    private static bool IsValidAttackMagicUseRuleMix(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return true;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (IsComboMagicCard(c) && !IsFlexibleOrAddOnUseRule(c)) return false;
            if (IsComboPhysicalAttackCard(c) && !IsFlexibleOrAddOnUseRule(c)) return false;
        }
        return true;
    }

    private static bool IsComboMagicCard(CardData c) =>
        c != null && c.cardType == CardType.Magic && !CardRules.IsRecoveryCard(c);

    private static bool IsComboPhysicalAttackCard(CardData c) =>
        c != null && c.cardType == CardType.Attack;

    private static bool IsFlexibleOrAddOnUseRule(CardData c) =>
        c != null
        && (c.attackPhaseUseRule == AttackPhaseUseRule.Flexible
            || c.attackPhaseUseRule == AttackPhaseUseRule.AddOn);

    private static bool HasMagicInSelection(IReadOnlyList<CardData> selection)
    {
        for (int i = 0; i < (selection?.Count ?? 0); i++)
        {
            var c = selection[i];
            if (c != null && c.cardType == CardType.Magic && !CardRules.IsRecoveryCard(c))
                return true;
        }
        return false;
    }

    private static bool HasRoleInSelection(IReadOnlyList<CardData> selection, SelectionRole role)
    {
        for (int i = 0; i < (selection?.Count ?? 0); i++)
        {
            var c = selection[i];
            if (c != null && c.attackPhaseRole == role)
                return true;
        }
        return false;
    }

    private static bool ContainsInstance(IReadOnlyList<CardData> selection, CardData card)
    {
        int id = card.GetInstanceID();
        for (int i = 0; i < selection.Count; i++)
        {
            var c = selection[i];
            if (c != null && c.GetInstanceID() == id)
                return true;
        }
        return false;
    }
}
