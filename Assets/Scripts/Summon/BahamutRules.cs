using UnityEngine;

/// <summary>
/// Bahamut passive (Mega Flare) and popup eligibility rules.
/// </summary>
public static class BahamutRules
{
    public const int MegaFlareUnlockTurn = 50;

    private static CardData _megaFlareTemplate;
    private static CardData _gigaFlareTemplate;

    public static bool IsBahamut(SummonData summon)
    {
        if (summon == null) return false;
        return summon.name == "Bahamut" || summon.StableSummonId == "bahamut";
    }

    public static CardData GetMegaFlareTemplate()
    {
        if (_megaFlareTemplate == null)
            _megaFlareTemplate = Resources.Load<CardData>("Cards/06_ULTIMATE/MegaFlare");
        return _megaFlareTemplate;
    }

    public static CardData GetGigaFlareTemplate()
    {
        if (_gigaFlareTemplate == null)
            _gigaFlareTemplate = Resources.Load<CardData>("Cards/06_ULTIMATE/GigaFlare");
        return _gigaFlareTemplate;
    }

    public static bool MeetsCommonAttackSelectGate(
        PlayerStatus summoner,
        GameState state,
        PlayerType turnOwner,
        PlayerType summonerSide)
    {
        if (summoner == null) return false;
        if (!IsBahamut(summoner.summonData)) return false;
        if (summoner.HasCurseBindEffect()) return false;
        if (summoner.HasFreezeEffect()) return false;
        if (summoner.IsCastingArchMagic) return false;
        if (state != GameState.AttackPhase) return false;
        return turnOwner == summonerSide;
    }

    public static bool CanUseMegaFlare(
        PlayerStatus summoner,
        SummonTurnCounterState counters,
        GameState state,
        PlayerType turnOwner,
        PlayerType summonerSide)
    {
        if (!MeetsCommonAttackSelectGate(summoner, state, turnOwner, summonerSide))
            return false;
        if (summoner.hasUsedMegaFlare) return false;
        if (counters == null || counters.CurrentBattleTurnDisplay < MegaFlareUnlockTurn)
            return false;
        return GetMegaFlareTemplate() != null;
    }

    public static bool CanUseGigaFlare(
        PlayerStatus summoner,
        GameState state,
        PlayerType turnOwner,
        PlayerType summonerSide)
    {
        if (!MeetsCommonAttackSelectGate(summoner, state, turnOwner, summonerSide))
            return false;
        if (summoner.hasUsedUltimateSkill) return false;
        if (!DisadvantageRules.IsDisadvantaged(summoner)) return false;
        var card = summoner.summonData != null ? summoner.summonData.ultimateSkillCard : null;
        if (card == null)
            card = GetGigaFlareTemplate();
        return card != null;
    }

    public static bool CanOpenBahamutPopup(
        PlayerStatus summoner,
        SummonTurnCounterState counters,
        GameState state,
        PlayerType turnOwner,
        PlayerType summonerSide)
    {
        return CanUseMegaFlare(summoner, counters, state, turnOwner, summonerSide)
            || CanUseGigaFlare(summoner, state, turnOwner, summonerSide);
    }

    public static bool ShouldEnemyUseMegaFlareNow(
        PlayerStatus enemy,
        SummonTurnCounterState counters,
        GameState state,
        PlayerType turnOwner)
    {
        return CanUseMegaFlare(enemy, counters, state, turnOwner, PlayerType.Enemy);
    }
}
