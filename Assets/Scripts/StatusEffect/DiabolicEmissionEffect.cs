using UnityEngine;

/// <summary>
/// Diabolic Emission buff: next successful opponent-target strike resolves as Dark element.
/// </summary>
public sealed class DiabolicEmissionEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.DiabolicEmission;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} is under Diabolic Emission (next strike becomes Dark).");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public bool IsExpired() => false;

    public string GetEffectName() => "\u95C7\u306E\u5E37\u304C\u8A2A\u308C\u308B...";

    public string GetDescription() => "\u6B21\u306E\u653B\u6483\u306F\u3059\u3079\u3066\u95C7\u5C5E\u6027\u306B\u306A\u308B\u3002";
}
