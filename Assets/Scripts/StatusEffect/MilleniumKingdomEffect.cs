using UnityEngine;

/// <summary>
/// Arcadias Millenium Kingdom: nullify opponent magic/attribute attacks until match end.
/// </summary>
public sealed class MilleniumKingdomEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.MilleniumKingdom;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} is under Millenium Kingdom.");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public bool IsExpired() => false;

    public string GetEffectName() => "\u5343\u5E74\u738B\u56FD";

    public string GetDescription() =>
        "\u30B2\u30FC\u30E0\u304C\u7D9A\u304F\u9650\u308A\u3001\u76F8\u624B\u306E\u9B54\u6CD5\u653B\u6483\u3068\u5C5E\u6027\u653B\u6483\u3092\u7121\u52B9\u5316\u3059\u308B\u3002";
}
