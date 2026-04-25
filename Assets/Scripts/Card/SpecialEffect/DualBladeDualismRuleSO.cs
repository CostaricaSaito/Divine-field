﻿using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「双剣デュアリズム」：1回目の解決（反射・打ち払い・無効化を含む）の後に、同じ攻撃として2回目の防御選択を挟む二段攻撃。
/// <see cref="CardData.specialAttackRule"/> に刺して <see cref="DualBladeDualismRules"/> から参照する。
/// </summary>
[CreateAssetMenu(
    fileName = "DualBladeDualismRule",
    menuName = "DivineField/Special Attack/Dual Blade Dualism Rule")]
public class DualBladeDualismRuleSO : SpecialAttackRuleSO
{
}

/// <summary>
/// 双剣デュアリズム：攻撃に含まれると、同一の攻撃解決を最大2回（3回目は発生しない）行う。
/// </summary>
public static class DualBladeDualismRules
{
    public static bool IsDualBladeDualismRule(CardData c)
    {
        return c != null && c.specialAttackRule is DualBladeDualismRuleSO;
    }

    /// <summary>攻撃コンボのいずれかが <see cref="DualBladeDualismRuleSO"/> なら true。</summary>
    public static bool ContainsDualBladeDualism(IReadOnlyList<CardData> attackCombo)
    {
        if (attackCombo == null) return false;
        for (int i = 0; i < attackCombo.Count; i++)
        {
            if (IsDualBladeDualismRule(attackCombo[i])) return true;
        }
        return false;
    }
}
