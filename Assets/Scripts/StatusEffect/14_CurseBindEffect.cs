using UnityEngine;

/// <summary>
/// 呪縛（公式14番）：付与中は加護スキル（パッシブ）が無効。数値補正は <see cref="SummonPassiveBlessingApplier"/>、
/// ターン起因の加護は召喚ライフサイクル（例: <see cref="SummonGarudaLifecycle"/>）側で判定する。
/// 直接攻撃付与系（例: <see cref="ShivaDirectAttackFreezeFlow"/>）も攻撃者の呪縛で無効。
/// ファイル名: <c>14_CurseBindEffect.cs</c>（<see cref="StatusEffectType.CurseBind"/>）。
/// </summary>
public sealed class CurseBindEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.CurseBind;

    public void ApplyEffect(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} に「呪縛」が付与されました");
        HitRateRules.RefreshHitRateDisplaysForOwner(target);
    }

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target)
    {
        Debug.Log($"{target.DisplayName} の「呪縛」が解除されました");
        HitRateRules.RefreshHitRateDisplaysForOwner(target);
    }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public bool IsExpired() => false;

    public string GetEffectName() => "呪縛";

    public string GetDescription() =>
        "付与されている間、召喚の加護スキル（パッシブ）が無効になる。付与前の恩恵は残る。";
}
