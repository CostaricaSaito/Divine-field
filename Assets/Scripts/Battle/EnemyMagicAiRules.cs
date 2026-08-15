using System.Collections.Generic;

/// <summary>
/// NPC（ローカル CPU）の魔法選択・対象判定。状態異常の無意味使用や自己回復のみなどを判定する。
/// </summary>
public static class EnemyMagicAiRules
{
    /// <summary>手札優先の攻撃系魔法（ダメージを与える魔法）。</summary>
    public static bool IsPrioritizedHandAttackMagic(CardData card)
    {
        if (card == null || !CardRules.IsAttackMagic(card)) return false;
        return card.attackPower > 0;
    }

    public static bool CanEnemyAffordMagic(CardData card, PlayerStatus enemyStatus)
    {
        if (card == null || enemyStatus == null) return false;
        if (enemyStatus.IsMagicUseForbidden()) return false;
        if (card.mpCost <= 0) return true;
        return enemyStatus.currentMP >= enemyStatus.GetEffectiveMagicMpCost(card.mpCost);
    }

    public static bool CanEnemyAddHandMagicToPool(CardData card, PlayerStatus enemyStatus, HandRefillService handRefill)
    {
        if (card == null || card.cardType != CardType.Magic) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(card, handRefill)) return false;
        if (!CardRules.IsUsableInAttackPhaseForHandRespectingMagicPool(card, PlayerType.Enemy)) return false;
        if (!CanEnemyAffordMagic(card, enemyStatus)) return false;
        if (MagicPoolManager.I == null) return true;
        if (MagicPoolManager.I.IsInPool(card, PlayerType.Enemy)) return false;
        return MagicPoolManager.I.CanAddToPool(card, PlayerType.Enemy);
    }

    public static bool CanEnemyUsePoolMagic(CardData card, PlayerStatus enemyStatus)
    {
        if (card == null || !CardRules.IsUsableInAttackPhase(card)) return false;
        if (MagicPoolManager.I == null || !MagicPoolManager.I.IsInPool(card, PlayerType.Enemy)) return false;
        return CanEnemyAffordMagic(card, enemyStatus);
    }

    /// <summary>相手に状態異常付与魔法を使っても効果があるか（病・眼精疲労の重ねがけは例外）。</summary>
    public static bool WouldStatusApplyHaveEffectOnTarget(CardData card, PlayerStatus target)
    {
        if (card == null || target == null || !card.canApplyStatusEffect) return false;

        StatusEffectType type = card.statusEffectToApply;
        if (type == StatusEffectType.None) return false;

        StatusProgressionConfig config = StatusProgressionConfig.GetRuntimeFallback();

        if (DiseaseLineEffect.IsDiseaseFamily(type))
            return true;

        if (type == StatusEffectType.EyeStrain)
        {
            if (target.HasClusterHeadacheEffect())
                return config.eyeClusterMutuallyExclusive;
            if (target.HasEyeStrainEffect())
                return config.eyeStrainDuplicateEscalatesToCluster;
            return true;
        }

        if (type == StatusEffectType.ClusterHeadache)
            return !target.HasClusterHeadacheEffect();

        if (type == StatusEffectType.Freeze)
            return target.GetFreezeEffect() == null;

        return !TargetHasEffectType(target, type);
    }

    public static bool HasCurableStatusEffects(PlayerStatus status)
    {
        if (status?.activeEffects == null) return false;
        foreach (var e in status.activeEffects)
        {
            if (e != null && e.EffectType != StatusEffectType.None && !StatusEffectRules.IsIndelible(e.EffectType))
                return true;
        }
        return false;
    }

    /// <summary>回復・状態異常解除魔法を NPC 自身に使う価値があるか。</summary>
    public static bool IsEnemySelfRecoveryMagicUseful(CardData card, PlayerStatus enemyStatus)
    {
        if (card == null || enemyStatus == null || !CardRules.IsRecoveryCard(card)) return false;
        if (card.healsHP && enemyStatus.currentHP < enemyStatus.maxHP) return true;
        if (card.healsMP && enemyStatus.currentMP < enemyStatus.maxMP) return true;
        if (card.healsGP && enemyStatus.currentGP < enemyStatus.maxGP) return true;
        if (card.cureAllStatusEffects && HasCurableStatusEffects(enemyStatus)) return true;
        return false;
    }

    /// <summary>相手を対象とする攻撃フェーズ魔法が有用か（回復魔法は常に false）。</summary>
    public static bool IsEnemyMagicUsefulAgainstOpponent(CardData card, PlayerStatus enemyStatus, PlayerStatus playerStatus)
    {
        if (card == null || playerStatus == null) return false;
        if (!CardRules.IsUsableInAttackPhase(card)) return false;
        if (CardRules.IsRecoveryCard(card)) return false;
        if (!CanEnemyAffordMagic(card, enemyStatus)) return false;

        if (CardRules.IsAttackMagic(card))
        {
            if (card.attackPower > 0) return true;
            if (card.canApplyStatusEffect)
                return WouldStatusApplyHaveEffectOnTarget(card, playerStatus);
            return true;
        }

        return false;
    }

    public static bool IsPhysicalAttackCandidate(CardData card, HandRefillService handRefill)
    {
        if (card == null) return false;
        if (card.cardType == CardType.Magic) return false;
        if (ArchMagicRules.IsArchMagicCard(card)) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(card, handRefill)) return false;
        return CardRules.IsUsableInAttackPhase(card);
    }

    public static List<CardData> CollectPrioritizedHandAttackMagic(
        List<CardData> enemyHand,
        PlayerStatus enemyStatus,
        PlayerStatus playerStatus,
        HandRefillService handRefill)
    {
        var result = new List<CardData>();
        if (enemyHand == null) return result;

        foreach (var c in enemyHand)
        {
            if (!IsPrioritizedHandAttackMagic(c)) continue;
            if (!CanEnemyAddHandMagicToPool(c, enemyStatus, handRefill)) continue;
            if (!IsEnemyMagicUsefulAgainstOpponent(c, enemyStatus, playerStatus)) continue;
            result.Add(c);
        }

        return result;
    }

    public static List<CardData> CollectUsefulHandStatusMagic(
        List<CardData> enemyHand,
        PlayerStatus enemyStatus,
        PlayerStatus playerStatus,
        HandRefillService handRefill)
    {
        var result = new List<CardData>();
        if (enemyHand == null) return result;

        foreach (var c in enemyHand)
        {
            if (c == null || !CardRules.IsAttackMagic(c)) continue;
            if (IsPrioritizedHandAttackMagic(c)) continue;
            if (!CanEnemyAddHandMagicToPool(c, enemyStatus, handRefill)) continue;
            if (!IsEnemyMagicUsefulAgainstOpponent(c, enemyStatus, playerStatus)) continue;
            result.Add(c);
        }

        return result;
    }

    public static List<CardData> CollectUsefulHandSelfRecoveryMagic(
        List<CardData> enemyHand,
        PlayerStatus enemyStatus,
        HandRefillService handRefill)
    {
        var result = new List<CardData>();
        if (enemyHand == null) return result;

        foreach (var c in enemyHand)
        {
            if (c == null || !CardRules.IsRecoveryCard(c)) continue;
            if (!CanEnemyAddHandMagicToPool(c, enemyStatus, handRefill)) continue;
            if (!IsEnemySelfRecoveryMagicUseful(c, enemyStatus)) continue;
            result.Add(c);
        }

        return result;
    }

    public static List<CardData> CollectUsefulPoolMagic(
        PlayerStatus enemyStatus,
        PlayerStatus playerStatus)
    {
        var result = new List<CardData>();
        if (MagicPoolManager.I == null) return result;

        foreach (var entry in MagicPoolManager.I.GetPoolEntries(PlayerType.Enemy))
        {
            var c = entry.cardData;
            if (c == null) continue;
            if (!CanEnemyUsePoolMagic(c, enemyStatus)) continue;
            if (!IsEnemyMagicUsefulAgainstOpponent(c, enemyStatus, playerStatus)) continue;
            result.Add(c);
        }

        return result;
    }

    public static List<CardData> CollectPhysicalAttackCandidates(List<CardData> enemyHand, HandRefillService handRefill)
    {
        var result = new List<CardData>();
        if (enemyHand == null) return result;

        foreach (var c in enemyHand)
        {
            if (!IsPhysicalAttackCandidate(c, handRefill)) continue;
            result.Add(c);
        }

        return result;
    }

    private static bool TargetHasEffectType(PlayerStatus target, StatusEffectType type)
    {
        if (target?.activeEffects == null) return false;
        foreach (var e in target.activeEffects)
        {
            if (e != null && e.EffectType == type)
                return true;
        }
        return false;
    }
}
