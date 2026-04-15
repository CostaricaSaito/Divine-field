/// <summary>眼精疲労（戦闘中の数値効果はフェーズ4で拡張予定）。</summary>
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
