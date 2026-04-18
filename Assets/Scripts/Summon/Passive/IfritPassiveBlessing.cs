using System;

/// <summary>
/// 炎の召喚獣イフリートの加護：合計攻撃力に +1、合算属性が炎ならさらに +2（計 +3）。
/// </summary>
[Serializable]
public sealed class IfritPassiveBlessing : SummonPassiveBlessing
{
    public override int ApplyToTotalAttackPower(int sumOfCardAttackPower, ElementType combinedAttackElement, PlayerStatus attacker)
    {
        int bonus = 1;
        if (combinedAttackElement == ElementType.Fire)
            bonus += 2;
        return sumOfCardAttackPower + bonus;
    }
}
