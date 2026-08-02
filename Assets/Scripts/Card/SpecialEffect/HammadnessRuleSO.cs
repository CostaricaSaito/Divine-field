using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 気狂いハンマー：攻撃力 1〜15 をランダムに決定（カードの ATK は 0 想定）。
/// </summary>
[CreateAssetMenu(fileName = "HammadnessRule", menuName = "DivineField/Special Attack/Hammadness Rule")]
public class HammadnessRuleSO : SpecialAttackRuleSO
{
}

/// <summary>
/// 気狂いハンマーの判定と、ランダム決定後の攻撃力合算。
/// ロール値は <see cref="BattleManager.TryGetHammadnessRollSnapshot"/> のスナップショットを参照する
///（プレイヤー／敵とも <c>RunHammadnessAttackIntroAsync</c> で設定する）。
/// </summary>
public static class HammadnessRules
{
    public const int MinRollInclusive = 1;
    public const int MaxRollInclusive = 15;

    /// <summary>Hand / CardSheet label before the roll is revealed.</summary>
    public const string AtkQuestionMarkLabel = "ATK ?";

    public static bool IsHammadnessCard(CardData c)
    {
        return c != null && c.specialAttackRule is HammadnessRuleSO;
    }

    public static bool ContainsHammadness(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return false;
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsHammadnessCard(cards[i]))
                return true;
        }
        return false;
    }

    public static CardData FindFirstHammadnessCard(IReadOnlyList<CardData> cards)
    {
        if (cards == null) return null;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (IsHammadnessCard(c))
                return c;
        }
        return null;
    }

    /// <summary>気狂いハンマー以外のカード attackPower 合計。</summary>
    public static int SumAttackPowerExcludingHammadness(IReadOnlyList<CardData> cards)
    {
        int s = 0;
        if (cards == null) return 0;
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || IsHammadnessCard(c)) continue;
            s += c.attackPower;
        }
        return s;
    }

    public static int RollRandomAttackPower()
    {
        return BattleRandom.Range(MinRollInclusive, MaxRollInclusive + 1);
    }

    /// <summary>気狂いハンマーを含むコンボのカード攻撃力合算（加護・ゴッドレイジの前段）。</summary>
    public static int SumCardAttackPowerForHammadnessCombo(IReadOnlyList<CardData> cards, PlayerStatus attacker)
    {
        if (cards == null) return 0;
        if (!ContainsHammadness(cards))
        {
            int plain = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c != null) plain += c.attackPower;
            }
            if (attacker != null)
                plain += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
            return plain;
        }

        int rolled = 0;
        if (BattleManager.I != null && BattleManager.I.TryGetHammadnessRollSnapshot(out int snap))
            rolled = snap;

        int sum = SumAttackPowerExcludingHammadness(cards);
        if (attacker != null)
            sum += MagicalSwordRules.GetActivePowerBonus(cards, attacker);
        sum += rolled;
        return sum;
    }
}
