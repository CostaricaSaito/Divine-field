using System.Collections.Generic;
using UnityEngine;

public enum EnemyCpuPlanKind
{
    None,
    Prayer,
    Reload,
    Exchange,
    Ultimate,
    ArchMagic,
    Attack,
}

public struct EnemyCpuAttackPlan
{
    public EnemyCpuPlanKind Kind;
    public List<CardData> Combo;

    public static EnemyCpuAttackPlan Attack(List<CardData> combo) =>
        new EnemyCpuAttackPlan { Kind = EnemyCpuPlanKind.Attack, Combo = combo };

    public static EnemyCpuAttackPlan Of(EnemyCpuPlanKind kind) =>
        new EnemyCpuAttackPlan { Kind = kind, Combo = null };
}

/// <summary>Local CPU attack-phase decision: priority waterfall, then weighted-random normal actions.</summary>
public static class EnemyCpuAttackPlanner
{
    private const int PriorityHpThreshold = 30;
    private const int PriorityMpThreshold = 10;

    private const float WeightMagicChant = 1.25f;
    private const float WeightElemental = 1.15f;
    private const float WeightPhysical = 1.0f;
    private const float WeightStatusMagic = 1.1f;
    private const float WeightStatusItem = 0.95f;
    private const float WeightSpecial = 0.8f;

    public static EnemyCpuAttackPlan TryPriority(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill)
    {
        if (hand == null || enemy == null) return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.None);

        if (PrayerFlow.IsPrayOnlyAttackMode(hand))
            return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.Prayer);

        if (TryFindMagicFountain(hand, refill, out CardData fountain))
            return EnemyCpuAttackPlan.Attack(BuildSingle(fountain, hand, enemy, player, refill, false));

        if (TryFindMagicSealer(hand, refill, out CardData sealer))
            return EnemyCpuAttackPlan.Attack(BuildSingle(sealer, hand, enemy, player, refill, false));

        if (TryFindArrowOfIndra(hand, refill, out CardData arrow))
            return EnemyCpuAttackPlan.Attack(BuildSingle(arrow, hand, enemy, player, refill, false));

        if (TryFindStatusCureSelf(hand, enemy, refill, out CardData cure))
            return EnemyCpuAttackPlan.Attack(BuildSingle(cure, hand, enemy, player, refill, false));

        if (TryFindSelfHpRecover(hand, enemy, refill, out CardData hpHeal))
            return EnemyCpuAttackPlan.Attack(BuildSingle(hpHeal, hand, enemy, player, refill, false));

        if (TryFindSelfMpRecover(hand, enemy, refill, out CardData mpHeal))
            return EnemyCpuAttackPlan.Attack(BuildSingle(mpHeal, hand, enemy, player, refill, false));

        if (ShouldOfferReload(enemy) && EnemyCpuHandReloadPlanner.CanReload(hand))
            return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.Reload);

        if (CanCpuExchange(enemy) && EnemyCpuExchangePlanner.CanEmergencyExchange(enemy))
            return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.Exchange);

        if (UltimateReadyRules.IsAvailable(enemy)
            && !enemy.HasFreezeEffect()
            && !enemy.IsCastingArchMagic)
            return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.Ultimate);

        if (TryFindArchMagic(hand, refill, out CardData arch))
        {
            return new EnemyCpuAttackPlan
            {
                Kind = EnemyCpuPlanKind.ArchMagic,
                Combo = BuildSingle(arch, hand, enemy, player, refill, false),
            };
        }

        return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.None);
    }

    public static EnemyCpuAttackPlan PlanNormal(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill)
    {
        var buckets = new List<(float weight, System.Func<List<CardData>> build)>();

        if (HasMagicChantOption(hand, enemy, player, refill))
            buckets.Add((WeightMagicChant, () => BuildMagicChantCombo(hand, enemy, player, refill)));

        if (HasElementalOption(hand, enemy, player, refill))
            buckets.Add((WeightElemental, () => BuildElementalCombo(hand, enemy, player, refill)));

        if (HasPhysicalOption(hand, enemy, refill))
            buckets.Add((WeightPhysical, () => BuildPhysicalCombo(hand, enemy, player, refill, false)));

        if (HasStatusMagicOption(hand, enemy, player, refill))
            buckets.Add((WeightStatusMagic, () => BuildStatusMagicCombo(hand, enemy, player, refill)));

        if (HasStatusItemOption(hand, enemy, player, refill))
            buckets.Add((WeightStatusItem, () => BuildStatusItemCombo(hand, enemy, player, refill)));

        if (HasSpecialOption(hand, enemy, player, refill))
            buckets.Add((WeightSpecial, () => BuildSpecialCombo(hand, enemy, player, refill)));

        if (buckets.Count == 0)
            return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.None);

        float total = 0f;
        for (int i = 0; i < buckets.Count; i++)
            total += buckets[i].weight;

        float roll = BattleRandom.Value * total;
        float acc = 0f;
        for (int i = 0; i < buckets.Count; i++)
        {
            acc += buckets[i].weight;
            if (roll < acc)
            {
                var combo = buckets[i].build();
                if (combo != null && combo.Count > 0)
                    return EnemyCpuAttackPlan.Attack(combo);
                break;
            }
        }

        for (int i = 0; i < buckets.Count; i++)
        {
            var combo = buckets[i].build();
            if (combo != null && combo.Count > 0)
                return EnemyCpuAttackPlan.Attack(combo);
        }

        return EnemyCpuAttackPlan.Of(EnemyCpuPlanKind.None);
    }

    private static bool ShouldOfferReload(PlayerStatus enemy)
    {
        if (enemy?.summonData != null
            && enemy.summonData.IsGarudaLifecycle()
            && UltimateReadyRules.IsAvailable(enemy))
            return false;
        return true;
    }

    private static bool CanCpuExchange(PlayerStatus enemy)
    {
        var bm = BattleManager.I;
        if (bm == null || bm.IsOnlineMatch) return false;
        return bm.EnemyCpuEconomy != null && bm.EnemyCpuEconomy.CanExchange;
    }

    private static List<CardData> BuildSingle(
        CardData primary,
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill,
        bool forbidNeutralMix)
    {
        var handCand = EnemyCpuComboBuilder.FilterHandForAttack(hand, enemy, player, refill);
        var pool = EnemyCpuComboBuilder.GetPoolCandidates(enemy, player, hand);
        return EnemyCpuComboBuilder.BuildGreedy(primary, handCand, hand, pool, enemy, forbidNeutralMix);
    }

    #region Priority helpers

    private static bool TryFindMagicFountain(List<CardData> hand, HandRefillService refill, out CardData card)
    {
        card = null;
        if (!OwnPoolHasMagic()) return false;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) continue;
            if (!MagicFountainRules.IsMagicFountainCard(c)) continue;
            if (!CardRules.IsUsableInAttackPhase(c)) continue;
            card = c;
            return true;
        }
        return false;
    }

    private static bool TryFindMagicSealer(List<CardData> hand, HandRefillService refill, out CardData card)
    {
        card = null;
        if (!OpponentHasPooledMagic()) return false;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) continue;
            if (!MagicSealerRules.IsMagicSealerCard(c)) continue;
            if (!CardRules.IsUsableInAttackPhase(c)) continue;
            card = c;
            return true;
        }
        return false;
    }

    private static bool TryFindArrowOfIndra(List<CardData> hand, HandRefillService refill, out CardData card)
    {
        card = null;
        if (!OpponentHasDestroyableHand()) return false;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) continue;
            if (!ArrowOfIndraRules.IsArrowOfIndraCard(c)) continue;
            if (!CardRules.IsUsableInAttackPhase(c)) continue;
            card = c;
            return true;
        }
        return false;
    }

    private static bool TryFindStatusCureSelf(
        List<CardData> hand,
        PlayerStatus enemy,
        HandRefillService refill,
        out CardData card)
    {
        card = null;
        var picks = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsPrioritySelfStatusCure(c, enemy, refill)) continue;
            picks.Add(c);
        }
        if (picks.Count == 0) return false;
        card = picks[BattleRandom.Range(0, picks.Count)];
        return true;
    }

    private static bool TryFindSelfHpRecover(
        List<CardData> hand,
        PlayerStatus enemy,
        HandRefillService refill,
        out CardData card)
    {
        card = null;
        if (enemy.currentHP >= PriorityHpThreshold) return false;
        var picks = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsPrioritySelfHpRecover(c, enemy, refill)) continue;
            picks.Add(c);
        }
        if (picks.Count == 0) return false;
        card = picks[BattleRandom.Range(0, picks.Count)];
        return true;
    }

    private static bool TryFindSelfMpRecover(
        List<CardData> hand,
        PlayerStatus enemy,
        HandRefillService refill,
        out CardData card)
    {
        card = null;
        if (enemy.currentMP >= PriorityMpThreshold) return false;
        var picks = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsPrioritySelfMpRecover(c, enemy, refill)) continue;
            picks.Add(c);
        }
        if (picks.Count == 0) return false;
        card = picks[BattleRandom.Range(0, picks.Count)];
        return true;
    }

    private static bool TryFindArchMagic(List<CardData> hand, HandRefillService refill, out CardData card)
    {
        card = null;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) continue;
            if (!ArchMagicRules.IsArchMagicCard(c)) continue;
            card = c;
            return true;
        }
        return false;
    }

    private static bool IsPrioritySelfStatusCure(CardData c, PlayerStatus enemy, HandRefillService refill)
    {
        if (c == null || enemy == null) return false;
        if (!CardRules.IsRecoveryCard(c) && c.cardType != CardType.Magic) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) return false;
        if (!CardRules.IsUsableInAttackPhase(c)) return false;
        if (!CardRules.SkipsHitAndDefenseWhenSelfTarget(c)) return false;
        if (c.cardType == CardType.Magic
            && !EnemyMagicAiRules.CanEnemyAddHandMagicToPool(c, enemy, refill, null))
            return false;

        var scope = c.GetStatusEffectCureScope();
        if (scope == StatusEffectCureScope.None) return false;
        return EnemyMagicAiRules.HasCurableStatusEffects(enemy, scope);
    }

    private static bool IsPrioritySelfHpRecover(CardData c, PlayerStatus enemy, HandRefillService refill)
    {
        if (c == null || !c.healsHP) return false;
        if (enemy.currentHP >= enemy.maxHP) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) return false;
        if (!CardRules.IsUsableInAttackPhase(c)) return false;
        if (!CardRules.SkipsHitAndDefenseWhenSelfTarget(c)) return false;
        if (c.cardType == CardType.Magic
            && !EnemyMagicAiRules.CanEnemyAddHandMagicToPool(c, enemy, refill, null))
            return false;
        return true;
    }

    private static bool IsPrioritySelfMpRecover(CardData c, PlayerStatus enemy, HandRefillService refill)
    {
        if (c == null || !c.healsMP) return false;
        if (enemy.currentMP >= enemy.maxMP) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) return false;
        if (!CardRules.IsUsableInAttackPhase(c)) return false;
        if (!CardRules.SkipsHitAndDefenseWhenSelfTarget(c)) return false;
        if (c.cardType == CardType.Magic
            && !EnemyMagicAiRules.CanEnemyAddHandMagicToPool(c, enemy, refill, null))
            return false;
        return true;
    }

    private static bool OwnPoolHasMagic() =>
        MagicPoolManager.I != null && MagicPoolManager.I.GetPooledCardDatas(PlayerType.Enemy).Count > 0;

    private static bool OpponentHasPooledMagic() =>
        MagicPoolManager.I != null && MagicPoolManager.I.GetPooledCardDatas(PlayerType.Player).Count > 0;

    private static bool OpponentHasDestroyableHand()
    {
        var bm = BattleManager.I;
        return bm?.playerHand != null
            && HandDestroyRules.PickRandomDestroyableCard(bm.playerHand, PlayerType.Player) != null;
    }

    #endregion

    #region Normal categories

    private static bool IsStatusOnlyMagic(CardData c)
    {
        if (c == null || !CardRules.IsAttackMagic(c)) return false;
        return c.attackPower <= 0 && c.canApplyStatusEffect;
    }

    private static bool IsChantMagic(CardData c, PlayerStatus enemy, PlayerStatus player, List<CardData> hand)
    {
        if (c == null || !CardRules.IsAttackMagic(c)) return false;
        if (IsStatusOnlyMagic(c)) return false;
        return EnemyMagicAiRules.IsEnemyMagicUsefulAgainstOpponent(c, enemy, player, hand);
    }

    private static bool HasMagicChantOption(List<CardData> hand, PlayerStatus enemy, PlayerStatus player, HandRefillService refill)
    {
        var pool = EnemyCpuComboBuilder.GetPoolCandidates(enemy, player, hand);
        if (pool.Count > 0) return true;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) continue;
            if (IsChantMagic(c, enemy, player, hand)
                && EnemyMagicAiRules.CanEnemyAddHandMagicToPool(c, enemy, refill, hand))
                return true;
        }
        return false;
    }

    private static List<CardData> BuildMagicChantCombo(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill)
    {
        var primaries = new List<CardData>();
        var pool = EnemyCpuComboBuilder.GetPoolCandidates(enemy, player, hand);
        primaries.AddRange(pool);

        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsChantMagic(c, enemy, player, hand)) continue;
            if (!EnemyMagicAiRules.CanEnemyAddHandMagicToPool(c, enemy, refill, hand)) continue;
            if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) continue;
            primaries.Add(c);
        }

        if (primaries.Count == 0) return null;
        var primary = primaries[BattleRandom.Range(0, primaries.Count)];
        return BuildSingle(primary, hand, enemy, player, refill, false);
    }

    private static bool IsElementalPrimary(CardData c, HandRefillService refill)
    {
        if (c == null || c.element == ElementType.None) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) return false;
        if (!CardRules.IsUsableInAttackPhase(c)) return false;
        return CardRules.CountsAsPhysicalAttackInAttackCombo(c)
            || (CardRules.IsAttackMagic(c) && c.attackPower > 0);
    }

    private static bool HasElementalOption(List<CardData> hand, PlayerStatus enemy, PlayerStatus player, HandRefillService refill)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (IsElementalPrimary(hand[i], refill)) return true;
        }
        return false;
    }

    private static List<CardData> BuildElementalCombo(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill)
    {
        var picks = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            if (IsElementalPrimary(hand[i], refill)) picks.Add(hand[i]);
        }
        if (picks.Count == 0) return null;
        var primary = picks[BattleRandom.Range(0, picks.Count)];
        return BuildSingle(primary, hand, enemy, player, refill, true);
    }

    private static bool IsPhysicalPrimary(CardData c, HandRefillService refill)
    {
        if (c == null) return false;
        if (!EnemyMagicAiRules.IsPhysicalAttackCandidate(c, refill)) return false;
        if (c.element != ElementType.None) return false;
        return true;
    }

    private static bool HasPhysicalOption(List<CardData> hand, PlayerStatus enemy, HandRefillService refill)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (IsPhysicalPrimary(hand[i], refill)) return true;
        }
        return false;
    }

    private static List<CardData> BuildPhysicalCombo(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill,
        bool forbidNeutralMix)
    {
        var picks = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsPhysicalPrimary(c, refill)) continue;
            if (c.cardType == CardType.Attack || c.attackPhaseUseRule == AttackPhaseUseRule.Primary)
                picks.Add(c);
        }
        if (picks.Count == 0)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (IsPhysicalPrimary(hand[i], refill)) picks.Add(hand[i]);
            }
        }
        if (picks.Count == 0) return null;
        var primary = picks[BattleRandom.Range(0, picks.Count)];
        return BuildSingle(primary, hand, enemy, player, refill, forbidNeutralMix);
    }

    private static bool HasStatusMagicOption(List<CardData> hand, PlayerStatus enemy, PlayerStatus player, HandRefillService refill)
    {
        return EnemyMagicAiRules.CollectUsefulHandStatusMagic(hand, enemy, player, refill).Count > 0
            || HasUsefulPoolStatusMagic(enemy, player, hand);
    }

    private static bool HasUsefulPoolStatusMagic(PlayerStatus enemy, PlayerStatus player, List<CardData> hand)
    {
        var pool = EnemyCpuComboBuilder.GetPoolCandidates(enemy, player, hand);
        for (int i = 0; i < pool.Count; i++)
        {
            if (IsStatusOnlyMagic(pool[i])) return true;
        }
        return false;
    }

    private static List<CardData> BuildStatusMagicCombo(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill)
    {
        var handList = EnemyMagicAiRules.CollectUsefulHandStatusMagic(hand, enemy, player, refill);
        var poolStatus = new List<CardData>();
        var pool = EnemyCpuComboBuilder.GetPoolCandidates(enemy, player, hand);
        for (int i = 0; i < pool.Count; i++)
        {
            if (IsStatusOnlyMagic(pool[i])) poolStatus.Add(pool[i]);
        }

        var primaries = new List<CardData>();
        primaries.AddRange(handList);
        primaries.AddRange(poolStatus);
        if (primaries.Count == 0) return null;
        var primary = primaries[BattleRandom.Range(0, primaries.Count)];
        return BuildSingle(primary, hand, enemy, player, refill, false);
    }

    private static bool IsStatusItem(CardData c, PlayerStatus enemy, PlayerStatus player, HandRefillService refill)
    {
        if (c == null || c.cardType == CardType.Magic) return false;
        if (!c.canApplyStatusEffect) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) return false;
        if (!CardRules.IsUsableInAttackPhase(c)) return false;
        return EnemyMagicAiRules.WouldStatusApplyHaveEffectOnTarget(c, player);
    }

    private static bool HasStatusItemOption(List<CardData> hand, PlayerStatus enemy, PlayerStatus player, HandRefillService refill)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            if (IsStatusItem(hand[i], enemy, player, refill)) return true;
        }
        return false;
    }

    private static List<CardData> BuildStatusItemCombo(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill)
    {
        var picks = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            if (IsStatusItem(hand[i], enemy, player, refill)) picks.Add(hand[i]);
        }
        if (picks.Count == 0) return null;
        var primary = picks[BattleRandom.Range(0, picks.Count)];
        return BuildSingle(primary, hand, enemy, player, refill, false);
    }

    private static bool IsOtherSpecial(CardData c, HandRefillService refill)
    {
        if (c == null) return false;
        if (MagicFountainRules.IsMagicFountainCard(c)) return false;
        if (MagicSealerRules.IsMagicSealerCard(c)) return false;
        if (ArrowOfIndraRules.IsArrowOfIndraCard(c)) return false;
        if (!EnemyAI.IsEnemyHandCardAvailable(c, refill)) return false;
        if (!CardRules.IsUsableInAttackPhase(c)) return false;
        if (c.cardType != CardType.Special) return false;
        return c.specialCardEffect != null;
    }

    private static bool HasSpecialOption(List<CardData> hand, PlayerStatus enemy, PlayerStatus player, HandRefillService refill)
    {
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsOtherSpecial(c, refill)) continue;
            if (c.cardType == CardType.Magic) continue;
            if (!CardRules.IsAttackMagic(c) && c.cardType == CardType.Magic) continue;
            if (CardRules.IsRecoveryCard(c)) continue;
            if (c.cardType == CardType.Magic
                && !EnemyMagicAiRules.IsEnemyMagicUsefulAgainstOpponent(c, enemy, player, hand))
                continue;
            return true;
        }
        return false;
    }

    private static List<CardData> BuildSpecialCombo(
        List<CardData> hand,
        PlayerStatus enemy,
        PlayerStatus player,
        HandRefillService refill)
    {
        var picks = new List<CardData>();
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (!IsOtherSpecial(c, refill)) continue;
            picks.Add(c);
        }
        if (picks.Count == 0) return null;
        var primary = picks[BattleRandom.Range(0, picks.Count)];
        return BuildSingle(primary, hand, enemy, player, refill, false);
    }

    #endregion
}
