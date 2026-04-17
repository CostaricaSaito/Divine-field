using UnityEngine;

/// <summary>
/// 未実装の状態異常をデバッグ付与するときのプレースホルダー。戦闘ロジックへの影響は最小限。
/// </summary>
public sealed class PlaceholderStatusEffect : IStatusEffect
{
    private readonly StatusEffectType _type;

    public PlaceholderStatusEffect(StatusEffectType type)
    {
        _type = type;
    }

    public StatusEffectType EffectType => _type;

    public void ApplyEffect(PlayerStatus target) { }

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public bool IsExpired() => false;

    public string GetEffectName() => StatusEffectPresentation.GetDisplayName(_type);

    public string GetDescription() => "（デバッグ：未実装のプレースホルダー）";
}
