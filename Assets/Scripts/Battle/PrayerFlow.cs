using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 「祈り」：手札に CardType.Attack が無いとき UseButton から発動。DamagePopup「祈る」→1枚ドロー→ターン終了。
/// </summary>
public static class PrayerFlow
{
    public const string Message = "祈る";

    public static readonly Color MessageColor = new Color(1f, 0.95f, 0.75f, 1f);

    /// <summary>手札に CardType.Attack が1枚も無い（魔法・即時・プール魔法は不問）。</summary>
    public static bool IsEligibleHand(IReadOnlyList<CardData> hand) =>
        !CardRules.HasAttackTypeCardInHand(hand);

    /// <summary>プレイヤー UI の「祈り専用」モードと同じ（攻撃カード以外の選択肢も無い）。</summary>
    public static bool IsPrayOnlyAttackMode(IReadOnlyList<CardData> hand)
    {
        if (!IsEligibleHand(hand)) return false;
        var choices = hand is List<CardData> list
            ? CardRules.GetAttackChoices(list)
            : CardRules.GetAttackChoices(new List<CardData>(hand));
        return choices == null || choices.Count == 0;
    }

    public static void PlayPrayerSe() => SoundEffectCatalogSO.Get()?.PlayPrayer();

    /// <summary>攻撃フェーズ開始時の手札 UI（魔法のみ残る場合は選択可、それ以外は祈り専用 UI）。</summary>
    public static void RefreshPlayerAttackPhaseHandUi(List<CardData> playerHand, bool shouldGrayOutCards)
    {
        if (playerHand == null) return;

        if (IsEligibleHand(playerHand))
        {
            var nonAttackChoices = CardRules.GetAttackChoices(playerHand);
            if (nonAttackChoices.Count > 0)
                BattleUIManager.I?.RefreshAttackInteractivity(playerHand, nonAttackChoices);
            else
                BattleUIManager.I?.SetPrayModeUI(playerHand);
        }
        else if (shouldGrayOutCards)
        {
            BattleUIManager.I?.RefreshAttackInteractivity(
                playerHand, CardRules.GetAttackChoices(playerHand));
        }
        else
        {
            BattleUIManager.I?.SetIntroModeUI(playerHand);
        }
    }

    public static async Task RunPlayerPrayerAsync(IPlayerInputHost host, CancellationToken cancellationToken)
    {
        if (host?.PlayerStatus == null || host.HandRefill == null) return;

        await PresentPrayerPopupAsync(host.PlayerStatus, cancellationToken);
        await host.DrawOneCardAsync(trailingDelayMs: 200, playSoundOnDraw: true);
    }

    /// <summary>CPU：条件を満たすとき攻撃の代わりに祈り（1枚ドロー）してターン終了。</summary>
    public static async Task RunCpuPrayerAsync(
        PlayerStatus enemyStatus,
        List<CardData> cpuHand,
        HandRefillService handRefill,
        CancellationToken cancellationToken)
    {
        await RunOwnerPrayerDrawDataOnlyAsync(enemyStatus, cpuHand, handRefill, cancellationToken);
    }

    /// <summary>
    /// Online: mirror the remote turn owner's prayer. Adds one card to that
    /// owner's mirrored hand only (Enemy draw stream, no local player-hand UI).
    /// </summary>
    public static async Task RunRemoteTurnOwnerPrayerAsync(
        PlayerStatus prayingOwner,
        List<CardData> ownerHand,
        HandRefillService handRefill,
        CancellationToken cancellationToken)
    {
        await RunOwnerPrayerDrawDataOnlyAsync(prayingOwner, ownerHand, handRefill, cancellationToken);
    }

    private static async Task PresentPrayerPopupAsync(PlayerStatus owner, CancellationToken cancellationToken)
    {
        PlayPrayerSe();
        float fade = BattleUIManager.I != null
            ? BattleUIManager.I.ShowMessagePopupForTarget(owner, Message, MessageColor)
            : 0f;
        await DamagePopup.WaitAfterPopupLifetimeAsync(fade, cancellationToken);
    }

    private static async Task RunOwnerPrayerDrawDataOnlyAsync(
        PlayerStatus owner,
        List<CardData> ownerHand,
        HandRefillService handRefill,
        CancellationToken cancellationToken)
    {
        if (owner == null || ownerHand == null || handRefill == null) return;

        await PresentPrayerPopupAsync(owner, cancellationToken);

        var drawn = handRefill.DrawCardDataOnly(ownerHand, PlayerType.Enemy);
        if (drawn != null)
            CardDealAudio.Play(drawn, true);

        await Task.Delay(200, cancellationToken);

        var bm = BattleManager.I;
        if (bm != null)
            BattleUIManager.I?.UpdateStatus(bm.GetPlayerStatus(), bm.GetEnemyStatus());
    }
}
