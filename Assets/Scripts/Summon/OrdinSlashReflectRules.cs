using System.Collections.Generic;

/// <summary>
/// Odin passive (切り払い) eligibility: same as Mirror Sword physical reflect, excluding Shining Barrier force-None.
/// </summary>
public static class OrdinSlashReflectRules
{
    public static bool CanTrigger(
        PlayerStatus defender,
        PlayerStatus attacker,
        IReadOnlyList<CardData> incomingAttack,
        BattleManager battleManager)
    {
        if (defender == null || attacker == null || incomingAttack == null || incomingAttack.Count == 0)
            return false;
        if (ReferenceEquals(defender, attacker))
            return false;

        if (defender.HasCurseBindEffect())
            return false;

        if (defender.summonData == null || !defender.summonData.IsOrdinSlashReflect())
            return false;

        if (battleManager != null)
        {
            if (ReferenceEquals(defender, battleManager.GetPlayerStatus())
                && battleManager.IsPlayerSelfAttackTargetMode)
                return false;

            if (battleManager.IncomingAttackForceNoneElement)
                return false;
        }

        if (ReflectionRules.ShouldUseImmediateEffectReflectionFlow(incomingAttack))
            return false;

        return ReflectionRules.CanReflectPhysical(incomingAttack);
    }
}
