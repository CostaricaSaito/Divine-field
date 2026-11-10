using System.Collections.Generic;

/// <summary>カード種別・フェーズ可否の共通ルール。</summary>
public static class CardRules
{
    /// <summary>
    /// 手札に保持するだけのパッシブカード（道連れ・不死鳥等）。いかなるフェーズでも手動使用不可。
    /// </summary>
    /// <summary>Shiva ice barrier hand token (<c>FROZEN</c> asset). No hand status line label.</summary>
    public static bool IsShivaFrozenHandCard(CardData c)
    {
        if (c == null) return false;
        return c.name == IceBarrierTransformRegistry.FrozenTemplateAssetName;
    }

    /// <summary>手札リロード（3枚同種）の対象外。</summary>
    public static bool CanParticipateInHandReload(CardData c)
    {
        if (c == null) return false;
        return !IsShivaFrozenHandCard(c);
    }

    public static bool IsPassiveHandOnly(CardData c)
    {
        if (c == null) return false;
        if (IsShivaFrozenHandCard(c)) return true;
        if (SealedExoDionRules.IsSealedExoDion(c)) return true;
        if (c.passiveHandOnly) return true;
        if (c.postDeathCardEffect != null) return true;
        if (c.nearDeathCardEffect != null) return true;
        return false;
    }

    /// <summary>HP/MP/GP 回復または全状態異常解除を持つか。</summary>
    public static bool HasRecoveryEffect(CardData c)
    {
        if (c == null) return false;
        return c.healsHP || c.healsMP || c.healsGP || c.HasStatusEffectCure();
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
            if (c.postDeathCardEffect != null) return false;
            if (c.specialCardEffect != null) return true;
            return c.canApplyStatusEffect
                && c.statusEffectToApply != StatusEffectType.None
                && c.statusEffectApplyTiming == StatusEffectApplyTiming.OnCardEffectResolve;
        }
        return c.cardType == CardType.Attack || c.cardType == CardType.Recovery;
    }

    /// <summary>防御フェーズで使用できるか。</summary>
    public static bool IsUsableInDefensePhase(CardData c)
    {
        if (c == null) return false;
        if (c.cardUI != null && c.cardUI.IsFaceDown()) return false;
        var handRefill = BattleManager.I?.HandRefill;
        if (handRefill != null)
        {
            if (handRefill.IsPlayerCardUsedThisTurn(c))
            {
                bool magicParryStillInPool = c.cardType == CardType.Magic
                    && MagicPoolManager.I != null
                    && MagicPoolManager.I.GetRemainingUses(c) > 0;
                if (!magicParryStillInPool) return false;
            }

            if (handRefill.IsEnemyCardUsedThisTurn(c)) return false;
        }
        if (IsPassiveHandOnly(c)) return false;
        if (c.cardType == CardType.Disaster) return false;
        if (c.usableInDefensePhase) return true;
        if (ReflectionRules.IsReflectionCard(c) && c.defensePhaseUseRule != DefensePhaseUseRule.None)
            return true;
        if (ShiningBarrierRules.IsShiningBarrierCard(c)) return true;
        return c.cardType == CardType.Defense;
    }

    /// <summary>
    /// 自分向けのとき命中・防御をスキップして即時解決するカード（回復・自分付与 Special 等）。
    /// 相手向けは命中→防御→成立後付与ルートを使う。
    /// </summary>
    public static bool SkipsHitAndDefenseWhenSelfTarget(CardData c)
    {
        if (c == null) return false;
        if (IsPassiveHandOnly(c)) return false;
        if (IsStatusConduitMagicCard(c)) return false;
        if (IsRecoveryCard(c)) return true;
        if (c.HasStatusEffectCure()
            && (c.cardType == CardType.Recovery || c.cardType == CardType.Magic || c.cardType == CardType.Ultimate))
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

    /// <summary>後方互換。新規コードは <see cref="SkipsHitAndDefenseWhenSelfTarget"/> を使う。</summary>
    public static bool IsImmediateAction(CardData c) => SkipsHitAndDefenseWhenSelfTarget(c);

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

    /// <summary>状態異常を通す魔法単体（触媒のみの併用は含めない）。</summary>
    public static bool IsStatusConduitMagicCard(CardData c)
    {
        if (c == null || !c.isStatusConduitMagic) return false;
        if (c.cardType != CardType.Magic || IsRecoveryCard(c)) return false;
        return c.canApplyStatusEffect
            && c.statusEffectToApply != StatusEffectType.None
            && c.statusEffectApplyTiming == StatusEffectApplyTiming.OnCardEffectResolve;
    }

    /// <summary>攻撃コンボの戦闘カードがすべて状態異常通過魔法か。</summary>
    public static bool IsStatusConduitMagicAttackCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        var combat = FreeMagicCastRules.ExcludeCatalystsFromCombatStats(cards);
        if (combat.Count == 0) return false;
        foreach (var c in combat)
        {
            if (c == null || !IsStatusConduitMagicCard(c)) return false;
        }
        return true;
    }

    /// <summary>相手向け効果アイテム（回復・OnCardEffectResolve 付与 Special 等）。FULL 反応のみ。</summary>
    public static bool IsOpponentTargetedFullReactiveEffectCard(CardData c)
    {
        if (c == null || IsStatusConduitMagicCard(c)) return false;
        if (IsRecoveryCard(c) && HasRecoveryEffect(c)) return true;
        if (c.canApplyStatusEffect
            && c.statusEffectToApply != StatusEffectType.None
            && c.statusEffectApplyTiming == StatusEffectApplyTiming.OnCardEffectResolve)
            return true;
        if (c.cardType == CardType.Special && c.specialCardEffect != null && c.postDeathCardEffect == null
            && !ShiningBarrierRules.IsShiningBarrierCard(c))
            return true;
        return false;
    }

    /// <summary>Special／相手向け回復・付与アイテム。FULL 以外の反射・無効・打ち払いは不可。</summary>
    public static bool IncomingRequiresFullOnlyReactiveDefense(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        if (DeadlyChainRules.IsActivePostDeathIncoming(incomingAttack)) return false;
        if (IsStatusConduitMagicAttackCombo(incomingAttack)) return false;

        if (IncomingContainsSpecialCard(incomingAttack))
        {
            foreach (var c in incomingAttack)
            {
                if (c != null && ShiningBarrierRules.IsShiningBarrierCard(c)) return false;
            }
            return true;
        }

        var combat = FreeMagicCastRules.ExcludeCatalystsFromCombatStats(incomingAttack);
        return combat.Count == 1 && combat[0] != null && IsOpponentTargetedFullReactiveEffectCard(combat[0]);
    }

    /// <summary>状態異常通過魔法に対し許可される反応系防御か（防具・盾は不可）。</summary>
    public static bool IsAllowedStatusConduitReactiveDefense(CardData c, IReadOnlyList<CardData> incomingAttack)
    {
        if (c == null) return false;
        return ReflectionRules.CanReflectIncoming(c, incomingAttack)
            || ParryRules.CanParryIncoming(c, incomingAttack)
            || BlockingRules.ResolvesAsBlockingNullify(c, incomingAttack);
    }

    /// <summary>状態異常通過魔法 incoming で、防具等の不正な防御が混ざっているか。</summary>
    public static bool DefenseContainsDisallowedStatusConduitDefense(
        IReadOnlyList<CardData> defenseCards,
        IReadOnlyList<CardData> incomingAttack)
    {
        if (!IsStatusConduitMagicAttackCombo(incomingAttack) || defenseCards == null) return false;
        foreach (var c in defenseCards)
        {
            if (c == null) continue;
            if (!IsAllowedStatusConduitReactiveDefense(c, incomingAttack)) return true;
        }
        return false;
    }

    /// <summary>戦闘ダメージ 0（効果・付与・回復のみ）。加護・衰弱等で ATK が乗らない。</summary>
    public static bool IncomingUsesZeroDamageCombat(IReadOnlyList<CardData> incomingAttack)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        return IsStatusConduitMagicAttackCombo(incomingAttack)
            || IncomingRequiresFullOnlyReactiveDefense(incomingAttack);
    }

    /// <summary>命中・防御後に OnCardEffectResolve／回復を適用する incoming か。</summary>
    public static bool IncomingAppliesEstablishedEffectsAfterCombat(
        IReadOnlyList<CardData> incomingAttack,
        IReadOnlyList<CardData> defenseCards)
    {
        if (incomingAttack == null || incomingAttack.Count == 0) return false;
        if (IsStatusConduitMagicAttackCombo(incomingAttack)
            && DefenseContainsDisallowedStatusConduitDefense(defenseCards, incomingAttack))
            return false;
        return IsStatusConduitMagicAttackCombo(incomingAttack)
            || IncomingRequiresFullOnlyReactiveDefense(incomingAttack);
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

    /// <summary>状態異常通過魔法向け：Magic/FULL 反射・打ち払い・無効のみ（通常防具なし）。</summary>
    public static List<CardData> GetStatusConduitMagicDefenseChoices(
        List<CardData> hand,
        IReadOnlyList<CardData> incoming)
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
            else if (BlockingRules.ResolvesAsBlockingNullify(c, incoming) && !result.Contains(c))
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

        if (IsStatusConduitMagicAttackCombo(incomingAttack))
            return GetStatusConduitMagicDefenseChoices(hand, incomingAttack);

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

        foreach (var c in hand)
        {
            if (c != null && LightningRodRules.CanNullifyIncoming(c, incomingAttack) && !defenseChoices.Contains(c))
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

    /// <summary>防御タイプだが攻撃フェーズでも物理攻撃として使えるカード（魔竜の装甲籠手等）。</summary>
    public static bool IsDefenseUsableAsPhysicalAttack(CardData c)
    {
        return c != null
            && c.cardType == CardType.Defense
            && c.usableInAttackPhase
            && c.attackPower > 0;
    }

    /// <summary>攻撃コンボ内で物理攻撃として扱うカード（魔法 Primary との併用判定に使用）。</summary>
    public static bool CountsAsPhysicalAttackInAttackCombo(CardData c)
    {
        if (c == null || FreeMagicCastRules.IsFreeMagicCastCard(c)) return false;
        if (c.cardType == CardType.Attack) return true;
        return IsDefenseUsableAsPhysicalAttack(c);
    }

    /// <summary>
    /// 攻撃シーケンスの戦闘解決・TOTALATKDEF 用。魔竜の装甲兜／靴など攻撃フェーズ後乗り防具を含む。
    /// </summary>
    public static bool ContributesToAttackComboCombat(CardData c, IReadOnlyList<CardData> fullSelection)
    {
        if (c == null) return false;
        if (fullSelection != null && FreeMagicCastRules.IsCombatCatalyst(c, fullSelection)) return false;
        if (IsDefenseUsableAsPhysicalAttack(c)) return true;
        return c.cardType == CardType.Attack
            || c.cardType == CardType.Magic
            || c.cardType == CardType.Ultimate
            || c.cardType == CardType.ArchMagic
            || c.cardType == CardType.Recovery
            || c.cardType == CardType.Special;
    }

    /// <summary>Ultimate Skill カードで回復・全治癒効果を持つか。</summary>
    public static bool IsUltimateRecoverySkillCard(CardData c)
    {
        return c != null && c.cardType == CardType.Ultimate && HasRecoveryEffect(c);
    }

    /// <summary>Ultimate Skill カードでドロー＋マリガン（Ascendant Shade 等）か。</summary>
    public static bool IsUltimateDrawMulliganSkillCard(CardData c)
    {
        return GarudaUltimateRules.IsAscendantShadeCard(c);
    }

    /// <summary>Ultimate Skill カードで相手手札破壊（Judgement Thunder 等）か。</summary>
    public static bool IsUltimateHandDestroySkillCard(CardData c)
    {
        return IndraUltimateRules.IsJudgementThunderCard(c);
    }

    /// <summary>Ultimate Skill カードで斬鉄剣バフ（Zantestuken 等）か。</summary>
    public static bool IsUltimateZantestukenSkillCard(CardData c)
    {
        return OrdinUltimateRules.IsZantestukenCard(c);
    }

    /// <summary>Ultimate Skill カードで千年王国バフ（Millenium Kingdom 等）か。</summary>
    public static bool IsUltimateMilleniumKingdomSkillCard(CardData c)
    {
        return ArcadiasUltimateRules.IsMilleniumKingdomCard(c);
    }

    /// <summary>Ultimate Skill カードで闇の帷バフ（Diabolic Emission 等）か。</summary>
    public static bool IsUltimateDiabolicEmissionSkillCard(CardData c)
    {
        return DiabolosUltimateRules.IsDiabolicEmissionCard(c);
    }

    /// <summary>Ultimate Skill カードでギガフレア（ターン数×2の攻撃力）か。</summary>
    public static bool IsGigaFlareUltimateSkillCard(CardData c) => GigaFlareRules.IsGigaFlareCard(c);

    /// <summary>Ultimate Skill カードでグレイシャルダイアモンド（無限の魔力）か。</summary>
    public static bool IsGlacialDiamondUltimateSkillCard(CardData c) => ShivaUltimateRules.IsGlacialDiamondCard(c);

    // 回復カードかどうか
    public static bool IsRecoveryCard(CardData c)
    {
        if (c == null) return false;
        if (IsUltimateRecoverySkillCard(c)) return true;
        return c.cardType == CardType.Recovery || (c.cardType == CardType.Magic && HasRecoveryEffect(c));
    }

    /// <summary>
    /// TOTAL 赤トグル時の効果対象（<see cref="TotalAtkDefCombatPhaseRules.FormatEffectTargetToggleLabel"/> と同じ）。
    /// 回復・魔泉は赤＝相手／通常は赤＝自分。
    /// </summary>
    public static PlayerStatus ResolvePlayerAttackPhaseEffectTarget(
        PlayerStatus player,
        PlayerStatus enemy,
        bool selfAttackTargetModeRed,
        CardData primaryCombatCard)
    {
        if (player == null) return enemy;
        bool invertedTargetToggle = primaryCombatCard != null
            && (IsRecoveryCard(primaryCombatCard) || MagicFountainRules.IsMagicFountainCard(primaryCombatCard));
        if (invertedTargetToggle)
            return selfAttackTargetModeRed ? enemy : player;
        return selfAttackTargetModeRed ? player : enemy;
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
    /// 手札に CardType.Attack が1枚でもあるか。
    /// </summary>
    public static bool HasAttackTypeCardInHand(IReadOnlyList<CardData> hand)
    {
        if (hand == null) return false;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null && hand[i].cardType == CardType.Attack)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 攻撃フェーズの手札候補。マジックパネル登録済み（同一参照がプール内）は攻撃手札から除外し、
    /// プールが3種で満杯かつ同種未登録の魔法も追加不可（<see cref="MagicPoolManager.CanAddToPool"/> に合わせる）。
    /// </summary>
    public static List<CardData> GetAttackChoices(List<CardData> hand, PlayerType handOwner = PlayerType.Player)
    {
        if (hand == null) return new List<CardData>();
        return hand.FindAll(c =>
            IsUsableInAttackPhaseForHandRespectingMagicPool(c, handOwner)
            && !FreeMagicCastRules.IsCatalystOnlyCard(c));
    }

    /// <summary>無料詠唱触媒専用カードは単体では攻撃フェーズに出さない（魔法併用時のみ <see cref="FreeMagicCastRules"/>）。</summary>
    public static bool IsUsableInAttackPhaseStandalone(CardData c)
    {
        if (FreeMagicCastRules.IsCatalystOnlyCard(c)) return false;
        return IsUsableInAttackPhase(c);
    }

    /// <summary>無料詠唱触媒専用カードは単体では防御フェーズに出さない。</summary>
    public static bool IsUsableInDefensePhaseStandalone(CardData c)
    {
        if (FreeMagicCastRules.IsCatalystOnlyCard(c)) return false;
        return IsUsableInDefensePhase(c);
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
    public static List<CardData> GetDefenseChoices(List<CardData> hand) =>
        hand.FindAll(c => IsUsableInDefensePhaseStandalone(c));

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

    /// <summary>攻撃コンボに Ultimate Skill カードが含まれるか。</summary>
    public static bool ContainsUltimateSkillCard(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].cardType == CardType.Ultimate)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 魔法単体攻撃に相当する分類（魔法コンボ、または単独の Ultimate Skill カード）。反射・衰弱除外などに使用。
    /// 無料詠唱触媒は <see cref="FreeMagicCastRules.ExcludeCatalystsFromCombatStats"/> で除いてから判定する。
    /// </summary>
    public static bool IsMagicClassifiedAttackCombo(IReadOnlyList<CardData> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        var combatCards = FreeMagicCastRules.ExcludeCatalystsFromCombatStats(cards);
        if (combatCards.Count == 0) return false;
        if (IsMagicOnlyAttackCombo(combatCards)) return true;
        return combatCards.Count == 1 && combatCards[0] != null && combatCards[0].cardType == CardType.Ultimate;
    }

    public static bool IsStatusOnlyMagicCard(CardData c) => IsStatusConduitMagicCard(c);

    public static bool IsStatusOnlyMagicAttackCombo(IReadOnlyList<CardData> cards)
        => IsStatusConduitMagicAttackCombo(cards);

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
        if (attackCards != null && IsStatusConduitMagicAttackCombo(attackCards))
            return GetStatusConduitMagicDefenseChoices(hand, attackCards);

        return GetDefenseChoicesForElement(hand, attackElement);
    }
}
