using UnityEngine;

/// <summary>
/// 濃霧: 人間プレイヤー（<c>player</c>）に付与されたときだけ、その視点で①②③。敵だけ濃霧なら画面は変化しない（<see cref="BattleStatusUI"/>）。
/// </summary>
public sealed class FogEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Fog;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「濃霧」が付与されました");
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「濃霧」が解除されました");
    }

    public bool IsExpired() => false;

    public string GetEffectName() => "濃霧";

    public string GetDescription() => "数値・状態・手札枚数が判別できなくなる。";
}
