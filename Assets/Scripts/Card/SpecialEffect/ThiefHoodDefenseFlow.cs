using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Thief's Hood: after combat damage, copy random cards from the incoming attack combo.
/// </summary>
public static class ThiefHoodDefenseFlow
{
    private const string StealSePath = "Assets/SE/盗む.mp3";

    public static async Task TryRunAfterCombatDamageAsync(
        BattleProcessor processor,
        IReadOnlyList<CardData> attackCards,
        IReadOnlyList<CardData> defenseCards,
        PlayerStatus attacker,
        PlayerStatus defender,
        CancellationToken cancellationToken = default)
    {
        if (processor == null || attackCards == null || attackCards.Count == 0
            || defender == null || attacker == null)
            return;

        int hoodCount = ThiefHoodRules.CountThiefHoods(defenseCards);
        if (hoodCount <= 0) return;

        var stealablePool = ThiefHoodRules.CollectStealableAttackCards(attackCards);
        if (stealablePool.Count == 0) return;

        var bm = BattleManager.I;
        if (bm == null) return;

        ThiefHoodStealPlan plan;
        if (bm.IsOnlineMatch)
        {
            int turnTag = bm.SummonTurnCounters.PlayerOwnTurnsEnded
                + bm.SummonTurnCounters.EnemyOwnTurnsEnded;
            if (OnlineMatchContext.IsHost)
            {
                plan = BuildPlan(stealablePool, hoodCount, defender, bm);
                NetworkBattleBridge.SendThiefHoodEffect(turnTag, ToSync(plan, defender, bm));
            }
            else
            {
                var sync = await NetworkBattleBridge.WaitForThiefHoodEffectAsync(
                    turnTag, cancellationToken);
                plan = PlanFromSync(sync);
            }
        }
        else
        {
            plan = BuildPlan(stealablePool, hoodCount, defender, bm);
        }

        if (plan.StolenTemplateNames == null || plan.StolenTemplateNames.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        Side defenderSide = plan.DefenderIsLocalPlayer ? Side.Player : Side.Enemy;

        await ThiefHoodPresentation.RunHoodSheetGlowAsync(defenseCards, cancellationToken);
        BattleUIManager.I?.ClearAllCardDisplaysAndSelectionImmediate();
        bm.ClearStatsDisplaySequenceCards();

        float fadeSec = 0f;
        if (BattleUIManager.I != null)
            fadeSec = BattleUIManager.I.ShowStyledMessagePopup(defender, MessagePopupKind.ThiefHoodStoleCard);

        SoundEffectPlayer.I?.Play(StealSePath);

        if (fadeSec > 0f)
            await MessagePopup.WaitAfterPopupLifetimeAsync(fadeSec, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var stolenInstances = BuildStolenInstances(plan, bm);
        if (stolenInstances.Count == 0) return;

        await ThiefHoodPresentation.ShowStolenCardsOnDisplayAsync(
            stolenInstances, defenderSide, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        await ApplyStealPlanAsync(plan, stolenInstances, bm, cancellationToken);
    }

    private static List<CardData> BuildStolenInstances(ThiefHoodStealPlan plan, BattleManager bm)
    {
        var instances = new List<CardData>();
        if (plan.StolenTemplateNames == null || bm?.cardDealer == null) return instances;

        for (int i = 0; i < plan.StolenTemplateNames.Count; i++)
        {
            string templateName = plan.StolenTemplateNames[i];
            var template = bm.cardDealer.FindTemplateByDisplayOrAssetName(templateName);
            if (template == null)
            {
                Debug.LogWarning($"[ThiefHoodDefenseFlow] Template not found: {templateName}");
                continue;
            }

            var instance = bm.cardDealer.InstantiateCardFromTemplate(template);
            if (instance != null)
                instances.Add(instance);
        }

        return instances;
    }

    private static ThiefHoodStealPlan BuildPlan(
        IReadOnlyList<CardData> stealablePool,
        int hoodCount,
        PlayerStatus defender,
        BattleManager bm)
    {
        var plan = new ThiefHoodStealPlan
        {
            DefenderIsLocalPlayer = ReferenceEquals(defender, bm.GetPlayerStatus()),
            StolenTemplateNames = ThiefHoodRules.PickStealTemplateNames(stealablePool, hoodCount),
        };
        return plan;
    }

    private static NetworkBattleBridge.ThiefHoodEffectSync ToSync(
        ThiefHoodStealPlan plan,
        PlayerStatus defender,
        BattleManager bm)
    {
        bool defenderIsHostPlayer = OnlineMatchContext.IsHost
            ? ReferenceEquals(defender, bm.GetPlayerStatus())
            : ReferenceEquals(defender, bm.GetEnemyStatus());

        return new NetworkBattleBridge.ThiefHoodEffectSync
        {
            NoEffect = plan.StolenTemplateNames == null || plan.StolenTemplateNames.Count == 0,
            DefenderIsHostPlayer = defenderIsHostPlayer,
            StolenTemplateNames = plan.StolenTemplateNames != null
                ? new List<string>(plan.StolenTemplateNames)
                : new List<string>(),
        };
    }

    private static ThiefHoodStealPlan PlanFromSync(NetworkBattleBridge.ThiefHoodEffectSync sync)
    {
        if (sync.NoEffect || sync.StolenTemplateNames == null || sync.StolenTemplateNames.Count == 0)
            return default;

        bool defenderIsLocalPlayer = OnlineMatchContext.IsHost
            ? sync.DefenderIsHostPlayer
            : !sync.DefenderIsHostPlayer;

        return new ThiefHoodStealPlan
        {
            DefenderIsLocalPlayer = defenderIsLocalPlayer,
            StolenTemplateNames = new List<string>(sync.StolenTemplateNames),
        };
    }

    private static async Task ApplyStealPlanAsync(
        ThiefHoodStealPlan plan,
        List<CardData> instances,
        BattleManager bm,
        CancellationToken cancellationToken)
    {
        if (instances == null || instances.Count == 0) return;

        var hand = plan.DefenderIsLocalPlayer ? bm.playerHand : bm.cpuHand;
        if (hand == null) return;

        var added = new List<CardData>();
        for (int i = 0; i < instances.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var instance = instances[i];
            if (instance == null) continue;

            hand.Add(instance);
            added.Add(instance);

            if (plan.DefenderIsLocalPlayer && bm.cardDealer != null)
                bm.cardDealer.CreateCardUIForHand(instance);
        }

        if (added.Count == 0) return;

        int removed = HandCapacityRules.TrimOverflowRandom(
            hand,
            BattleManager.MaxHandCards,
            destroyPlayerUi: plan.DefenderIsLocalPlayer);

        if (removed > 0)
        {
            Debug.Log($"[ThiefHoodDefenseFlow] Trimmed {removed} overflow card(s) from hand (count={hand.Count})");
        }

        if (plan.DefenderIsLocalPlayer && bm.HandRefill != null)
        {
            for (int i = 0; i < added.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var card = added[i];
                if (card?.cardUI == null) continue;
                await bm.HandRefill.RevealDrawnCardAfterCombatAsync(card, cancellationToken);
            }

            BattleUIManager.I?.RefreshMagicCardInteractivity(bm.playerHand);
            bm.UpdateTotalATKDEFDisplay();
            BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
            bm.RefreshPlayerDefensePhaseInteractivity();
        }

        await Task.Yield();
    }

    private struct ThiefHoodStealPlan
    {
        public bool DefenderIsLocalPlayer;
        public List<string> StolenTemplateNames;
    }
}
