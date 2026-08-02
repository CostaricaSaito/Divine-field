using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ダイナマイト：相手に与えた第1段ダメージ（防御差し引き後）を攻撃者も受ける。
/// </summary>
[CreateAssetMenu(fileName = "DynamiteRule", menuName = "DivineField/Special Attack/Dynamite Rule")]
public class DynamiteRuleSO : SpecialAttackRuleSO
{
}

/// <summary>
/// ダイナマイト攻撃の判定。
/// </summary>
public static class DynamiteRules
{
    public static bool IsDynamiteCard(CardData c)
    {
        return c != null && c.specialAttackRule is DynamiteRuleSO;
    }

    public static bool ContainsDynamite(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsDynamiteCard(cards[i]))
                return true;
        }
        return false;
    }
}
