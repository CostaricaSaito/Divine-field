using UnityEngine;

/// <summary>
/// マジカルソード：確定のあと、任意の MP（<see cref="optionalMpCost"/>）を払い、同じ数値分だけ
/// 攻撃力（<see cref="attackPowerBonus"/>）を上乗せした通常攻撃にできる（UI で選択可）。
/// </summary>
[CreateAssetMenu(fileName = "MagicalSwordRule", menuName = "DivineField/Special Attack/Magical Sword Rule")]
public class MagicalSwordRuleSO : SpecialAttackRuleSO
{
    [Header("MP消耗と上乗せ攻撃力（Inspector で調整）")]
    [Tooltip("魔法攻撃ボタンで払い、攻撃力上乗せを有効にする MP 量 n")]
    [Min(0)]
    public int optionalMpCost = 4;

    [Tooltip("上記 MP を払ったときに上乗せする攻撃力 x")]
    [Min(0)]
    public int attackPowerBonus = 10;
}
