using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命中率の解決（Primary カード・煙幕補正・ロール）。
/// </summary>
public static class HitRateRules
{
    public const int SmokeAccuracyPenalty = 25;

    /// <summary>
    /// Primary の攻撃カードを解決（isPrimaryAttack 優先、なければリスト先頭）。
    /// </summary>
    public static CardData GetPrimaryForHitRate(IReadOnlyList<CardData> attackCards)
    {
        if (attackCards == null || attackCards.Count == 0) return null;
        for (int i = 0; i < attackCards.Count; i++)
        {
            var c = attackCards[i];
            if (c != null && c.isPrimaryAttack) return c;
        }
        return attackCards[0];
    }

    /// <summary>
    /// 攻撃側が煙幕のとき Primary 命中率から減算し 0〜100 にクランプ。
    /// </summary>
    public static int ComputeFinalHitPercent(CardData primary, PlayerStatus attacker)
    {
        if (primary == null) return 0;
        int hr = primary.hitRate;
        if (attacker != null && HasSmoke(attacker))
            hr = Mathf.Max(0, hr - SmokeAccuracyPenalty);
        return Mathf.Clamp(hr, 0, 100);
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
        return Random.Range(0, 100) < finalPercent;
    }
}
