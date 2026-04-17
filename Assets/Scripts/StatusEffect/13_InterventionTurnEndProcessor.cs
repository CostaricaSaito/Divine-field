using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 介入（公式13番）：攻撃フェーズを終えた側（TurnEnd 突入時点の <see cref="BattleManager.CurrentTurnOwner"/>）が
/// 病系処理より前に一定確率で、攻撃フェーズ使用可カードから1枚を追加発動する。
/// MP・MagicPool 回数は消費しない。使用されたカードは手札から破棄（プールのみの候補はプールを変更しない）。
/// ファイル名: <c>13_InterventionTurnEndProcessor.cs</c>（<see cref="StatusEffectType.Intervention"/>）。
/// </summary>
public static class InterventionTurnEndProcessor
{
    public const float TriggerChance = 0.25f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>デバッグ用: true のとき介入のランダム発生を必ず通す（本番では false のまま）。</summary>
    public static bool DebugForceInterventionChance100 { get; set; }
#endif

    private static float EffectiveTriggerChance
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return DebugForceInterventionChance100 ? 1f : TriggerChance;
#else
            return TriggerChance;
#endif
        }
    }

    /// <summary>
    /// 病系 TurnEnd 処理の直前に呼ぶ。
    /// </summary>
    public static async Task ProcessIfNeededAsync(BattleManager bm, CancellationToken ct)
    {
        if (bm == null) return;

        PlayerType attackerOwner = bm.CurrentTurnOwner;
        PlayerStatus attackerStatus = attackerOwner == PlayerType.Player ? bm.GetPlayerStatus() : bm.GetEnemyStatus();
        if (attackerStatus == null || !attackerStatus.HasInterventionEffect()) return;

        if (UnityEngine.Random.value > EffectiveTriggerChance) return;

        List<CardData> hand = attackerOwner == PlayerType.Player ? bm.playerHand : bm.cpuHand;
        var candidates = BuildInterventionCandidates(hand, attackerOwner, attackerStatus);
        if (candidates.Count == 0) return;

        int idx = UnityEngine.Random.Range(0, candidates.Count);
        CardData source = candidates[idx];
        bool fromHand = hand != null && hand.Contains(source);

        BattleUIManager.I?.ShowInterventionIntroPopup(attackerStatus);

        float waitSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await Task.Delay(System.TimeSpan.FromSeconds(waitSec), ct);
        await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);

        // 戦闘解決用はインスタンスを分離（手札参照のままだと破棄時に手札が壊れる）
        CardData combatCard = UnityEngine.Object.Instantiate(source);
        combatCard.name = source.name + " (介入)";
        combatCard.cardUI = null;

        Side displaySide = attackerOwner == PlayerType.Player ? Side.Player : Side.Enemy;
        BattleUIManager.I?.ShowInterventionAttackSheet(combatCard, displaySide);
        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        await Task.Delay(500, ct);

        var atkList = new List<CardData> { combatCard };

        try
        {
            if (attackerOwner == PlayerType.Player)
            {
                if (bm.Sequences != null)
                    await bm.Sequences.ResolvePlayerAttackCombatAsync(
                        atkList, bm.GetPlayerStatus(), bm.GetEnemyStatus(), bm.cpuHand, ct);
                else
                    await RunPlayerInterventionVsEnemyFallbackAsync(bm, atkList, ct);
            }
            else
                await RunEnemyInterventionVsPlayerAsync(bm, atkList, ct);

            if (fromHand && hand != null && hand.Contains(source))
            {
                if (bm.HandRefill != null)
                    await bm.HandRefill.FinalizeInterventionDiscardedCardAsync(
                        source, attackerOwner, bm.playerHand, bm.cpuHand, ct);
                else
                {
                    hand.Remove(source);
                    UnityEngine.Object.Destroy(source);
                }
            }
        }
        finally
        {
            UnityEngine.Object.Destroy(combatCard);
            BattleUIManager.I?.HideAllCardDetails();
            BattleUIManager.I?.ClearAllSelections();
            bm.ClearInterventionDefenseWait();
        }
    }

    private static List<CardData> BuildInterventionCandidates(List<CardData> hand, PlayerType owner, PlayerStatus attackerStatus)
    {
        var list = new List<CardData>();
        if (hand != null)
        {
            foreach (var c in CardRules.GetAttackChoices(hand))
            {
                if (c == null) continue;
                if (CardRules.IsImmediateAction(c)) continue;
                if (c.cardType == CardType.Magic && attackerStatus.IsMagicUseForbidden()) continue;
                list.Add(c);
            }
        }

        if (MagicPoolManager.I != null)
        {
            foreach (var p in MagicPoolManager.I.GetPooledCardDatas(owner))
            {
                if (p == null || !CardRules.IsUsableInAttackPhase(p)) continue;
                if (CardRules.IsImmediateAction(p)) continue;
                if (p.cardType == CardType.Magic && attackerStatus.IsMagicUseForbidden()) continue;
                if (hand != null && hand.Contains(p)) continue;
                list.Add(p);
            }
        }

        return list;
    }

    /// <summary>CardSequenceManager 未設定時のみ使うフォールバック（通常は未使用）。</summary>
    private static async Task RunPlayerInterventionVsEnemyFallbackAsync(BattleManager bm, List<CardData> atkList, CancellationToken ct)
    {
        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, bm.GetPlayerStatus(), bm.GetEnemyStatus());
        bool hit = HitRateRules.RollHit(finalPct);

        if (!hit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(bm.GetEnemyStatus());
            await Task.Delay(System.TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
            return;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float sec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(bm.GetEnemyStatus())
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await Task.Delay(System.TimeSpan.FromSeconds(sec), ct);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
        }

        await bm.PickAndDisplayEnemyDefenseAfterPlayerHitAsync(atkList);
        var def = bm.GetSelectedDefenseCard();
        bool showYurusu = def == null && BattleUIManager.I != null;
        if (showYurusu) BattleUIManager.I.ShowYurusuDisplay();
        try
        {
            await bm.battleProcessor.ResolveCombatAsync(
                atkList, def, bm.GetPlayerStatus(), bm.GetEnemyStatus(), bm.cpuHand, skipHitCheck: true);
        }
        finally
        {
            if (showYurusu) BattleUIManager.I?.HideYurusuButton();
        }

        if (def != null)
            bm.battleProcessor.UseCard(def, bm.cpuHand);
    }

    private static async Task RunEnemyInterventionVsPlayerAsync(BattleManager bm, List<CardData> atkList, CancellationToken ct)
    {
        var primary = HitRateRules.GetPrimaryForHitRate(atkList);
        int finalPct = HitRateRules.ComputeFinalHitPercent(primary, bm.GetEnemyStatus(), bm.GetPlayerStatus());
        bool hit = HitRateRules.RollHit(finalPct);

        if (!hit)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/ニュッ1.mp3");
            BattleUIManager.I?.ShowMissPopup(bm.GetPlayerStatus());
            await Task.Delay(System.TimeSpan.FromSeconds(DamagePopup.DefaultFadeDurationIfUnknown), ct);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
            return;
        }

        if (finalPct < 100)
        {
            SoundEffectPlayer.I?.Play("Assets/SE/小パンチ.mp3");
            float sec = BattleUIManager.I != null
                ? BattleUIManager.I.ShowCombatHitConfirmedPopup(bm.GetPlayerStatus())
                : DamagePopup.DefaultFadeDurationIfUnknown;
            await Task.Delay(System.TimeSpan.FromSeconds(sec), ct);
            await Task.Delay(DamagePopup.PostPopupIntervalMs, ct);
        }

        bm.BeginInterventionPlayerDefensePhase(atkList);
        try
        {
            await bm.WaitForInterventionPlayerDefenseSubmitAsync(ct);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        var defs = BattleUIManager.I?.GetSelectedDefenseCards() ?? new List<CardData>();
        if (defs.Count == 0)
        {
            await bm.battleProcessor.ResolveCombatAsync(
                atkList, (CardData)null, bm.GetEnemyStatus(), bm.GetPlayerStatus(), bm.playerHand, skipHitCheck: true);
        }
        else if (defs.Count == 1)
        {
            await bm.battleProcessor.ResolveCombatAsync(
                atkList, defs[0], bm.GetEnemyStatus(), bm.GetPlayerStatus(), bm.playerHand, skipHitCheck: true);
        }
        else
        {
            await bm.battleProcessor.ResolveCombatAsync(
                atkList, defs, bm.GetEnemyStatus(), bm.GetPlayerStatus(), bm.playerHand, skipHitCheck: true);
        }

        foreach (var d in defs)
        {
            if (d != null)
                bm.battleProcessor.UseCard(d, bm.playerHand);
        }
    }
}
