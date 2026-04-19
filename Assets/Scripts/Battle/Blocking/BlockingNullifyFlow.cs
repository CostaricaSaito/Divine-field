using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>物理無効（ブロッキング）成功時：ダメージ不適用・通常フローへ。</summary>
public static class BlockingNullifyFlow
{
    /// <summary>プレイヤー防御：敵攻撃を無効化（カードは <see cref="CardSequenceManager"/> で既に Use 済み）。</summary>
    public static async Task RunPlayerInitiatedAsync(
        BattleManager battleManager,
        List<CardData> incomingAttackCards,
        CardData blockingDefenseCard,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || incomingAttackCards == null || incomingAttackCards.Count == 0)
            return;

        var player = battleManager.GetPlayerStatus();
        float sec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowBlockingNullifyPopup(player)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (sec <= 0f) sec = DamagePopup.DefaultFadeDurationIfUnknown;

        await Task.Delay(TimeSpan.FromSeconds(sec), cancellationToken);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);

        if (blockingDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetForCardData(blockingDefenseCard);

        BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
    }

    /// <summary>敵防御：プレイヤー攻撃を無効化（未使用のためここで Use／記録）。</summary>
    public static async Task RunEnemyDefenderNullifiesAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        HandRefillService handRefill,
        List<CardData> incomingPlayerAttackCards,
        CardData enemyBlockingDefenseCard,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || battleProcessor == null || incomingPlayerAttackCards == null
            || incomingPlayerAttackCards.Count == 0)
            return;

        var enemy = battleManager.GetEnemyStatus();

        if (enemyBlockingDefenseCard != null)
        {
            handRefill?.RecordEnemyUse(enemyBlockingDefenseCard);
            battleProcessor.UseCard(enemyBlockingDefenseCard, battleManager.cpuHand);
        }

        float sec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowBlockingNullifyPopup(enemy)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (sec <= 0f) sec = DamagePopup.DefaultFadeDurationIfUnknown;

        await Task.Delay(TimeSpan.FromSeconds(sec), cancellationToken);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, cancellationToken);

        if (enemyBlockingDefenseCard != null)
            BattleUIManager.I?.DestroyCardSheetForCardData(enemyBlockingDefenseCard);

        BattleUIManager.I?.UpdateStatus(battleManager.GetPlayerStatus(), battleManager.GetEnemyStatus());
    }
}
