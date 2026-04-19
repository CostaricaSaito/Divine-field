using UnityEngine;

/// <summary>
/// 介入（公式13番）：攻撃フェーズ終了後の CombatResolve で、病処理より前に一定確率で追加の攻撃が発生しうる。
/// ファイル名: <c>13_InterventionEffect.cs</c>（<see cref="StatusEffectType.Intervention"/>）。
/// </summary>
public sealed class InterventionEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Intervention;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「介入」が付与されました");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「介入」が解除されました");
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "介入";

    public string GetDescription() => "攻撃フェーズ終了後、未知の力により追加の攻撃が発生することがある。";
}
