using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Turn-end summon passives (Garuda draw, Indra hand destroy). Counter increment and online sync live here.
/// </summary>
public static class SummonTurnEndLifecycle
{
    private const int OnlineSummonEffectTimeoutMs = 20000;

    private sealed class BuiltTurnEndPack
    {
        public List<SummonTurnEndEffectEntry> Effects = new();
        public List<List<CardData>> GarudaDrawPlans = new();
    }

    public static async Task ProcessTurnEndAsync(BattleManager bm, SummonTurnCounterState ctr, CancellationToken ct)
    {
        if (bm == null || ctr == null) return;
        if (ct.IsCancellationRequested || bm.CurrentState != GameState.EndPhase) return;

        bool turnOwnerIsPlayer = bm.CurrentTurnOwner == PlayerType.Player;
        if (turnOwnerIsPlayer)
            ctr.PlayerOwnTurnsEnded++;
        else
            ctr.EnemyOwnTurnsEnded++;

        int ownTurnsEnded = turnOwnerIsPlayer ? ctr.PlayerOwnTurnsEnded : ctr.EnemyOwnTurnsEnded;
        if (ownTurnsEnded % 5 != 0) return;

        int turnTag = ctr.PlayerOwnTurnsEnded + ctr.EnemyOwnTurnsEnded;
        var built = BuildEffects(bm, turnOwnerIsPlayer);

        if (bm.IsOnlineMatch)
        {
            if (OnlineMatchContext.IsHost)
                NetworkBattleBridge.SendSummonTurnEndEffects(turnTag, built.Effects);
            else
                built = await WaitRemoteBuiltPackAsync(turnTag, ct);
        }

        await PlayEffectsAsync(bm, turnOwnerIsPlayer, built, ct);

        if (ct.IsCancellationRequested) return;
        BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
        BattleUIManager.I?.RefreshMagicCardInteractivity(bm.playerHand);
    }

    private static async Task<BuiltTurnEndPack> WaitRemoteBuiltPackAsync(int turnTag, CancellationToken ct)
    {
        var effects = await NetworkBattleBridge.WaitForSummonTurnEndEffectsAsync(
            turnTag, ct, OnlineSummonEffectTimeoutMs);
        return new BuiltTurnEndPack { Effects = effects ?? new List<SummonTurnEndEffectEntry>() };
    }

    private static BuiltTurnEndPack BuildEffects(BattleManager bm, bool turnOwnerIsPlayer)
    {
        var pack = new BuiltTurnEndPack();
        var owner = turnOwnerIsPlayer ? bm.GetPlayerStatus() : bm.GetEnemyStatus();
        var ownerHand = turnOwnerIsPlayer ? bm.playerHand : bm.cpuHand;
        var victimHand = turnOwnerIsPlayer ? bm.cpuHand : bm.playerHand;
        bool ownerIsHostPlayer = ResolveOwnerIsHostPlayer(turnOwnerIsPlayer);

        if (owner == null || owner.HasCurseBindEffect())
            return pack;

        var summon = owner.summonData;
        if (summon == null) return pack;

        if (summon.IsGarudaLifecycle())
        {
            var drawPlan = SummonGarudaLifecycle.ComputeTurnEndDrawPlan(bm, owner, ownerHand, turnOwnerIsPlayer);
            if (drawPlan.Count > 0)
            {
                var names = new List<string>(drawPlan.Count);
                foreach (var c in drawPlan)
                    names.Add(c != null ? c.cardName : "");

                pack.Effects.Add(new SummonTurnEndEffectEntry
                {
                    Kind = SummonTurnEndEffectKind.GarudaDraw,
                    OwnerIsHostPlayer = ownerIsHostPlayer,
                    DrawnCardNames = names,
                });
                pack.GarudaDrawPlans.Add(drawPlan);
            }
        }

        if (summon.IsIndraLifecycle())
        {
            var victimOwner = turnOwnerIsPlayer ? PlayerType.Enemy : PlayerType.Player;
            var pick = HandDestroyRules.PickRandomDestroyableCard(victimHand, victimOwner);
            pack.Effects.Add(new SummonTurnEndEffectEntry
            {
                Kind = pick != null ? SummonTurnEndEffectKind.IndraHandDestroy : SummonTurnEndEffectKind.IndraNoTarget,
                OwnerIsHostPlayer = ownerIsHostPlayer,
                CardName = pick != null ? pick.cardName : null,
                VictimHandIndex = pick != null && victimHand != null ? victimHand.IndexOf(pick) : -1,
            });
        }

        return pack;
    }

    private static async Task PlayEffectsAsync(
        BattleManager bm,
        bool turnOwnerIsPlayer,
        BuiltTurnEndPack pack,
        CancellationToken ct)
    {
        if (pack?.Effects == null || pack.Effects.Count == 0) return;

        SortEffectsForFirstPlayerPriority(bm, pack.Effects);

        int garudaPlanIndex = 0;
        foreach (var effect in pack.Effects)
        {
            if (ct.IsCancellationRequested) return;

            bool ownerIsPlayer = ResolveOwnerIsLocalPlayer(effect.OwnerIsHostPlayer);
            var owner = ownerIsPlayer ? bm.GetPlayerStatus() : bm.GetEnemyStatus();
            var ownerHand = ownerIsPlayer ? bm.playerHand : bm.cpuHand;
            bool victimIsPlayerHand = !ownerIsPlayer;
            var victim = ownerIsPlayer ? bm.GetEnemyStatus() : bm.GetPlayerStatus();
            var victimHand = ownerIsPlayer ? bm.cpuHand : bm.playerHand;

            switch (effect.Kind)
            {
                case SummonTurnEndEffectKind.GarudaDraw:
                {
                    List<CardData> drawPlan;
                    if (pack.GarudaDrawPlans != null
                        && garudaPlanIndex < pack.GarudaDrawPlans.Count
                        && pack.GarudaDrawPlans[garudaPlanIndex] != null
                        && pack.GarudaDrawPlans[garudaPlanIndex].Count > 0)
                    {
                        drawPlan = pack.GarudaDrawPlans[garudaPlanIndex];
                    }
                    else
                    {
                        drawPlan = SummonGarudaLifecycle.InstantiateDrawPlanFromNames(bm, effect.DrawnCardNames);
                    }

                    garudaPlanIndex++;
                    await SummonGarudaLifecycle.RunTurnEndDrawSequenceAsync(
                        bm, owner, ownerHand, ownerIsPlayer, drawPlan, ct);
                    break;
                }

                case SummonTurnEndEffectKind.IndraHandDestroy:
                case SummonTurnEndEffectKind.IndraNoTarget:
                {
                    bool noTarget = effect.Kind == SummonTurnEndEffectKind.IndraNoTarget;
                    CardData target = noTarget
                        ? null
                        : HandDestroyService.ResolveTargetCard(victimHand, effect.CardName, effect.VictimHandIndex);

                    await SummonIndraLifecycle.RunHandDestroySequenceAsync(
                        bm, owner, victim, victimHand, victimIsPlayerHand, target, noTarget || target == null, ct);
                    break;
                }
            }
        }
    }

    private static void SortEffectsForFirstPlayerPriority(BattleManager bm, List<SummonTurnEndEffectEntry> effects)
    {
        if (effects == null || effects.Count <= 1 || bm == null) return;

        bool firstTurnOwnerIsPlayer = bm.OpeningTurnOwner == PlayerType.Player;

        effects.Sort((a, b) =>
        {
            bool aFirst = ResolveOwnerIsLocalPlayer(a.OwnerIsHostPlayer) == firstTurnOwnerIsPlayer;
            bool bFirst = ResolveOwnerIsLocalPlayer(b.OwnerIsHostPlayer) == firstTurnOwnerIsPlayer;
            return bFirst.CompareTo(aFirst);
        });
    }

    private static bool ResolveOwnerIsHostPlayer(bool turnOwnerIsPlayer)
    {
        if (!OnlineMatchContext.IsOnline)
            return turnOwnerIsPlayer;

        if (OnlineMatchContext.IsHost)
            return turnOwnerIsPlayer;
        return !turnOwnerIsPlayer;
    }

    private static bool ResolveOwnerIsLocalPlayer(bool ownerIsHostPlayer)
    {
        if (!OnlineMatchContext.IsOnline)
            return ownerIsHostPlayer;

        return ownerIsHostPlayer == OnlineMatchContext.IsHost;
    }
}
