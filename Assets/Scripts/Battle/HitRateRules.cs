using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命中率の解決（Primary カード・不運・煙幕補正・ロール）。
/// 不運は防御側の判定で、最終命中率を100%に固定し、煙幕より優先する。
/// </summary>
public static class HitRateRules
{
    public const int SmokeAccuracyPenalty = 25;
    /// <summary>未設定（必中）扱いのデフォルト命中率。</summary>
    public const int DefaultHitRatePercent = 100;

    public static bool HasCustomHitRate(CardData card)
    {
        return card != null && HasCustomHitRate(card.hitRate);
    }

    public static bool HasCustomHitRate(int hitRatePercent)
    {
        return hitRatePercent != DefaultHitRatePercent;
    }

    public static string FormatHitRateLabel(int hitRatePercent)
    {
        return $"{hitRatePercent}%";
    }

    /// <summary>
    /// 命中率の主対象: AddOn 以外の先頭（Primary / Flexible / Standalone 等）。無ければ先頭。
    /// </summary>
    public static CardData GetPrimaryForHitRate(IReadOnlyList<CardData> attackCards)
    {
        if (attackCards == null || attackCards.Count == 0) return null;
        for (int i = 0; i < attackCards.Count; i++)
        {
            var c = attackCards[i];
            if (c != null && c.attackPhaseUseRule != AttackPhaseUseRule.AddOn) return c;
        }
        return attackCards[0];
    }

    /// <summary>
    /// Primary の命中率を解決する。
    /// 防御側に不運があれば最終命中率は常に100%（煙幕減算より優先）。
    /// それ以外は攻撃側の煙幕で Primary から減算し 0〜100 にクランプ。
    /// </summary>
    public static int ComputeFinalHitPercent(CardData primary, PlayerStatus attacker, PlayerStatus defender)
    {
        if (primary == null) return 0;

        if (defender != null && HasMisfortune(defender))
            return 100;

        int hr = primary.hitRate;
        if (attacker != null && HasSmoke(attacker))
            hr = Mathf.Max(0, hr - SmokeAccuracyPenalty);
        return Mathf.Clamp(hr, 0, 100);
    }

    private static bool HasMisfortune(PlayerStatus defender)
    {
        if (defender?.activeEffects == null) return false;
        foreach (var e in defender.activeEffects)
        {
            if (e != null && e.EffectType == StatusEffectType.Misfortune)
                return true;
        }
        return false;
    }

    private static bool HasSmoke(PlayerStatus attacker)
    {
        if (attacker?.activeEffects == null) return false;
        foreach (var e in attacker.activeEffects)
        {
            if (e != null && e.EffectType == StatusEffectType.Smoke)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 100% は必中（ロールなし扱い）。0% は必ず外れ。
    /// </summary>
    public static bool RollHit(int finalPercent)
    {
        if (finalPercent >= 100) return true;
        if (finalPercent <= 0) return false;
        return BattleRandom.Range(0, 100) < finalPercent;
    }
}
