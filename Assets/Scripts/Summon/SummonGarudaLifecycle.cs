using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Garuda: opening hand +2, own turn end at 5/10/15… draws up to 2 cards (cap 18).
/// Turn-end orchestration: <see cref="SummonTurnEndLifecycle"/>.
/// </summary>
public static class SummonGarudaLifecycle
{
    public const int DefaultOpeningHand = 10;
    public const int GarudaOpeningHand = 12;

    public const string TurnEndBonusMessage = "風が手札を増やす";
    public const string TurnEndBonusSeAddress = "Assets/SE/ジャンプ.mp3";

    public static void GetOpeningHandTargets(PlayerStatus playerStatus, PlayerStatus enemyStatus, out int playerCards, out int cpuCards)
    {
        bool pGaruda = playerStatus?.summonData != null && playerStatus.summonData.IsGarudaLifecycle();
        bool eGaruda = enemyStatus?.summonData != null && enemyStatus.summonData.IsGarudaLifecycle();
        playerCards = pGaruda ? GarudaOpeningHand : DefaultOpeningHand;
        cpuCards = eGaruda ? GarudaOpeningHand : DefaultOpeningHand;
    }

    /// <summary>
    /// Host/offline: decide which cards to draw (RNG). Does not mutate hand.
    /// </summary>
    public static List<CardData> ComputeTurnEndDrawPlan(
        BattleManager bm,
        PlayerStatus owner,
        List<CardData> hand,
        bool isPlayerHand)
    {
        var result = new List<CardData>();
        if (bm == null || owner == null || hand == null) return result;

        int cap = isPlayerHand ? bm.GetHandMaxCount() : bm.GetEnemyHandCapacity();
        int space = Mathf.Max(0, cap - hand.Count);
        int toDraw = Mathf.Min(2, space);
        if (toDraw <= 0) return result;

        for (int i = 0; i < toDraw; i++)
        {
            CardData card = isPlayerHand
                ? bm.cardDealer?.DrawRandomCard(PlayerType.Player)
                : bm.cardDealer?.DrawRandomCard(PlayerType.Enemy);
            if (card != null)
                result.Add(card);
        }

        return result;
    }

    /// <summary>
    /// Instantiate drawn cards from synced names (online client).
    /// </summary>
    public static List<CardData> InstantiateDrawPlanFromNames(BattleManager bm, IReadOnlyList<string> names)
    {
        var result = new List<CardData>();
        if (bm?.cardDealer == null || names == null) return result;

        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var template = bm.cardDealer.FindTemplateByName(name);
            if (template == null)
            {
                Debug.LogWarning($"[SummonGarudaLifecycle] Template not found: {name}");
                continue;
            }

            var instance = bm.cardDealer.InstantiateCardFromTemplate(template);
            if (instance != null)
                result.Add(instance);
        }

        return result;
    }

    public static async Task RunTurnEndDrawSequenceAsync(
        BattleManager bm,
        PlayerStatus owner,
        List<CardData> hand,
        bool isPlayerHand,
        IReadOnlyList<CardData> drawPlan,
        CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || owner == null || drawPlan == null || drawPlan.Count == 0) return;

        Color messageColor = new Color(0.5f, 0.92f, 0.72f);
        SoundEffectPlayer.I?.Play(TurnEndBonusSeAddress);
        float fadeSec = ui.ShowMessagePopupForTarget(owner, TurnEndBonusMessage, messageColor);
        await DamagePopup.WaitAfterPopupLifetimeAsync(fadeSec, ct);
        if (ct.IsCancellationRequested) return;

        var drawn = new List<CardData>();
        var refill = bm.HandRefill;

        foreach (var card in drawPlan)
        {
            if (ct.IsCancellationRequested) return;
            if (card == null) continue;

            int cap = isPlayerHand ? bm.GetHandMaxCount() : bm.GetEnemyHandCapacity();
            if (hand.Count >= cap) break;

            hand.Add(card);
            drawn.Add(card);

            if (isPlayerHand)
            {
                bm.cardDealer?.CreateCardUIForHand(card);
            }
        }

        if (isPlayerHand && refill != null)
        {
            foreach (var card in drawn)
            {
                if (ct.IsCancellationRequested) return;
                await refill.RevealDrawnCardAfterCombatAsync(card, ct);
            }
        }
    }
}
