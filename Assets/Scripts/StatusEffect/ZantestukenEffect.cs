using UnityEngine;

/// <summary>
/// Zantestuken buff: next successful opponent-target strike skips defense phase entirely.
/// </summary>
public sealed class ZantestukenEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Zantestuken;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} is under Zantestuken (next strike is unblockable).");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public bool IsExpired() => false;

    public string GetEffectName() => "\u65AC\u9244\u5263";

    public string GetDescription() => "\u6B21\u306E\u76F8\u624B\u5BFE\u8C61\u653B\u6483\u3067\u9632\u5FA1\u30D5\u30A7\u30FC\u30BA\u3092\u7121\u8996\u3059\u308B\u3002";
}
