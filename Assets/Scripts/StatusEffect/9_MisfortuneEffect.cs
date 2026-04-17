using UnityEngine;

/// <summary>
/// 不運: 命中率の解決は <see cref="HitRateRules"/>（防御側が受ける攻撃は最終100%、煙幕より優先）。
/// </summary>
public sealed class MisfortuneEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Misfortune;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「不運」が付与されました");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「不運」が解除されました");
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "不運";

    public string GetDescription() => "相手からの攻撃を必ず命中されやすくなる。";
}
