using UnityEngine;

/// <summary>
/// 電脳サイコシス: 混乱同等の攻撃対象ランダム＋鋼属性残存DEF5。
/// </summary>
public sealed class CyberPsychosisEffect : IStatusEffect, IPermanentDefContributor
{
    public const int ResidualSteelDefense = 5;

    public StatusEffectType EffectType => StatusEffectType.CyberPsychosis;

    public PermanentDefContribution GetPermanentDefContribution() =>
        new PermanentDefContribution(ElementType.Steel, ResidualSteelDefense);

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} gained Cyber Psychosis");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} Cyber Psychosis removed");
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "電脳サイコシス";

    public string GetDescription() =>
        "攻撃の対象が自分と相手でランダムになる。鋼属性の残存DEF5が常に適用される。";
}
