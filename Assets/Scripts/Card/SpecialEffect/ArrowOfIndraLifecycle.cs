using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Arrow of Indra (インドラの矢): destroy up to 3 random cards from the victim's hand.
/// </summary>
public static class ArrowOfIndraLifecycle
{
    private const int OnlineEffectTimeoutMs = 20000;

    public static async Task RunAsync(
        BattleManager bm,
        CardData arrowCard,
        PlayerStatus user,
        PlayerStatus effectTarget,
        CancellationToken ct)
    {
        if (bm == null || user == null || effectTarget == null)
            return;

        bool victimIsLocalPlayer = ReferenceEquals(effectTarget, bm.GetPlayerStatus());
        PlayerType handOwner = victimIsLocalPlayer ? PlayerType.Player : PlayerType.Enemy;
        List<CardData> victimHand = victimIsLocalPlayer ? bm.playerHand : bm.cpuHand;
        Side victimDisplaySide = victimIsLocalPlayer ? Side.Player : Side.Enemy;
        Side arrowDisplaySide = ReferenceEquals(user, bm.GetPlayerStatus()) ? Side.Player : Side.Enemy;

        ArrowOfIndraEffectPlan plan;
        int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded + bm.SummonTurnCounters.EnemyOwnTurnsEnded;
        if (bm.IsOnlineMatch)
        {
            if (OnlineMatchContext.IsHost)
            {
                plan = BuildPlan(victimHand, handOwner);
                NetworkBattleBridge.SendArrowOfIndraEffect(turnTag, new NetworkBattleBridge.ArrowOfIndraEffectSync
                {
                    VictimIsHostPlayer = ResolveVictimIsHostPlayer(bm, effectTarget),
                    NoTarget = plan.NoTarget,
                    CardNames = plan.CardNames,
                    HandIndices = plan.HandIndices,
                });
            }
            else
            {
                var sync = await NetworkBattleBridge.WaitForArrowOfIndraEffectAsync(
                    turnTag, ct, OnlineEffectTimeoutMs);
                plan = PlanFromSync(bm, sync);
            }
        }
        else
        {
            plan = BuildPlan(victimHand, handOwner);
        }

        if (ct.IsCancellationRequested) return;

        await ArrowOfIndraPresentation.PlayAsync(
            arrowCard,
            arrowDisplaySide,
            effectTarget,
            victimDisplaySide,
            effectTarget,
            plan.Cards,
            plan.NoTarget,
            ct);

        if (ct.IsCancellationRequested || plan.NoTarget) return;

        for (int i = 0; i < plan.Cards.Count; i++)
        {
            CardDestroyPresentation.RemoveFromHand(
                bm,
                victimHand,
                plan.Cards[i],
                victimIsLocalPlayer);
        }
    }

    private static ArrowOfIndraEffectPlan BuildPlan(List<CardData> victimHand, PlayerType handOwner)
    {
        var plan = new ArrowOfIndraEffectPlan();
        var picks = HandDestroyRules.PickRandomDestroyableCards(
            victimHand,
            handOwner,
            ArrowOfIndraRules.MaxDestroyCount);

        for (int i = 0; i < picks.Count; i++)
        {
            var pick = picks[i];
            if (pick.Card == null) continue;
            plan.Cards.Add(pick.Card);
            plan.CardNames.Add(pick.Card.cardName);
            plan.HandIndices.Add(pick.HandIndex);
        }

        plan.NoTarget = plan.Cards.Count == 0;
        return plan;
    }

    private static ArrowOfIndraEffectPlan PlanFromSync(
        BattleManager bm,
        NetworkBattleBridge.ArrowOfIndraEffectSync sync)
    {
        var plan = new ArrowOfIndraEffectPlan
        {
            NoTarget = sync.NoTarget,
            CardNames = sync.CardNames ?? new List<string>(),
            HandIndices = sync.HandIndices ?? new List<int>(),
        };

        if (plan.NoTarget || plan.CardNames.Count == 0)
            return plan;

        bool victimIsLocalPlayer = ResolveVictimIsLocalPlayer(bm, sync.VictimIsHostPlayer);
        var hand = victimIsLocalPlayer ? bm.playerHand : bm.cpuHand;

        for (int i = 0; i < plan.CardNames.Count; i++)
        {
            string name = plan.CardNames[i];
            int handIndex = i < plan.HandIndices.Count ? plan.HandIndices[i] : -1;
            var card = CardDestroyPresentation.ResolveTargetCard(hand, name, handIndex);
            if (card == null && bm.cardDealer != null)
                card = bm.cardDealer.FindTemplateByDisplayOrAssetName(name);
            if (card != null)
                plan.Cards.Add(card);
        }

        if (plan.Cards.Count == 0)
            plan.NoTarget = true;

        return plan;
    }

    private static bool ResolveVictimIsHostPlayer(BattleManager bm, PlayerStatus victim)
    {
        if (!OnlineMatchContext.IsOnline)
            return ReferenceEquals(victim, bm.GetPlayerStatus());

        return OnlineMatchContext.IsHost
            ? ReferenceEquals(victim, bm.GetPlayerStatus())
            : ReferenceEquals(victim, bm.GetEnemyStatus());
    }

    private static bool ResolveVictimIsLocalPlayer(BattleManager bm, bool victimIsHostPlayer)
    {
        if (!OnlineMatchContext.IsOnline)
            return victimIsHostPlayer;

        return victimIsHostPlayer == OnlineMatchContext.IsHost;
    }

    private sealed class ArrowOfIndraEffectPlan
    {
        public bool NoTarget;
        public List<CardData> Cards = new();
        public List<string> CardNames = new();
        public List<int> HandIndices = new();
    }
}
