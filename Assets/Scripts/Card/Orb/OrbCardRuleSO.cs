using UnityEngine;

/// <summary>
/// 「◯◯の宝玉」：第1段の実ダメ通過時に臨時効果。DEF0 汎用との区別のため
/// カードにルール（本SO）の参照を必須にする。
/// </summary>
public abstract class OrbCardRuleSO : ScriptableObject
{
    /// <summary>受けた第1段ダメージを基礎にした反撃（獄炎・金剛など）。</summary>
    public virtual bool IsDamageCounter => false;

    /// <summary>反撃の基礎攻撃力。既定は受けたダメージそのまま。</summary>
    public virtual int ScaleReceivedDamageToAttackBase(int receivedDamage) =>
        receivedDamage > 0 ? receivedDamage : 0;

    /// <summary>反撃の攻撃属性を無属性に固定する。カード自身の属性は防御判定に残す。</summary>
    public virtual bool CounterAttackIsNoneElement => false;
}
