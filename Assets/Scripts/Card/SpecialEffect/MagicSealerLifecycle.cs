using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Magic Sealer (魔力封印の呪印): destroy every card in the victim's MagicPool regardless of remaining uses.
/// </summary>
public static class MagicSealerLifecycle
{
    private const int OnlineEffectTimeoutMs = 20000;

    public static async Task RunAsync(
        BattleManager bm,
        PlayerStatus user,
        PlayerStatus effectTarget,
        CancellationToken ct)
    {
        if (bm == null || user == null || effectTarget == null || MagicPoolManager.I == null)
            return;

        bool victimIsLocalPlayer = ReferenceEquals(effectTarget, bm.GetPlayerStatus());
        PlayerType victimPoolOwner = victimIsLocalPlayer ? PlayerType.Player : PlayerType.Enemy;
        Side displaySide = victimIsLocalPlayer ? Side.Player : Side.Enemy;

        MagicSealerEffectPlan plan;
        int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded + bm.SummonTurnCounters.EnemyOwnTurnsEnded;
        if (bm.IsOnlineMatch)
        {
            if (OnlineMatchContext.IsHost)
            {
                plan = BuildPlan(victimPoolOwner);
                NetworkBattleBridge.SendMagicSealerEffect(turnTag, new NetworkBattleBridge.MagicSealerEffectSync
                {
                    VictimIsHostPlayer = ResolveVictimIsHostPlayer(bm, effectTarget),
                    NoTarget = plan.NoTarget,
                    DestroyedCardNames = plan.CardNames,
                });
            }
            else
            {
                var sync = await NetworkBattleBridge.WaitForMagicSealerEffectAsync(
                    turnTag, ct, OnlineEffectTimeoutMs);
                plan = PlanFromSync(bm, sync);
            }
        }
        else
        {
            plan = BuildPlan(victimPoolOwner);
        }

        if (ct.IsCancellationRequested) return;

        await CardDestroyPresentation.PlayMagicPoolDestroySequenceAsync(
            effectTarget,
            displaySide,
            plan.Cards,
            plan.NoTarget,
            ct);

        if (ct.IsCancellationRequested) return;

        MagicPoolManager.I.ClearPool(victimPoolOwner);
        BattleUIManager.I?.RefreshMagicCardInteractivity(bm.playerHand);
    }

    private static MagicSealerEffectPlan BuildPlan(PlayerType victimPoolOwner)
    {
        var plan = new MagicSealerEffectPlan();
        var entries = MagicPoolManager.I.GetPoolEntries(victimPoolOwner);
        for (int i = 0; i < entries.Count; i++)
        {
            var card = entries[i]?.cardData;
            if (card == null) continue;
            plan.Cards.Add(card);
            plan.CardNames.Add(card.cardName);
        }

        plan.NoTarget = plan.Cards.Count == 0;
        return plan;
    }

    private static MagicSealerEffectPlan PlanFromSync(BattleManager bm, NetworkBattleBridge.MagicSealerEffectSync sync)
    {
        var plan = new MagicSealerEffectPlan
        {
            NoTarget = sync.NoTarget,
            CardNames = sync.DestroyedCardNames ?? new List<string>(),
        };

        if (plan.NoTarget || plan.CardNames.Count == 0)
            return plan;

        bool victimIsLocalPlayer = ResolveVictimIsLocalPlayer(bm, sync.VictimIsHostPlayer);
        PlayerType victimPoolOwner = victimIsLocalPlayer ? PlayerType.Player : PlayerType.Enemy;
        var entries = MagicPoolManager.I.GetPoolEntries(victimPoolOwner);

        for (int i = 0; i < plan.CardNames.Count; i++)
        {
            string name = plan.CardNames[i];
            if (string.IsNullOrEmpty(name)) continue;

            CardData match = null;
            for (int j = 0; j < entries.Count; j++)
            {
                var c = entries[j]?.cardData;
                if (c != null && c.cardName == name)
                {
                    match = c;
                    break;
                }
            }

            if (match == null && bm.cardDealer != null)
                match = bm.cardDealer.FindTemplateByDisplayOrAssetName(name);

            if (match != null)
                plan.Cards.Add(match);
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

    private sealed class MagicSealerEffectPlan
    {
        public bool NoTarget;
        public List<CardData> Cards = new();
        public List<string> CardNames = new();
    }
}
