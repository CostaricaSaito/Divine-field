using UnityEngine;

/// <summary>煙幕: 命中率補正は <see cref="HitRateRules"/> 側で参照（攻撃側の補正）。</summary>
public sealed class SmokeEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Smoke;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「煙幕」が付与されました");
        HitRateRules.RefreshHitRateDisplaysForOwner(target);
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「煙幕」が解除されました");
        HitRateRules.RefreshHitRateDisplaysForOwner(target);
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "煙幕";

    public string GetDescription() => "命中率が下がる。";
}
