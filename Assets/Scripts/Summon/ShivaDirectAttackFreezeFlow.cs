using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Shiva passive: configurable chance to apply Freeze on qualifying direct attack damage.
/// </summary>
public static class ShivaDirectAttackFreezeFlow
{
    private static ShivaDirectAttackFreezeSettings _settings;
    private static ShivaDirectAttackFreezeSettings _fallbackInstance;

    public static void BindSettings(ShivaDirectAttackFreezeSettings settings)
    {
        _settings = settings;
    }

    private static ShivaDirectAttackFreezeSettings Active
    {
        get
        {
            if (_settings != null) return _settings;
            if (_fallbackInstance == null)
            {
                _fallbackInstance = ScriptableObject.CreateInstance<ShivaDirectAttackFreezeSettings>();
                _fallbackInstance.name = "ShivaDirectAttackFreezeSettings (Runtime Fallback)";
            }
            return _fallbackInstance;
        }
    }

    public static async Task TryApplyFreezeAfterDirectAttackAsync(
        PlayerStatus attacker,
        PlayerStatus defender,
        int firstPhaseDamage,
        bool countsAsDirectAttack,
        CancellationToken ct = default)
    {
        if (!countsAsDirectAttack) return;
        if (firstPhaseDamage <= 0) return;
        if (attacker == null || defender == null) return;
        if (ReferenceEquals(attacker, defender)) return;

        if (attacker.HasCurseBindEffect()) return;
        var summon = attacker.summonData;
        if (summon == null || !summon.IsShivaDirectAttackFreeze()) return;

        var s = Active;
        if (BattleRandom.Range(0, 100) >= s.freezeChancePercent) return;

        ProgressiveStatusApplicator.ApplyFreeze(defender, s.freezeDurationTurns, stackExisting: true);

        var ui = BattleUIManager.I;
        if (ui == null) return;

        float fadeSec = ui.ShowStyledMessagePopup(defender, MessagePopupKind.ShivaFreezeApplied);
        if (fadeSec > 0f)
            await MessagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
    }
}
