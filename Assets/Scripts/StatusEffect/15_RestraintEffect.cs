using UnityEngine;

/// <summary>
/// 拘束（公式15）：防御フェーズで防御カードを1枚まで。特定の回復で治癒するまで永続（ターン経過では消えない）。
/// </summary>
public sealed class RestraintEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Restraint;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「拘束」が付与されました");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「拘束」が解除されました");
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "拘束";

    public string GetDescription() => "体が重く、防御カードを1枚までしか選べない。特定の回復で治癒するまで続く。";
}
