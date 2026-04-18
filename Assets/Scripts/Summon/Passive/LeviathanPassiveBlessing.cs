using System;
using UnityEngine;

/// <summary>
/// 水の召喚獣リヴァイアサンの加護：相手の合計攻撃力を -1、合算攻撃属性が無属性以外ならさらに -1（防御・ダメージ計算の前段）。
/// 状態異常ターン処理などカード戦闘外は <see cref="BattleProcessor"/> の攻撃力計算を経由しない。
/// </summary>
[Serializable]
public sealed class LeviathanPassiveBlessing : SummonPassiveBlessing
{
    public override int ApplyToTotalAttackPower(int sumOfCardAttackPower, ElementType combinedAttackElement, PlayerStatus attacker)
    {
        return sumOfCardAttackPower;
    }

    public override int ApplyOpponentAttackPowerSuppression(
        int attackPowerAfterAttackerSideModifiers,
        ElementType combinedAttackElement,
        PlayerStatus attacker,
        PlayerStatus defender)
    {
        if (attackPowerAfterAttackerSideModifiers < 0) return attackPowerAfterAttackerSideModifiers;
        if (defender == null || attacker == null) return attackPowerAfterAttackerSideModifiers;
        if (ReferenceEquals(attacker, defender)) return attackPowerAfterAttackerSideModifiers;

        int reduction = 1;
        if (combinedAttackElement != ElementType.None)
            reduction += 1;

        return Mathf.Max(0, attackPowerAfterAttackerSideModifiers - reduction);
    }
}
