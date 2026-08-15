using System.Collections.Generic;

/// <summary>カード種別・フェーズ可否の共通ルール。</summary>
public static class CardRules
{
    /// <summary>
    /// 手札に保持するだけのパッシブカード（道連れ・不死鳥等）。いかなるフェーズでも手動使用不可。
    /// </summary>
    public static bool IsPassiveHandOnly(CardData c)
    {
        if (c == null) return false;
        if (c.passiveHandOnly) return true;
        if (c.postDeathCardEffect != null) return true;
        if (c.nearDeathCardEffect != null) return true;
        return false;
    }

    /// <summary>HP/MP/GP 回復または全状態異常解除を持つか。</summary>
    public static bool HasRecoveryEffect(CardData c)
    {
        if (c == null) return false;
        return c.healsHP || c.healsMP || c.healsGP || c.cureAllStatusEffects;
    }

    /// <summary>通常防具・防御専用魔法など（反射/打ち払い攻撃カードは含まない）。AI・レイアウト用。</summary>
    public static bool IsPrimaryDefenseCard(CardData c)
    {
        if (c == null) return false;
        if (c.cardType == CardType.Defense) return true;
        return c.cardType == CardType.Magic
            && IsUsableInDefensePhase(c)
            && !HasRecoveryEffect(c)
            && !c.usableInAttackPhase;
    }

    /// <summary>攻撃フェーズで使用できるか。</summary>
    public static bool IsUsableInAttackPhase(CardData c)
    {
        if (c == null) return false;
        if (IsPassiveHandOnly(c)) return false;
        if (c.cardType == CardType.Ultimate) return false;
        if (c.cardType == CardType.Disaster) return false;
        if (c.usableInAttackPhase) return true;
        if (c.cardType == CardType.ArchMagic) return true;
        if (IsRecoveryCard(c)) return true;
        if (c.cardType == CardType.Magic) return false;
        if (c.cardType == CardType.Special)
        {
            if (ShiningBarrierRules.IsShiningBarrierCard(c)) return false;
            return c.specialCardEffect != null && c.postDeathCardEffect == null;
        }
        return c.cardType == CardType.Attack || c.cardType == CardType.Recovery;
    }

    /// <summary>防御フェーズで使用できるか。</summary>
    public static bool IsUsableInDefensePhase(CardData c)
    {
        if (c == null) return false;
        if (IsPassiveHandOnly(c)) return false;
        if (c.cardType == CardType.Disaster) return false;
        if (c.usableInDefensePhase) return true;
        if (ReflectionRules.IsReflectionCard(c) && c.defensePhaseUseRule != DefensePhaseUseRule.None)
            return true;
        if (ShiningBarrierRules.IsShiningBarrierCard(c)) return true;
        return c.cardType == CardType.Defense;
    }

    /// <summary>
    /// 防御フェーズを挟まず、使用後すぐ効果解決するカード（回復・②の状態異常単体カードなど）。
    /// </summary>
    public static bool IsImmediateAction(CardData c)
    {
        if (c == null) return false;
        if (IsPassiveHandOnly(c)) return false;
        if (IsRecoveryCard(c)) return true;
        if (c.cureAllStatusEffects
            && (c.cardType == CardType.Recovery || c.cardType == CardType.Magic))
            return true;
        if (c.cardType == CardType.Special && c.specialCardEffect != null && c.postDeathCardEffect == null)
        {
            if (ShiningBarrierRules.IsShiningBarrierCard(c)) return false;
            return true;
        }
        return c.canApplyStatusEffect
            && c.statusEffectToApply != StatusEffectType.None
            && c.statusEffectApplyTiming == StatusEffectApplyTiming.OnCardEffectResolve;
    }

    /// <summary>攻撃コンボに Special が1枚でも含まれるか（反射・打ち払い・無効の分類に使用）。</summary>
    public static bool IncomingContainsSpecialCard(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null) return false;
        for (int i = 0; i < incomingAttack.Count; i++)
        {
            var c = incomingAttack[i];
            if (c != null && c.cardType == CardType.Special) return true;
        }
        return false;
    }

    /// <summary>
    /// 単体の即時行動カード（回復・回復魔法・Special 効果・プリズム等）のみの incoming か。
    /// 相手対象時、反射／無効／打ち払いは FULL のみ有効にする分類に使う。
    /// </summary>
    public static bool IncomingIsSingleImmediateActionAttack(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count != 1) return false;
        return IsImmediateAction(incomingAttack[0]);
    }

    /// <summary>Special または単体即時行動。FULL 以外の反射・無効・打ち払いを無効にする。</summary>
    public static bool IncomingRequiresFullOnlyReactiveDefense(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        if (DeadlyChainRules.IsActivePostDeathIncoming(incomingAttack)) return false;
        if (IncomingContainsSpecialCard(incomingAttack)) return true;
        return IncomingIsSingleImmediateActionAttack(incomingAttack);
    }

    /// <summary>
    /// 即時系 incoming に対し選択可能な防御：FULL 反射・FULL 打ち払いのみ（無効化の FULL は未使用）。
    /// </summary>
    public static List<CardData> GetFullOnlyReactiveDefenseChoices(List<CardData> hand, IReadOnlyList<CardData> incoming)
    {
        var result = new List<CardData>();
        if (hand == null || incoming == null || incoming.Count == 0) return result;

        foreach (var c in hand)
        {
            if (c == null || !IsUsableInDefensePhase(c)) continue;
            if (ReflectionRules.CanReflectIncoming(c, incoming) && !result.Contains(c))
                result.Add(c);
            else if (ParryRules.CanParryIncoming(c, incoming) && !result.Contains(c))
                result.Add(c);
        }

        return result;
    }

    /// <summary>incoming 攻撃に対する防御手札候補（反射・打ち払い・無効・通常防御）。</summary>
    public static List<CardData> GetDefenseChoicesForIncoming(List<CardData> hand, IReadOnlyList<CardData> incomingAttack)
    {
        if (hand == null) return new List<CardData>();
        if (incomingAttack == null || incomingAttack.Count == 0)
            return GetDefenseChoices(hand);

        if (IncomingRequiresFullOnlyReactiveDefense(incomingAttack))
            return GetFullOnlyReactiveDefenseChoices(hand, incomingAttack);

        ElementType attackElement = ElementHelper.GetIncomingAttackElement(incomingAttack);
        var defenseChoices = GetDefenseChoicesAgainstAttack(hand, attackElement, incomingAttack);

        foreach (var c in hand)
        {
            if (c != null && ReflectionRules.CanReflectIncoming(c, incomingAttack) && !defenseChoices.Contains(c))
                defenseChoices.Add(c);
        }

        defenseChoices.RemoveAll(c =>
            c != null
            && ReflectionRules.IsReflectionCard(c)
            && !ReflectionRules.CanReflectIncoming(c, incomingAttack)
            && !CanServeAsNormalArmorDefense(c));

        if (BlockingRules.CanBlockPhysical(incomingAttack))
        {
            foreach (var c in hand)
            {
                if (c != null && BlockingRules.IsPhysicalBlockingCard(c) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }
        else
        {
            defenseChoices.RemoveAll(c =>
                c != null
                && BlockingRules.IsPhysicalBlockingCard(c)
                && !CanServeAsNormalArmorDefense(c));
        }

        defenseChoices.RemoveAll(c =>
            c != null
            && ParryRules.IsParryCard(c)
            && !ParryRules.CanParryIncoming(c, incomingAttack)
            && !CanServeAsNormalArmorDefense(c));
        foreach (var c in hand)
        {
            if (c != null && ParryRules.CanParryIncoming(c, incomingAttack) && !defenseChoices.Contains(c))
                defenseChoices.Add(c);
        }

        if (incomingAttack != null && ShiningBarrierRules.CanUseAgainstIncoming(incomingAttack))
        {
            foreach (var c in hand)
            {
                if (c != null && ShiningBarrierRules.IsShiningBarrierCard(c) && !defenseChoices.Contains(c))
                    defenseChoices.Add(c);
            }
        }

        return defenseChoices;
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
        return c.cardType == CardType.Recovery || (c.cardType == CardType.Magic && HasRecoveryEffect(c));
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
        return !IsRecoveryCard(c);
    }

    /// <summary>
    /// 回復魔法かどうか
    /// </summary>
    public static bool IsRecoveryMagic(CardData c)
    {
        if (c == null || c.cardType != CardType.Magic) return false;
        return IsRecoveryCard(c);
    }

    /// <summary>
    /// 攻撃フェーズの手札候補。マジックパネル登録済み（同一参照がプール内）は攻撃手札から除外し、
    /// プールが3種で満杯かつ同種未登録の魔法も追加不可（<see cref="MagicPoolManager.CanAddToPool"/> に合わせる）。
    /// </summary>
    public static List<CardData> GetAttackChoices(List<CardData> hand, PlayerType handOwner = PlayerType.Player)
    {
        if (hand == null) return new List<CardData>();
        return hand.FindAll(c => IsUsableInAttackPhaseForHandRespectingMagicPool(c, handOwner));
    }

    public static bool IsUsableInAttackPhaseForHandRespectingMagicPool(CardData c, PlayerType handOwner)
    {
        if (c == null) return false;
        if (c.cardType == CardType.Magic && MagicPoolManager.I != null)
        {
            if (MagicPoolManager.I.IsInPool(c, handOwner)) return false;
            if (!MagicPoolManager.I.CanAddToPool(c, handOwner)) return false;
        }
        return IsUsableInAttackPhase(c);
    }
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
            if (sel == null || !IsUsableInDefensePhase(sel)) continue;

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

    /// <summary>
    /// 魔法単体攻撃に相当する分類（魔法コンボ、または単独の顕現カード）。反射・衰弱除外などに使用。
    /// </summary>
    public static bool IsMagicClassifiedAttackCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        if (IsMagicOnlyAttackCombo(cards)) return true;
        return cards.Count == 1 && cards[0] != null && cards[0].cardType == CardType.Ultimate;
    }

    /// <summary>
    /// A: ダメージを与えず <see cref="StatusEffectApplyTiming.OnCardEffectResolve"/> のみの魔法（濃霧付与など。ATK0）。
    /// B（将来）: ダメージあり＋<see cref="StatusEffectApplyTiming.WithDamageThrough"/> はここでは false。
    /// </summary>
    public static bool IsStatusOnlyMagicCard(CardData c)
    {
        if (c == null || c.cardType != CardType.Magic || IsRecoveryCard(c)) return false;
        if (!c.canApplyStatusEffect || c.statusEffectToApply == StatusEffectType.None) return false;
        if (c.statusEffectApplyTiming != StatusEffectApplyTiming.OnCardEffectResolve) return false;
        return c.attackPower <= 0;
    }

    /// <summary>攻撃コンボがすべて A（状態異常のみ魔法）か。</summary>
    public static bool IsStatusOnlyMagicAttackCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        foreach (var c in cards)
        {
            if (c == null) return false;
            if (!IsStatusOnlyMagicCard(c)) return false;
        }
        return true;
    }

    /// <summary>
    /// Free ルールの二役防具、または反応系を持たない通常防具。
    /// 反応（反射・無効・打ち払い）が成立しない攻撃に対して DEF として使える。
    /// </summary>
    public static bool CanServeAsNormalArmorDefense(CardData c)
    {
        if (c == null || c.defensePower <= 0) return false;

        bool armorLike = c.cardType == CardType.Defense
            || (c.cardType == CardType.Attack && c.usableInDefensePhase);
        if (!armorLike) return false;

        if (c.reflectionKind == ReflectionKind.None
            && c.blockingKind == BlockingKind.None
            && c.parryKind == ParryKind.None)
        {
            return true;
        }

        return c.defensePhaseUseRule == DefensePhaseUseRule.Free;
    }

    /// <summary>反射・ブロッキング・打ち払いではない通常の盾防御（濃霧付与などでは防げない）。</summary>
    public static bool IsNormalPhysicalDefenseCard(CardData c)
    {
        if (c == null) return false;
        if (c.reflectionKind != ReflectionKind.None) return false;
        if (c.blockingKind != BlockingKind.None) return false;
        if (c.parryKind != ParryKind.None) return false;
        if (c.defensePower <= 0) return false;
        if (c.cardType == CardType.Defense) return true;
        return c.cardType == CardType.Attack && c.usableInDefensePhase;
    }

    /// <summary>防御リストに通常防具が含まれるか（誤選択時の状態異常抑止用）。</summary>
    public static bool DefenseContainsNormalPhysicalArmor(IReadOnlyList<CardData> defenseCards)
    {
        if (defenseCards == null) return false;
        foreach (var c in defenseCards)
        {
            if (c != null && IsNormalPhysicalDefenseCard(c)) return true;
        }
        return false;
    }

    /// <summary>
    /// <see cref="GetDefenseChoicesForElement"/> の結果から、攻撃が濃霧付与系のときは通常防具を除く。
    /// </summary>
    public static List<CardData> GetDefenseChoicesAgainstAttack(
        List<CardData> hand,
        ElementType attackElement,
        IReadOnlyList<CardData> attackCards)
    {
        var baseList = GetDefenseChoicesForElement(hand, attackElement);
        if (attackCards == null || !IsStatusOnlyMagicAttackCombo(attackCards))
            return baseList;

        var filtered = new List<CardData>();
        foreach (var c in baseList)
        {
            if (c == null) continue;
            if (IsNormalPhysicalDefenseCard(c)) continue;
            filtered.Add(c);
        }
        return filtered;
    }
}
