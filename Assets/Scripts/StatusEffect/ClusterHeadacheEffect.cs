/// <summary>群発頭痛（戦闘中の数値効果はフェーズ4で拡張予定）。</summary>
public sealed class ClusterHeadacheEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.ClusterHeadache;

    public void ApplyEffect(PlayerStatus target) { }
    public void OnTurnStart(PlayerStatus target) { }
    public void OnRemove(PlayerStatus target) { }
    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;
    public bool IsExpired() => false;
    public string GetEffectName() => "群発頭痛";
    public string GetDescription() => "";
}
