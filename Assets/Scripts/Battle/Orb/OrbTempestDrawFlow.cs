using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 暴風の宝玉：第1段通過ダメージ後、防御側の手札を1枚ドロー（上限時は無処理）。
/// 演出・文言は <see cref="SummonGarudaLifecycle"/> に合わせる。
/// </summary>
public static class OrbTempestDrawFlow
{
    private const int OnlineEffectTimeoutMs = 20000;

    public static async Task RunAsync(BattleManager bm, PlayerStatus defender, CancellationToken ct)
    {
        if (bm == null || defender == null) return;

        bool isPlayerHand = ReferenceEquals(defender, bm.GetPlayerStatus());
        List<CardData> hand = isPlayerHand ? bm.playerHand : bm.cpuHand;
        if (hand == null) return;

        int cap = isPlayerHand ? bm.GetHandMaxCount() : bm.GetEnemyHandCapacity();
        if (hand.Count >= cap) return;

        PlayerType drawSide = isPlayerHand ? PlayerType.Player : PlayerType.Enemy;
        int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded + bm.SummonTurnCounters.EnemyOwnTurnsEnded;

        List<CardData> drawPlan;
        if (bm.IsOnlineMatch)
        {
            if (OnlineMatchContext.IsHost)
            {
                drawPlan = BuildDrawPlan(bm, hand, drawSide, cap);
                bool skipped = drawPlan.Count == 0;
                NetworkBattleBridge.SendOrbTempestDrawEffect(turnTag, new NetworkBattleBridge.OrbTempestDrawEffectSync
                {
                    DrawerIsHostPlayer = ResolveDrawerIsHostPlayer(bm, defender),
                    Skipped = skipped,
                    DrawnCardName = skipped || drawPlan[0] == null ? string.Empty : drawPlan[0].cardName,
                });
            }
            else
            {
                var sync = await NetworkBattleBridge.WaitForOrbTempestDrawEffectAsync(
                    turnTag, ct, OnlineEffectTimeoutMs);
                if (sync.Skipped || string.IsNullOrEmpty(sync.DrawnCardName))
                    return;
                drawPlan = SummonGarudaLifecycle.InstantiateDrawPlanFromNames(
                    bm, new List<string> { sync.DrawnCardName });
            }
        }
        else
        {
            drawPlan = BuildDrawPlan(bm, hand, drawSide, cap);
        }

        if (drawPlan == null || drawPlan.Count == 0) return;

        await SummonGarudaLifecycle.RunTurnEndDrawSequenceAsync(
            bm, defender, hand, isPlayerHand, drawPlan, ct);
    }

    private static List<CardData> BuildDrawPlan(
        BattleManager bm,
        List<CardData> hand,
        PlayerType drawSide,
        int cap)
    {
        var result = new List<CardData>();
        if (bm == null || hand == null || hand.Count >= cap) return result;

        CardData card = bm.cardDealer?.DrawRandomCard(drawSide);
        if (card != null)
            result.Add(card);
        return result;
    }

    private static bool ResolveDrawerIsHostPlayer(BattleManager bm, PlayerStatus drawer)
    {
        if (!OnlineMatchContext.IsOnline)
            return ReferenceEquals(drawer, bm.GetPlayerStatus());

        return OnlineMatchContext.IsHost
            ? ReferenceEquals(drawer, bm.GetPlayerStatus())
            : ReferenceEquals(drawer, bm.GetEnemyStatus());
    }
}
