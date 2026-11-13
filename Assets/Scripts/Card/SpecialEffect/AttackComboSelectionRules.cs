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

    /// <summary>攻撃フェーズで手札の「1枚目」として選べるか（AddOn / ゴッドレイジ等は false）。</summary>
    public static bool CanSelectAsFirstAttackCard(CardData card) =>
        CanPickAttackCardNow(card, System.Array.Empty<CardData>());

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
        if (CardRules.IsDefenseUsableAsPhysicalAttack(card))
            return CardRules.IsUsableInAttackPhase(card);
        return card.cardType == CardType.Attack || CardRules.IsRecoveryCard(card);
    }

    /// <summary>
    /// Attack magic combo rules:
    /// Primary attack magic — no physical, no other attack magic.
    /// Flexible/AddOn attack magic — any physical OK; no Primary attack magic (multiple Flexible OK).
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
        if (!ContainsAttackMagic(tentative)) return false;
        return !IsValidAttackMagicCombo(tentative);
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
        if (FreeMagicCastRules.CanAddFreeCastToSelection(card, selection))
            return true;
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
            if (FreeMagicCastRules.GetEffectiveMagicMpCost(test, attackerStatus) > attackerStatus.currentMP)
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

        if (!CanSelectAsFirstAttackCard(primary))
            return BuildGreedyAttackComboWithAddOnPrimary(
                handCandidates, extraCandidates, primary, attackerStatus, handOwner);

        var combo = new List<CardData> { primary };
        if (primary.attackPhaseUseRule == AttackPhaseUseRule.Standalone)
            return combo;
        if (ArchMagicRules.IsArchMagicCard(primary))
            return combo;
        if (combo.Count == 1 && CardRules.SkipsHitAndDefenseWhenSelfTarget(primary))
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

        return IsValidAttackSelection(combo) ? combo : new List<CardData>();
    }

    /// <summary>
    /// AI: <paramref name="attachment"/>（AddOn 等）をメインにしたいとき、合法なベースカードへ後乗りするコンボを探す。
    /// </summary>
    private static List<CardData> BuildGreedyAttackComboWithAddOnPrimary(
        IReadOnlyList<CardData> handCandidates,
        IReadOnlyList<CardData> extraCandidates,
        CardData attachment,
        PlayerStatus attackerStatus,
        PlayerType handOwner)
    {
        if (attachment == null) return new List<CardData>();

        var allCandidates = new List<CardData>();
        if (handCandidates != null) allCandidates.AddRange(handCandidates);
        if (extraCandidates != null) allCandidates.AddRange(extraCandidates);

        List<CardData> best = null;

        for (int i = 0; i < allCandidates.Count; i++)
        {
            var baseCard = allCandidates[i];
            if (baseCard == null || ReferenceEquals(baseCard, attachment)) continue;
            if (baseCard.GetInstanceID() == attachment.GetInstanceID()) continue;
            if (!CanSelectAsFirstAttackCard(baseCard)) continue;

            var combo = BuildGreedyAttackCombo(handCandidates, extraCandidates, baseCard, attackerStatus, handOwner);
            if (!IsValidAttackSelection(combo)) continue;

            if (ContainsInstance(combo, attachment))
            {
                if (best == null || combo.Count > best.Count)
                    best = combo;
                continue;
            }

            if (!CanAddToAttackSelection(combo, attachment, attackerStatus, handOwner))
                continue;

            var withAttachment = new List<CardData>(combo) { attachment };
            if (!IsValidAttackSelection(withAttachment)) continue;

            if (best == null || withAttachment.Count > best.Count)
                best = withAttachment;
        }

        return best ?? new List<CardData>();
    }

    private static bool ContainsAttackMagic(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsComboMagicCard(cards[i])) return true;
        }
        return false;
    }

    private static bool IsValidAttackMagicCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return true;

        bool hasPhysical = false;
        int primaryAttackMagicCount = 0;
        int flexibleAttackMagicCount = 0;

        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (IsComboPhysicalAttackCard(c))
                hasPhysical = true;
            if (!IsComboMagicCard(c))
                continue;

            if (c.attackPhaseUseRule == AttackPhaseUseRule.Primary)
                primaryAttackMagicCount++;
            else if (IsFlexibleOrAddOnUseRule(c))
                flexibleAttackMagicCount++;
            else
                return false;
        }

        if (primaryAttackMagicCount > 0)
        {
            if (hasPhysical) return false;
            if (flexibleAttackMagicCount > 0) return false;
            if (primaryAttackMagicCount > 1) return false;
        }

        return true;
    }

    private static bool IsComboMagicCard(CardData c) =>
        c != null && c.cardType == CardType.Magic && !CardRules.IsRecoveryCard(c);

    private static bool IsComboPhysicalAttackCard(CardData c) =>
        CardRules.CountsAsPhysicalAttackInAttackCombo(c);

    private static bool IsFlexibleOrAddOnUseRule(CardData c) =>
        c != null
        && (c.attackPhaseUseRule == AttackPhaseUseRule.Flexible
            || c.attackPhaseUseRule == AttackPhaseUseRule.AddOn);

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
