using UnityEngine;

/// <summary>
/// Infinite Manasource: magic costs 0 MP and has infinite uses (hand + magic panel) until match end.
/// </summary>
public sealed class InfiniteManasourceEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.InfiniteManasource;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} is under Infinite Manasource.");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public bool IsExpired() => false;

    public string GetEffectName() => "\u7121\u9650\u306E\u9B54\u529B";

    public string GetDescription() =>
        "\u9B54\u6CD5\u30AB\u30FC\u30C9\u306EMP\u6D88\u8CBB\u304C0\u3001\u4F7F\u7528\u56DE\u6570\u304C\u7121\u9650\u306B\u306A\u308B\u3002";
}
