using UnityEngine;

/// <summary>
/// CPU emergency exchange: raise HP to 30 when HP&lt;=10 (GP→MP→HP priority, same transfer rules as <see cref="ExchangePopupUI"/>).
/// </summary>
public static class EnemyCpuExchangePlanner
{
    private const int TargetHp = 30;
    private const int TriggerHpMax = 10;

    public static bool CanEmergencyExchange(PlayerStatus status)
    {
        if (status == null) return false;
        if (status.currentHP > TriggerHpMax) return false;
        if (status.currentHP >= TargetHp) return false;
        return CanEventuallyRaiseHp(status);
    }

    public static void ApplyEmergencyExchange(PlayerStatus status)
    {
        if (status == null) return;
        if (status.currentHP > TriggerHpMax) return;

        int guard = 0;
        while (status.currentHP < TargetHp && guard++ < 500)
        {
            if (TryRaiseHpByOne(status))
                continue;
            if (TryConvertGpToMp(status))
                continue;
            break;
        }

        Debug.Log($"[EnemyCpuExchange] Emergency exchange done HP={status.currentHP} MP={status.currentMP} GP={status.currentGP}");
    }

    private static bool CanEventuallyRaiseHp(PlayerStatus status)
    {
        if (status.currentMP > 0 && status.currentHP < status.maxHP) return true;
        if (status.currentGP > 0 && status.currentMP < status.maxMP) return true;
        if (status.currentGP > 0 && status.currentMP == 0 && status.currentHP > 0) return true;
        return false;
    }

    private static bool TryRaiseHpByOne(PlayerStatus status)
    {
        if (status.currentHP >= TargetHp) return false;
        if (status.currentMP <= 0) return false;
        if (status.currentHP >= status.maxHP) return false;
        status.currentHP++;
        status.currentMP--;
        return true;
    }

    private static bool TryConvertGpToMp(PlayerStatus status)
    {
        if (status.currentGP <= 0) return false;
        if (status.currentMP >= status.maxMP) return false;
        status.currentGP--;
        status.currentMP++;
        return true;
    }
}
