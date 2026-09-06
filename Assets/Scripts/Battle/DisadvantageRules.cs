using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Disadvantage (underdog) roll at own StandBy. Once latched, persists until battle end.
/// See DisadvantageImplementation.md for full condition tables.
/// </summary>
public static class DisadvantageRules
{
    public const int AbsoluteResourceThresholdGuaranteed = 10;
    public const int AbsoluteResourceBandMin = 11;
    public const int AbsoluteResourceBandMax = 20;

    public const int HpGapThresholdTier1 = 20;
    public const int HpGapThresholdTier2 = 30;
    public const int HpGapConsecutiveTurnsRequired = 3;

    public const int TotalGapThresholdTier1 = 50;
    public const int TotalGapThresholdTier2 = 70;

    public const int HandGapThresholdTier1 = 3;
    public const int HandGapThresholdTier2 = 5;
    public const int HandGapThresholdTier3 = 7;
    public const int HandGapSelfHandMaxExclusive = 10;

    public const int LongMatchTurnThreshold = 50;
    public const int LongMatchBonusPerCondition = 10;

    public const int MaxChancePercent = 100;

    // pct(total) = BaseAtReferenceTotal * ExpRatio^(ReferenceTotal - total), 11 <= total <= 20
    private const float AbsoluteBandBaseAtReference = 11.96f;
    private const float AbsoluteBandReferenceTotal = 19f;
    private const float AbsoluteBandExpRatio = 1.196f;

    public static bool IsDisadvantaged(PlayerStatus ps)
    {
        if (ps == null) return false;
        return ps.hasEnteredDisadvantage;
    }

    public static int GetResourceTotal(PlayerStatus ps)
    {
        if (ps == null) return 0;
        return ps.currentHP + ps.currentMP + ps.currentGP;
    }

    /// <summary>
    /// Own StandBy: update HP-gap counter, roll chance, latch on success.
    /// </summary>
    public static bool TryRollAndLatchAtTurnStart(
        PlayerStatus self,
        PlayerStatus opponent,
        int selfHandCount,
        int opponentHandCount,
        int battleTurnDisplay)
    {
        if (self == null || self.hasEnteredDisadvantage) return false;

        self.UpdateDisadvantageHpGapCounter(opponent);

        var contributions = CollectLatchContributions(
            self,
            opponent,
            selfHandCount,
            opponentHandCount,
            battleTurnDisplay);

        int chance = SumAppliedPercents(contributions);
        if (chance <= 0) return false;

        bool success = chance >= MaxChancePercent || BattleRandom.Range(0, MaxChancePercent) < chance;
        if (!success) return false;

        string dump = FormatLatchDump(self, opponent, battleTurnDisplay, chance, contributions);
        self.EnterDisadvantage(dump);
        return true;
    }

    public static int ComputeTotalChancePercent(
        PlayerStatus self,
        PlayerStatus opponent,
        int selfHandCount,
        int opponentHandCount,
        int battleTurnDisplay)
    {
        return SumAppliedPercents(CollectLatchContributions(
            self,
            opponent,
            selfHandCount,
            opponentHandCount,
            battleTurnDisplay));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static DisadvantageChanceBreakdown ComputeBreakdown(
        PlayerStatus self,
        PlayerStatus opponent,
        int selfHandCount,
        int opponentHandCount,
        int battleTurnDisplay)
    {
        var breakdown = new DisadvantageChanceBreakdown
        {
            AbsoluteResource = GetAbsoluteResourceBonus(self),
            HpGap = GetHpGapTier1Bonus(self, opponent) + GetHpGapTier2Bonus(self, opponent),
            TotalResourceGap = GetTotalGapTier1Bonus(self, opponent) + GetTotalGapTier2Bonus(self, opponent),
            StatusEffects = SumStatusEffectBonuses(self),
            HandGap = SumHandGapBonuses(selfHandCount, opponentHandCount),
            BattleTurnDisplay = battleTurnDisplay,
            HpGapConsecutiveOwnTurns = self != null ? self.disadvantageHpGapConsecutiveOwnTurns : 0,
            IsLongMatch = battleTurnDisplay >= LongMatchTurnThreshold,
        };
        breakdown.Total = ComputeTotalChancePercent(self, opponent, selfHandCount, opponentHandCount, battleTurnDisplay);
        return breakdown;
    }
#endif

    public static List<DisadvantageLatchContribution> CollectLatchContributions(
        PlayerStatus self,
        PlayerStatus opponent,
        int selfHandCount,
        int opponentHandCount,
        int battleTurnDisplay)
    {
        var list = new List<DisadvantageLatchContribution>();
        if (self == null) return list;

        TryAddContribution(list, "absolute_resource", DescribeAbsoluteResource(self), GetAbsoluteResourceBonus(self), battleTurnDisplay);
        TryAddContribution(list, "hp_gap_tier1", DescribeHpGapTier1(self, opponent), GetHpGapTier1Bonus(self, opponent), battleTurnDisplay);
        TryAddContribution(list, "hp_gap_tier2", DescribeHpGapTier2(self, opponent), GetHpGapTier2Bonus(self, opponent), battleTurnDisplay);
        TryAddContribution(list, "total_gap_tier1", DescribeTotalGapTier1(self, opponent), GetTotalGapTier1Bonus(self, opponent), battleTurnDisplay);
        TryAddContribution(list, "total_gap_tier2", DescribeTotalGapTier2(self, opponent), GetTotalGapTier2Bonus(self, opponent), battleTurnDisplay);

        if (self != null)
        {
            foreach (var type in self.GetActiveAilmentTypesOrdered())
            {
                int baseBonus = GetBonusForStatusEffect(type);
                if (baseBonus <= 0) continue;
                TryAddContribution(
                    list,
                    $"status_{type}",
                    $"Status: {GetStatusEffectLabel(type)}",
                    baseBonus,
                    battleTurnDisplay);
            }
        }

        AccumulateHandGapContributionLines(list, selfHandCount, opponentHandCount, battleTurnDisplay);
        return list;
    }

    public static string FormatLatchDump(
        PlayerStatus self,
        PlayerStatus opponent,
        int battleTurnDisplay,
        int totalChance,
        List<DisadvantageLatchContribution> contributions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Disadvantage] {self?.DisplayName ?? "?"} latched at turn {battleTurnDisplay} (roll {Mathf.Clamp(totalChance, 0, MaxChancePercent)}%)");
        if (self != null && opponent != null)
        {
            sb.AppendLine(
                $"  Resources self={GetResourceTotal(self)} (HP{self.currentHP}/MP{self.currentMP}/GP{self.currentGP}) "
                + $"enemy={GetResourceTotal(opponent)} (HP{opponent.currentHP}/MP{opponent.currentMP}/GP{opponent.currentGP})");
            sb.AppendLine(
                $"  HP gap={opponent.currentHP - self.currentHP}, consecutiveOwnTurns={self.disadvantageHpGapConsecutiveOwnTurns}");
        }

        if (contributions == null || contributions.Count == 0)
        {
            sb.AppendLine("  (no contribution lines — forced latch?)");
            return sb.ToString();
        }

        sb.AppendLine("  Met conditions:");
        for (int i = 0; i < contributions.Count; i++)
        {
            var c = contributions[i];
            string longMatchSuffix = c.LongMatchBonusApplied ? " (+10 long match)" : string.Empty;
            sb.AppendLine($"    - {c.Description}: +{c.BasePercent}%{longMatchSuffix} => +{c.AppliedPercent}%");
        }

        return sb.ToString();
    }

    private static int SumAppliedPercents(List<DisadvantageLatchContribution> contributions)
    {
        if (contributions == null || contributions.Count == 0) return 0;
        int sum = 0;
        for (int i = 0; i < contributions.Count; i++)
            sum += contributions[i].AppliedPercent;
        return Mathf.Clamp(sum, 0, MaxChancePercent);
    }

    private static void TryAddContribution(
        List<DisadvantageLatchContribution> list,
        string id,
        string description,
        int baseBonus,
        int battleTurnDisplay)
    {
        if (baseBonus <= 0 || string.IsNullOrEmpty(description)) return;
        int applied = ApplyLongMatchBonus(baseBonus, battleTurnDisplay);
        list.Add(new DisadvantageLatchContribution
        {
            Id = id,
            Description = description,
            BasePercent = baseBonus,
            AppliedPercent = applied,
            LongMatchBonusApplied = battleTurnDisplay >= LongMatchTurnThreshold,
        });
    }

    private static void AccumulateHandGapContributionLines(
        List<DisadvantageLatchContribution> list,
        int selfHandCount,
        int opponentHandCount,
        int battleTurnDisplay)
    {
        if (selfHandCount >= HandGapSelfHandMaxExclusive) return;

        int gap = opponentHandCount - selfHandCount;
        if (gap >= HandGapThresholdTier1)
        {
            TryAddContribution(
                list,
                "hand_gap_tier1",
                $"Hand gap >= {HandGapThresholdTier1} (self {selfHandCount} / enemy {opponentHandCount})",
                10,
                battleTurnDisplay);
        }
        if (gap >= HandGapThresholdTier2)
        {
            TryAddContribution(
                list,
                "hand_gap_tier2",
                $"Hand gap >= {HandGapThresholdTier2} (self {selfHandCount} / enemy {opponentHandCount})",
                10,
                battleTurnDisplay);
        }
        if (gap >= HandGapThresholdTier3)
        {
            TryAddContribution(
                list,
                "hand_gap_tier3",
                $"Hand gap >= {HandGapThresholdTier3} (self {selfHandCount} / enemy {opponentHandCount})",
                20,
                battleTurnDisplay);
        }
    }

    private static string DescribeAbsoluteResource(PlayerStatus self)
    {
        int total = GetResourceTotal(self);
        if (total <= AbsoluteResourceThresholdGuaranteed)
            return $"Absolute resource total <= {AbsoluteResourceThresholdGuaranteed} (total={total})";
        return $"Absolute resource band {AbsoluteResourceBandMin}-{AbsoluteResourceBandMax} (total={total})";
    }

    private static string DescribeHpGapTier1(PlayerStatus self, PlayerStatus opponent)
    {
        if (self == null || opponent == null) return null;
        int gap = opponent.currentHP - self.currentHP;
        return $"HP gap >= {HpGapThresholdTier1} for {HpGapConsecutiveTurnsRequired}+ own turns (gap={gap}, streak={self.disadvantageHpGapConsecutiveOwnTurns})";
    }

    private static string DescribeHpGapTier2(PlayerStatus self, PlayerStatus opponent)
    {
        if (self == null || opponent == null) return null;
        int gap = opponent.currentHP - self.currentHP;
        return $"HP gap >= {HpGapThresholdTier2} for {HpGapConsecutiveTurnsRequired}+ own turns (gap={gap}, streak={self.disadvantageHpGapConsecutiveOwnTurns})";
    }

    private static string DescribeTotalGapTier1(PlayerStatus self, PlayerStatus opponent)
    {
        if (self == null || opponent == null) return null;
        int gap = GetResourceTotal(opponent) - GetResourceTotal(self);
        return $"Total resource gap >= {TotalGapThresholdTier1} (gap={gap})";
    }

    private static string DescribeTotalGapTier2(PlayerStatus self, PlayerStatus opponent)
    {
        if (self == null || opponent == null) return null;
        int gap = GetResourceTotal(opponent) - GetResourceTotal(self);
        return $"Total resource gap >= {TotalGapThresholdTier2} (gap={gap})";
    }

    private static int ApplyLongMatchBonus(int baseBonus, int battleTurnDisplay)
    {
        if (baseBonus <= 0) return 0;
        if (battleTurnDisplay < LongMatchTurnThreshold) return baseBonus;
        return baseBonus + LongMatchBonusPerCondition;
    }

    private static int GetAbsoluteResourceBonus(PlayerStatus self)
    {
        int total = GetResourceTotal(self);
        if (total <= AbsoluteResourceThresholdGuaranteed) return MaxChancePercent;
        if (total < AbsoluteResourceBandMin || total > AbsoluteResourceBandMax) return 0;

        double bonus = AbsoluteBandBaseAtReference
            * Math.Pow(AbsoluteBandExpRatio, AbsoluteBandReferenceTotal - total);
        return Mathf.Clamp(Mathf.RoundToInt((float)bonus), 1, 50);
    }

    private static bool MeetsHpGapPersistence(PlayerStatus self)
    {
        return self != null && self.disadvantageHpGapConsecutiveOwnTurns >= HpGapConsecutiveTurnsRequired;
    }

    private static int GetHpGapTier1Bonus(PlayerStatus self, PlayerStatus opponent)
    {
        if (!MeetsHpGapPersistence(self) || opponent == null) return 0;
        int gap = opponent.currentHP - self.currentHP;
        return gap >= HpGapThresholdTier1 ? 20 : 0;
    }

    private static int GetHpGapTier2Bonus(PlayerStatus self, PlayerStatus opponent)
    {
        if (!MeetsHpGapPersistence(self) || opponent == null) return 0;
        int gap = opponent.currentHP - self.currentHP;
        return gap >= HpGapThresholdTier2 ? 10 : 0;
    }

    private static int GetTotalGapTier1Bonus(PlayerStatus self, PlayerStatus opponent)
    {
        if (self == null || opponent == null) return 0;
        int gap = GetResourceTotal(opponent) - GetResourceTotal(self);
        return gap >= TotalGapThresholdTier1 ? 30 : 0;
    }

    private static int GetTotalGapTier2Bonus(PlayerStatus self, PlayerStatus opponent)
    {
        if (self == null || opponent == null) return 0;
        int gap = GetResourceTotal(opponent) - GetResourceTotal(self);
        return gap >= TotalGapThresholdTier2 ? 20 : 0;
    }

    private static int SumStatusEffectBonuses(PlayerStatus self)
    {
        if (self == null) return 0;
        int sum = 0;
        foreach (var type in self.GetActiveAilmentTypesOrdered())
            sum += GetBonusForStatusEffect(type);
        return sum;
    }

    private static int SumHandGapBonuses(int selfHandCount, int opponentHandCount)
    {
        if (selfHandCount >= HandGapSelfHandMaxExclusive) return 0;
        int gap = opponentHandCount - selfHandCount;
        int sum = 0;
        if (gap >= HandGapThresholdTier1) sum += 10;
        if (gap >= HandGapThresholdTier2) sum += 10;
        if (gap >= HandGapThresholdTier3) sum += 20;
        return sum;
    }

    private static int GetBonusForStatusEffect(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.PurgatorySickness:
            case StatusEffectType.Confusion:
                return 10;
            case StatusEffectType.SevereSickness:
            case StatusEffectType.Weaken:
            case StatusEffectType.Restraint:
            case StatusEffectType.ClusterHeadache:
            case StatusEffectType.CurseBind:
                return 5;
            default:
                return 0;
        }
    }

    private static string GetStatusEffectLabel(StatusEffectType type)
    {
        int id = StatusEffectCatalog.ToOfficialId(type);
        if (id >= 1 && id <= StatusEffectCatalog.OfficialDisplayNames.Length)
            return StatusEffectCatalog.OfficialDisplayNames[id - 1];
        return type.ToString();
    }
}

public struct DisadvantageLatchContribution
{
    public string Id;
    public string Description;
    public int BasePercent;
    public int AppliedPercent;
    public bool LongMatchBonusApplied;
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public struct DisadvantageChanceBreakdown
{
    public int AbsoluteResource;
    public int HpGap;
    public int TotalResourceGap;
    public int StatusEffects;
    public int HandGap;
    public int HpGapConsecutiveOwnTurns;
    public int BattleTurnDisplay;
    public bool IsLongMatch;
    public int Total;

    public override string ToString()
    {
        return $"Disadvantage chance {Total}% "
            + $"(abs={AbsoluteResource}, hpGap={HpGap}, totalGap={TotalResourceGap}, "
            + $"status={StatusEffects}, hand={HandGap}, hpGapTurns={HpGapConsecutiveOwnTurns}, "
            + $"turn={BattleTurnDisplay}, longMatch={IsLongMatch})";
    }
}
#endif
