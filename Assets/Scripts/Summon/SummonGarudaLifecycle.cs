using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ガルーダ：開幕手札 +2（合計12枚側）、自分のターン終了時に 5,10,15… 回目で最大2枚ドロー（手札上限18まで）。
/// 将来的にインドラ等のターン起因加護を足す場合は、共通の Summon ライフサイクル層へ寄せる想定。
/// </summary>
public static class SummonGarudaLifecycle
{
    public const int DefaultOpeningHand = 10;
    public const int GarudaOpeningHand = 12;

    /// <summary>5n ターン終了ドロー前のメッセージ（DamagePopup）。</summary>
    public const string TurnEndBonusMessage = "風が手札を増やす";

    /// <summary>上記メッセージ表示時の SE（Addressables: <c>Assets/SE/ジャンプ.mp3</c>）。</summary>
    public const string TurnEndBonusSeAddress = "Assets/SE/ジャンプ.mp3";

    /// <summary>
    /// 開幕配布枚数。ガルーダを持つ側だけ 12、それ以外は 10。
    /// </summary>
    public static void GetOpeningHandTargets(PlayerStatus playerStatus, PlayerStatus enemyStatus, out int playerCards, out int cpuCards)
    {
        bool pGaruda = playerStatus?.summonData != null && playerStatus.summonData.IsGarudaLifecycle();
        bool eGaruda = enemyStatus?.summonData != null && enemyStatus.summonData.IsGarudaLifecycle();
        playerCards = pGaruda ? GarudaOpeningHand : DefaultOpeningHand;
        cpuCards = eGaruda ? GarudaOpeningHand : DefaultOpeningHand;
    }

    /// <summary>
    /// <see cref="BattleManager.RunEndPhaseAsync"/> 内、病系処理の直後・Refill より前。
    /// メッセージ → 規定インターバル → 裏向きドロー → 表向け。
    /// </summary>
    public static async Task ProcessTurnEndBonusAsync(BattleManager bm, SummonTurnCounterState ctr, CancellationToken ct)
    {
        if (bm == null || ctr == null) return;
        if (ct.IsCancellationRequested || bm.CurrentState != GameState.EndPhase) return;

        bool isPlayerTurn = bm.CurrentTurnOwner == PlayerType.Player;
        if (isPlayerTurn)
        {
            ctr.PlayerOwnTurnsEnded++;
            if (ctr.PlayerOwnTurnsEnded % 5 != 0) return;
            var sd = bm.GetPlayerStatus()?.summonData;
            if (sd == null || !sd.IsGarudaLifecycle()) return;
            var owner = bm.GetPlayerStatus();
            // 呪縛中は 5n ボーナスのみスキップ（カウンタは既に進行済み）。開幕12枚は別経路のため対象外。
            if (owner != null && !owner.HasCurseBindEffect())
                await RunGarudaTurnEndDrawSequenceAsync(bm, owner, bm.playerHand, isPlayerHand: true, ct);
        }
        else
        {
            ctr.EnemyOwnTurnsEnded++;
            if (ctr.EnemyOwnTurnsEnded % 5 != 0) return;
            var sd = bm.GetEnemyStatus()?.summonData;
            if (sd == null || !sd.IsGarudaLifecycle()) return;
            var owner = bm.GetEnemyStatus();
            if (owner != null && !owner.HasCurseBindEffect())
                await RunGarudaTurnEndDrawSequenceAsync(bm, owner, bm.cpuHand, isPlayerHand: false, ct);
        }

        if (ct.IsCancellationRequested) return;
        BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
        BattleUIManager.I?.RefreshMagicCardInteractivity(bm.playerHand);
    }

    private static async Task RunGarudaTurnEndDrawSequenceAsync(
        BattleManager bm,
        PlayerStatus owner,
        List<CardData> hand,
        bool isPlayerHand,
        CancellationToken ct)
    {
        var ui = BattleUIManager.I;
        if (ui == null || owner == null) return;

        Color messageColor = new Color(0.5f, 0.92f, 0.72f);
        SoundEffectPlayer.I?.Play(TurnEndBonusSeAddress);
        float fadeSec = ui.ShowMessagePopupForTarget(owner, TurnEndBonusMessage, messageColor);
        if (fadeSec <= 0f)
            fadeSec = DamagePopup.DefaultFadeDurationIfUnknown;

        await Task.Delay(TimeSpan.FromSeconds(fadeSec), ct);
        if (ct.IsCancellationRequested) return;
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
        if (ct.IsCancellationRequested) return;

        int cap = isPlayerHand ? bm.GetHandMaxCount() : bm.GetEnemyHandCapacity();
        int space = Mathf.Max(0, cap - hand.Count);
        int toDraw = Mathf.Min(2, space);
        if (toDraw <= 0) return;

        var drawn = new List<CardData>();
        var refill = bm.HandRefill;

        for (int i = 0; i < toDraw; i++)
        {
            if (ct.IsCancellationRequested) return;
            if (isPlayerHand)
            {
                if (refill == null || hand.Count >= bm.GetHandMaxCount()) break;
                var card = await refill.DrawCardAsync(hand, trailingDelayMs: 0, playSoundOnDraw: false);
                if (card != null) drawn.Add(card);
            }
            else
            {
                if (hand.Count >= bm.GetEnemyHandCapacity()) break;
                var card = bm.cardDealer.DrawRandomCard();
                if (card != null) hand.Add(card);
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
