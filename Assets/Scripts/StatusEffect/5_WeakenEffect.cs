using UnityEngine;

/// <summary>衰弱: 与えるダメージが半分になる。</summary>
public sealed class WeakenEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Weaken;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「衰弱」が付与されました");
    }

    /// <summary>受け取るダメージには衰弱は影響しない（与ダメのみ半減）。</summary>
    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage)
    {
        return Mathf.FloorToInt(outgoingDamage * 0.5f);
    }

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「衰弱」が解除されました");
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "衰弱";

    public string GetDescription() => "相手に与えるダメージが半分になる。";
}
