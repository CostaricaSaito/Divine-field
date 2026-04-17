/// <summary>群発頭痛：魔法が使用不可（<see cref="PlayerStatus.IsMagicUseForbidden"/>）。</summary>
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
