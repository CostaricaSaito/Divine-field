using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命中率の解決（Primary カード・不運・煙幕補正・アルカディアス必中・ロール）と UI 表示。
/// 不運は防御側の判定で、最終命中率を100%に固定し、煙幕より優先する。
/// アルカディアス加護は攻撃側の煙幕を無視して100%（呪縛で無効）。
/// </summary>
public static class HitRateRules
{
    public const int SmokeAccuracyPenalty = 25;
    /// <summary>未設定（必中）扱いのデフォルト命中率。</summary>
    public const int DefaultHitRatePercent = 100;

    /// <summary>煙幕等で命中率が下がったときの手札表示色。</summary>
    public static readonly Color PenalizedHitRateHandColor = new Color(1f, 0.45f, 0.72f, 1f);

    private static int _lastPlayerHitRateDisplayKey = int.MinValue;
    private static int _lastEnemyHitRateDisplayKey = int.MinValue;

    public static bool HasCustomHitRate(CardData card)
    {
        return card != null && HasCustomHitRate(card.hitRate);
    }

    public static bool HasCustomHitRate(int hitRatePercent)
    {
        return hitRatePercent != DefaultHitRatePercent;
    }

    /// <summary>
    /// アルカディアス加護（曇りなき光）が有効か。呪縛付与中は false。
    /// </summary>
    public static bool IsArcadiasAlwaysHitActive(PlayerStatus attacker)
    {
        if (attacker == null || attacker.HasCurseBindEffect()) return false;
        var data = attacker.summonData;
        return data != null && data.IsArcadiasAlwaysHit();
    }

    /// <summary>
    /// 手札用の実効命中率（煙幕・アルカディアス等を反映）。
    /// </summary>
    public static int GetDisplayedHitRatePercentForHand(
        CardData card,
        PlayerStatus owner,
        HitRateApplicability.HandContext handContext = HitRateApplicability.HandContext.PlayerHand)
    {
        if (card == null) return DefaultHitRatePercent;
        bool applicable = handContext == HitRateApplicability.HandContext.MagicPanel
            ? HitRateApplicability.ShouldApplyHitRateDisplayOnMagicPanel(card, owner)
            : HitRateApplicability.ShouldApplyHitRateDisplayOnPlayerHand(card);
        if (!applicable) return DefaultHitRatePercent;

        return ComputeFinalHitPercent(card, owner, null, applyAttackerSmokePenalty: true);
    }

    public static int GetDisplayedHitRatePercentForSheet(
        CardData card,
        PlayerStatus owner,
        HitRateApplicability.SheetContext sheetContext = HitRateApplicability.SheetContext.Normal)
    {
        if (card == null) return DefaultHitRatePercent;
        if (!HitRateApplicability.ShouldApplyHitRateDisplayOnCardSheet(card, owner, sheetContext))
            return DefaultHitRatePercent;

        return ComputeFinalHitPercent(card, owner, null, applyAttackerSmokePenalty: true);
    }

    public static bool ShouldDisplayHitRateLabelForHand(
        CardData card,
        PlayerStatus owner,
        HitRateApplicability.HandContext handContext = HitRateApplicability.HandContext.PlayerHand)
    {
        if (card == null) return false;
        bool applicable = handContext == HitRateApplicability.HandContext.MagicPanel
            ? HitRateApplicability.ShouldApplyHitRateDisplayOnMagicPanel(card, owner)
            : HitRateApplicability.ShouldApplyHitRateDisplayOnPlayerHand(card);
        if (!applicable) return false;

        int effective = GetDisplayedHitRatePercentForHand(card, owner, handContext);
        return HasCustomHitRate(card) || effective != DefaultHitRatePercent;
    }

    public static bool ShouldDisplayHitRateLabelForSheet(
        CardData card,
        PlayerStatus owner,
        HitRateApplicability.SheetContext sheetContext = HitRateApplicability.SheetContext.Normal)
    {
        if (card == null) return false;
        if (!HitRateApplicability.ShouldApplyHitRateDisplayOnCardSheet(card, owner, sheetContext))
            return false;

        int effective = GetDisplayedHitRatePercentForSheet(card, owner, sheetContext);
        return HasCustomHitRate(card) || effective != DefaultHitRatePercent;
    }

    public static bool IsHitRateDisplayPenalizedForHand(
        CardData card,
        PlayerStatus owner,
        HitRateApplicability.HandContext handContext = HitRateApplicability.HandContext.PlayerHand)
    {
        if (card == null || owner == null) return false;
        if (!ShouldDisplayHitRateLabelForHand(card, owner, handContext)) return false;

        int effective = GetDisplayedHitRatePercentForHand(card, owner, handContext);
        int nominal = GetNominalHitRatePercentForDisplay(card, owner);
        return effective < nominal;
    }

    /// <summary>攻撃側の煙幕等を戦闘命中判定に適用するか。</summary>
    public static bool ShouldApplyAttackerSmokeForCombat(CardData primary)
    {
        if (InterventionTurnEndProcessor.IsResolving) return false;
        if (DisasterCombatContext.IsActive) return false;
        return HitRateApplicability.IsSubjectToAttackerSmokePenalty(primary);
    }

    /// <summary>煙幕等の攻撃側ペナルティを除いた表示用素命中率（加護は反映）。</summary>
    public static int GetNominalHitRatePercentForDisplay(CardData card, PlayerStatus owner)
    {
        if (card == null) return DefaultHitRatePercent;
        if (IsArcadiasAlwaysHitActive(owner)) return DefaultHitRatePercent;
        return card.hitRate;
    }

    /// <summary>状態変化を監視し、命中率表示に影響するキーが変わったら手札・シートを更新。</summary>
    public static void MonitorAndRefreshHitRateDisplaysIfNeeded(PlayerStatus player, PlayerStatus enemy)
    {
        if (player != null)
        {
            int key = ComputeHitRateDisplayMonitorKey(player);
            if (key != _lastPlayerHitRateDisplayKey)
            {
                _lastPlayerHitRateDisplayKey = key;
                RefreshHitRateDisplaysForOwner(player);
            }
        }

        if (enemy != null)
        {
            int key = ComputeHitRateDisplayMonitorKey(enemy);
            if (key != _lastEnemyHitRateDisplayKey)
            {
                _lastEnemyHitRateDisplayKey = key;
                RefreshHitRateDisplaysForOwner(enemy);
            }
        }
    }

    public static void ResetHitRateDisplayMonitor()
    {
        _lastPlayerHitRateDisplayKey = int.MinValue;
        _lastEnemyHitRateDisplayKey = int.MinValue;
    }

    /// <summary>加護・状態異常の変化時に手札と表示中カードシートの命中率を再描画。</summary>
    public static void RefreshHitRateDisplaysForOwner(PlayerStatus owner)
    {
        if (owner == null) return;

        var bm = BattleManager.I;
        if (bm != null && ReferenceEquals(owner, bm.GetPlayerStatus()))
            bm.RefreshPlayerHandStatusTextForDefenseSnapshot();

        BattleUIManager.I?.RefreshActiveCardSheetHitRateDisplaysForOwner(owner);
        BattleUIManager.I?.RefreshMagicPanelHitRateDisplays();
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
    public static int ComputeFinalHitPercent(
        CardData primary,
        PlayerStatus attacker,
        PlayerStatus defender,
        bool applyAttackerSmokePenalty = true)
    {
        if (primary == null) return 0;

        if (defender != null && HasMisfortune(defender))
            return 100;

        if (IsArcadiasAlwaysHitActive(attacker))
            return DefaultHitRatePercent;

        int hr = primary.hitRate;
        if (applyAttackerSmokePenalty
            && HitRateApplicability.IsSubjectToAttackerSmokePenalty(primary)
            && attacker != null
            && HasSmoke(attacker))
        {
            hr = Mathf.Max(0, hr - SmokeAccuracyPenalty);
        }

        return Mathf.Clamp(hr, 0, 100);
    }

    private static int ComputeHitRateDisplayMonitorKey(PlayerStatus owner)
    {
        if (owner == null) return 0;

        int key = owner.activeEffects != null ? owner.activeEffects.Count : 0;
        if (HasSmoke(owner)) key |= 1 << 8;
        if (owner.HasCurseBindEffect()) key |= 1 << 9;
        if (IsArcadiasAlwaysHitActive(owner)) key |= 1 << 10;

        var bm = BattleManager.I;
        if (bm != null && ReferenceEquals(owner, bm.GetPlayerStatus()) && bm.IsPlayerDefenseInputActive())
            key |= 1 << 11;

        return key;
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
