using UnityEngine;

/// <summary>
/// 混乱: 攻撃の対象が自分と相手でランダム（<see cref="CardSequenceManager"/> / <see cref="BattleManager"/> で解決）。
/// ファイル名: <c>12_ConfusionEffect.cs</c>（<see cref="StatusEffectType.Confusion"/>）。
/// </summary>
public sealed class ConfusionEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Confusion;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「混乱」が付与されました");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「混乱」が解除されました");
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "混乱";

    public string GetDescription() => "攻撃の対象が自分と相手でランダムになる。";
}
