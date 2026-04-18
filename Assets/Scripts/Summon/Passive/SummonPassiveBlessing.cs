using System;

/// <summary>
/// 召喚獣の加護（パッシブ）。攻撃ブロックの合計攻撃力への補正など。
/// <see cref="SummonPassiveBlessingMode"/> とファクトリで生成する（SerializeReference はインスペクター不具合を避けるため未使用）。
/// </summary>
[Serializable]
public abstract class SummonPassiveBlessing
{
    /// <summary>
    /// カードの攻撃力を合算した直後の値に、加護による補正を加えた合計を返す。
    /// </summary>
    public abstract int ApplyToTotalAttackPower(int sumOfCardAttackPower, ElementType combinedAttackElement, PlayerStatus attacker);

    /// <summary>
    /// 攻撃側の加護・カード合計後の合計攻撃力に、防御側の加護による抑制を適用（リヴァイアサン等）。
    /// 防御差し引き・衰弱などの与ダメ補正より前の段階。
    /// </summary>
    public virtual int ApplyOpponentAttackPowerSuppression(
        int attackPowerAfterAttackerSideModifiers,
        ElementType combinedAttackElement,
        PlayerStatus attacker,
        PlayerStatus defender)
    {
        return attackPowerAfterAttackerSideModifiers;
    }
}
