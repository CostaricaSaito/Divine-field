using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Magic Fountain (魔力の泉): add +4 remaining uses to every card in the target player's MagicPool.
/// </summary>
public static class MagicFountainLifecycle
{
    private const int OnlineEffectTimeoutMs = 20000;

    public static async Task RunAsync(
        BattleManager bm,
        CardData fountainCard,
        PlayerStatus user,
        PlayerStatus effectTarget,
        CancellationToken ct)
    {
        if (bm == null || user == null || effectTarget == null || MagicPoolManager.I == null)
            return;

        bool targetIsLocalPlayer = ReferenceEquals(effectTarget, bm.GetPlayerStatus());
        PlayerType poolOwner = targetIsLocalPlayer ? PlayerType.Player : PlayerType.Enemy;
        Side poolDisplaySide = targetIsLocalPlayer ? Side.Player : Side.Enemy;
        Side fountainDisplaySide = ReferenceEquals(user, bm.GetPlayerStatus()) ? Side.Player : Side.Enemy;

        MagicFountainEffectPlan plan;
        int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded + bm.SummonTurnCounters.EnemyOwnTurnsEnded;
        if (bm.IsOnlineMatch)
        {
            if (OnlineMatchContext.IsHost)
            {
                plan = BuildPlan(poolOwner);
                NetworkBattleBridge.SendMagicFountainEffect(turnTag, new NetworkBattleBridge.MagicFountainEffectSync
                {
                    TargetIsHostPlayer = ResolveTargetIsHostPlayer(bm, effectTarget),
                    NoTarget = plan.NoTarget,
                    CardNames = plan.CardNames,
                    StartUses = plan.StartUses,
                });
            }
            else
            {
                var sync = await NetworkBattleBridge.WaitForMagicFountainEffectAsync(
                    turnTag, ct, OnlineEffectTimeoutMs);
                plan = PlanFromSync(bm, sync);
            }
        }
        else
        {
            plan = BuildPlan(poolOwner);
        }

        if (ct.IsCancellationRequested) return;

        var snapshots = new List<MagicFountainPresentation.EntrySnapshot>(plan.Entries.Count);
        for (int i = 0; i < plan.Entries.Count; i++)
        {
            var e = plan.Entries[i];
            snapshots.Add(new MagicFountainPresentation.EntrySnapshot
            {
                Card = e.Card,
                StartUses = e.StartUses,
            });
        }

        await MagicFountainPresentation.PlayAsync(
            fountainCard,
            fountainDisplaySide,
            effectTarget,
            poolDisplaySide,
            effectTarget,
            snapshots,
            plan.NoTarget,
            ct);

        if (ct.IsCancellationRequested || plan.NoTarget) return;

        MagicPoolManager.I.AddRemainingUsesToAll(poolOwner, MagicFountainRules.UsesBonus);
        BattleUIManager.I?.RefreshMagicCardInteractivity(bm.playerHand);
    }

    private static MagicFountainEffectPlan BuildPlan(PlayerType poolOwner)
    {
        var plan = new MagicFountainEffectPlan();
        var entries = MagicPoolManager.I.GetPoolEntries(poolOwner);
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry?.cardData == null) continue;
            plan.Entries.Add(new MagicFountainPlanEntry
            {
                Card = entry.cardData,
                StartUses = entry.remainingUses,
            });
            plan.CardNames.Add(entry.cardData.cardName);
            plan.StartUses.Add(entry.remainingUses);
        }

        plan.NoTarget = plan.Entries.Count == 0;
        return plan;
    }

    private static MagicFountainEffectPlan PlanFromSync(
        BattleManager bm,
        NetworkBattleBridge.MagicFountainEffectSync sync)
    {
        var plan = new MagicFountainEffectPlan
        {
            NoTarget = sync.NoTarget,
            CardNames = sync.CardNames ?? new List<string>(),
            StartUses = sync.StartUses ?? new List<int>(),
        };

        if (plan.NoTarget || plan.CardNames.Count == 0)
            return plan;

        bool targetIsLocalPlayer = ResolveTargetIsLocalPlayer(bm, sync.TargetIsHostPlayer);
        PlayerType poolOwner = targetIsLocalPlayer ? PlayerType.Player : PlayerType.Enemy;
        var poolEntries = MagicPoolManager.I.GetPoolEntries(poolOwner);

        for (int i = 0; i < plan.CardNames.Count; i++)
        {
            string name = plan.CardNames[i];
            if (string.IsNullOrEmpty(name)) continue;

            int startUses = i < plan.StartUses.Count ? plan.StartUses[i] : 0;
            CardData match = null;
            for (int j = 0; j < poolEntries.Count; j++)
            {
                var c = poolEntries[j]?.cardData;
                if (c != null && c.cardName == name)
                {
                    match = c;
                    if (startUses <= 0)
                        startUses = poolEntries[j].remainingUses;
                    break;
                }
            }

            if (match == null && bm.cardDealer != null)
                match = bm.cardDealer.FindTemplateByDisplayOrAssetName(name);

            if (match == null) continue;

            plan.Entries.Add(new MagicFountainPlanEntry
            {
                Card = match,
                StartUses = startUses,
            });
        }

        if (plan.Entries.Count == 0)
            plan.NoTarget = true;

        return plan;
    }

    private static bool ResolveTargetIsHostPlayer(BattleManager bm, PlayerStatus target)
    {
        if (!OnlineMatchContext.IsOnline)
            return ReferenceEquals(target, bm.GetPlayerStatus());

        return OnlineMatchContext.IsHost
            ? ReferenceEquals(target, bm.GetPlayerStatus())
            : ReferenceEquals(target, bm.GetEnemyStatus());
    }

    private static bool ResolveTargetIsLocalPlayer(BattleManager bm, bool targetIsHostPlayer)
    {
        if (!OnlineMatchContext.IsOnline)
            return targetIsHostPlayer;

        return targetIsHostPlayer == OnlineMatchContext.IsHost;
    }

    private sealed class MagicFountainPlanEntry
    {
        public CardData Card;
        public int StartUses;
    }

    private sealed class MagicFountainEffectPlan
    {
        public bool NoTarget;
        public List<MagicFountainPlanEntry> Entries = new();
        public List<string> CardNames = new();
        public List<int> StartUses = new();
    }
}
