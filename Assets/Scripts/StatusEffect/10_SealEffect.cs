using UnityEngine;

/// <summary>
/// 封印。ターン開始ごとに残りターンが減り、0で解除（永続封印は別途仕様が入るまで未対応）。
/// </summary>
public sealed class SealEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Seal;

    private int _turnsRemaining;

    public SealEffect(int durationTurns)
    {
        _turnsRemaining = Mathf.Max(1, durationTurns);
    }

    public void ApplyEffect(PlayerStatus target) { }

    public void OnTurnStart(PlayerStatus target)
    {
        _turnsRemaining--;
    }

    public void OnRemove(PlayerStatus target) { }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public bool IsExpired() => _turnsRemaining <= 0;

    public string GetEffectName() => "封印";

    public string GetDescription() => $"残り{_turnsRemaining}ターン";
}
