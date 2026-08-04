using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TOTAL ATK 表示の演出フロー（カード掲出中の抑制・ランプ前後のロック解除）を <see cref="CardStatsDisplay"/> へ委譲する窓口。
/// ダメージ計算は <see cref="BattleProcessor"/> と各 Rule が担当。
/// </summary>
public static class PlayerAttackTotalDisplayFlow
{
    /// <summary>攻撃シーケンス開始時／キャンセル時：前回の表示ロックを解除する。</summary>
    public static void ResetAttackSequenceDisplayLocks(CardStatsDisplay d)
    {
        if (d == null) return;
        d.ClearAllAttackSequenceDisplayLocks();
    }

    /// <summary>
    /// カード掲出中：マジカルソード上乗せ・ゴッドレイジ 2 倍を TOTAL に反映しない。
    /// 掲出完了後のランプで緑字カウントアップする。
    /// </summary>
    public static void EnterSequentialCardReveal_SuppressPendingModifierRamps(
        CardStatsDisplay d,
        List<CardData> selectedCards,
        int magicalSwordOptionalPowerBonusIfPaid)
    {
        if (d == null || selectedCards == null) return;

        bool suppressMs = MagicalSwordRules.ContainsMagicalSword(selectedCards)
            && magicalSwordOptionalPowerBonusIfPaid > 0;
        bool suppressGod = GodrageRules.IsGodrageDoublingCombo(selectedCards);
        d.SetAttackModifierRevealPhase(suppressMs, suppressGod);
    }
}
