using UnityEngine;

/// <summary>
/// Bahamut summon identification, Maelstrom passive turn threshold, and Kannaduki grant.
/// </summary>
public static class BahamutRules
{
    public const int MaelstromUnlockTurn = 50;

    public static bool IsBahamut(SummonData summon)
    {
        if (summon == null) return false;
        return summon.name == "Bahamut" || summon.StableSummonId == "bahamut";
    }

    public static bool ShouldPlayMaelstromAwakeningAtStandBy(
        PlayerType turnOwner,
        PlayerStatus player,
        PlayerStatus enemy,
        SummonTurnCounterState counters)
    {
        if (counters == null || counters.CurrentBattleTurnDisplay < MaelstromUnlockTurn)
            return false;

        var owner = turnOwner == PlayerType.Player ? player : enemy;
        if (owner == null || owner.summonData == null || !IsBahamut(owner.summonData))
            return false;
        if (owner.HasKannadukiEffect()) return false;
        if (owner.HasCurseBindEffect()) return false;
        return true;
    }

    public static PlayerStatus ResolveTurnOwner(
        PlayerType turnOwner,
        PlayerStatus player,
        PlayerStatus enemy)
        => turnOwner == PlayerType.Player ? player : enemy;

    public static void GrantMaelstromKannaduki(PlayerStatus target)
    {
        if (target == null || target.summonData == null || !IsBahamut(target.summonData))
            return;
        if (target.HasCurseBindEffect()) return;
        if (target.HasKannadukiEffect()) return;

        var config = StatusProgressionConfig.GetRuntimeFallback();
        target.TryApplyStatusEffect(
            StatusEffectType.Kannaduki,
            config,
            suppressGrantPopupAndSound: true);
    }
}
