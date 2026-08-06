using UnityEngine;

/// <summary>
/// Freeze (official id 10). Decays on turn owner's EndPhase, not OnTurnStart.
/// </summary>
public sealed class FreezeEffect : IStatusEffect
{
    public StatusEffectType EffectType => StatusEffectType.Freeze;

    private int _turnsRemaining;

    public FreezeEffect(int durationTurns)
    {
        _turnsRemaining = Mathf.Max(1, durationTurns);
    }

    public int TurnsRemaining => _turnsRemaining;

    public void AddTurns(int additionalTurns)
    {
        if (additionalTurns <= 0) return;
        _turnsRemaining += additionalTurns;
    }

    /// <summary>Called from <see cref="FreezeTurnEndProcessor"/> at turn owner's EndPhase.</summary>
    public void DecrementAtTurnEnd()
    {
        _turnsRemaining--;
    }

    public void ApplyEffect(PlayerStatus target) { }

    public void OnTurnStart(PlayerStatus target) { }

    public void OnRemove(PlayerStatus target) { }

    public int ModifyDamage(int originalDamage) => originalDamage;

    public int ModifyOutgoingDamage(int outgoingDamage) => outgoingDamage;

    public bool IsExpired() => _turnsRemaining <= 0;

    public string GetEffectName() => "凍結";

    public string GetDescription() => $"残り{_turnsRemaining}ターン";
}
