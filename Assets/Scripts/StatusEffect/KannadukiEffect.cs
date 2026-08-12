using UnityEngine;

/// <summary>
/// 神無月: 物理攻撃力が2倍（試合終了まで・解除不可）。
/// </summary>
public sealed class KannadukiEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Kannaduki;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} is under Kannaduki (physical attack power x2).");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage)
    {
        if (outgoingDamage <= 0) return outgoingDamage;
        return outgoingDamage * 2;
    }

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public bool IsExpired() => false;

    public string GetEffectName() => "神無月";

    public string GetDescription() => "物理攻撃力が2倍になる（試合終了まで）。";
}