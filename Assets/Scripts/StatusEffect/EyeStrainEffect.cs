/// <summary>眼精疲労：魔法の消費MPが2倍（<see cref="PlayerStatus.GetEffectiveMagicMpCost"/>）。</summary>
public sealed class EyeStrainEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.EyeStrain;

    public void ApplyEffect(PlayerStatus target) { }
    public void OnTurnStart(PlayerStatus target) { }
    public void OnRemove(PlayerStatus target) { }
    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;
    public bool IsExpired() => false;
    public string GetEffectName() => "眼精疲労";
    public string GetDescription() => "";
}
