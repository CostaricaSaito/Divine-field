using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Video;

/// <summary>
/// Where Odin slash reflect was attempted (parry rerun is excluded).
/// </summary>
public enum OrdinInterceptContext
{
    NormalDefensePhase,
    ReflectionChain,
    Intervention,
    Disaster,
    DualBladeSecondDefense,
}

/// <summary>
/// Odin passive (切り払い): 5% auto physical reflect before defense select.
/// </summary>
public static class OrdinSlashReflectFlow
{
    private const float SlideDurationSec = 0.5f;

    private static OrdinSlashReflectSettings _settings;
    private static OrdinSlashReflectSettings _fallbackInstance;

    public static bool DebugForceOrdinSlashReflect100;

    public static void BindSettings(OrdinSlashReflectSettings settings)
    {
        _settings = settings;
    }

    public static OrdinSlashReflectSettings ActiveSettings
    {
        get
        {
            if (_settings != null) return _settings;
            if (_fallbackInstance == null)
            {
                _fallbackInstance = Resources.Load<OrdinSlashReflectSettings>("OrdinSlashReflectSettings");
                if (_fallbackInstance == null)
                {
                    _fallbackInstance = ScriptableObject.CreateInstance<OrdinSlashReflectSettings>();
                    _fallbackInstance.name = "OrdinSlashReflectSettings (Runtime Fallback)";
                }
            }
            return _fallbackInstance;
        }
    }

    public static async Task<VideoClip> LoadVideoClipAsync(string address, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(address)) return null;

        AsyncOperationHandle<VideoClip> handle = Addressables.LoadAssetAsync<VideoClip>(address);
        while (!handle.IsDone)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[OrdinSlashReflectFlow] Failed to load video: {address}");
            return null;
        }

        return handle.Result;
    }

    public static bool RollSuccess()
    {
        if (DebugForceOrdinSlashReflect100) return true;
        var s = ActiveSettings;
        return BattleRandom.Range(0, 100) < s.slashReflectChancePercent;
    }

    public static async Task<bool> TryInterceptPlayerDefenseAsync(
        BattleManager bm,
        IReadOnlyList<CardData> incomingAttack,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        if (bm == null || incomingAttack == null || incomingAttack.Count == 0)
            return false;

        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        if (player == null || enemy == null)
            return false;

        if (!OrdinSlashReflectRules.CanTrigger(player, enemy, incomingAttack, bm))
            return false;
        if (!RollSuccess())
            return false;

        var attackList = CopyAttackList(incomingAttack);
        await RunPlayerOrdinReflectAsync(bm, attackList, context, ct);
        return true;
    }

    public static async Task<bool> TryInterceptEnemyDefenseAsync(
        BattleManager bm,
        IReadOnlyList<CardData> incomingAttack,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        if (bm == null || incomingAttack == null || incomingAttack.Count == 0)
            return false;

        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        if (player == null || enemy == null)
            return false;

        if (!OrdinSlashReflectRules.CanTrigger(enemy, player, incomingAttack, bm))
            return false;
        if (!RollSuccess())
            return false;

        var attackList = CopyAttackList(incomingAttack);
        await RunEnemyOrdinReflectAsync(bm, attackList, context, ct);
        return true;
    }

    /// <summary>
    /// Reflection chain: player auto-bounce without card use. Returns true when handled.
    /// </summary>
    public static async Task<bool> TryRunPlayerBounceInReflectionChainAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        List<CardData> incomingAttackCards,
        int incomingPower,
        PlayerStatus reflectionBlessingAttacker,
        PlayerStatus reflectionBlessingDefender,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || incomingAttackCards == null || incomingAttackCards.Count == 0)
            return false;

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();
        if (player == null || enemy == null)
            return false;

        if (!OrdinSlashReflectRules.CanTrigger(player, enemy, incomingAttackCards, battleManager))
            return false;
        if (!RollSuccess())
            return false;

        await RunOrdinBouncePresentationAsync(
            player,
            incomingAttackCards,
            slideTowardPlayer: true,
            incomingPower,
            battleManager,
            reflectionBlessingAttacker,
            reflectionBlessingDefender,
            cancellationToken);
        return true;
    }

    /// <summary>
    /// Reflection chain: enemy auto-bounce without card use. Returns true when handled.
    /// </summary>
    public static async Task<bool> TryRunEnemyBounceInReflectionChainAsync(
        BattleManager battleManager,
        BattleProcessor battleProcessor,
        List<CardData> incomingAttackCards,
        int incomingPower,
        PlayerStatus reflectionBlessingAttacker,
        PlayerStatus reflectionBlessingDefender,
        CancellationToken cancellationToken)
    {
        if (battleManager == null || incomingAttackCards == null || incomingAttackCards.Count == 0)
            return false;

        var player = battleManager.GetPlayerStatus();
        var enemy = battleManager.GetEnemyStatus();
        if (player == null || enemy == null)
            return false;

        if (!OrdinSlashReflectRules.CanTrigger(enemy, player, incomingAttackCards, battleManager))
            return false;
        if (!RollSuccess())
            return false;

        await RunOrdinBouncePresentationAsync(
            enemy,
            incomingAttackCards,
            slideTowardPlayer: false,
            incomingPower,
            battleManager,
            reflectionBlessingAttacker,
            reflectionBlessingDefender,
            cancellationToken);
        return true;
    }

    private static async Task RunPlayerOrdinReflectAsync(
        BattleManager bm,
        List<CardData> incomingAttack,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        var processor = bm.battleProcessor;
        if (processor == null || player == null || enemy == null)
            return;

        bm.AdHocDefense?.Clear();

        bm.ClearReflectionAttackTotalDisplay();
        int incomingPower = processor.ComputeReflectionIncomingAttackPower(incomingAttack, enemy, player);

        await RunOrdinBouncePresentationAsync(
            player,
            incomingAttack,
            slideTowardPlayer: true,
            incomingPower,
            bm,
            enemy,
            enemy,
            ct);

        try
        {
            await PhysicalReflectionFlow.RunReflectionChainLoopAsync(
                bm,
                processor,
                bm.HandRefill,
                bm.GetEnemyAI(),
                incomingAttack,
                incomingPower,
                PlayerType.Enemy,
                sessionMagic: false,
                ct,
                enemy,
                enemy);
        }
        finally
        {
            bm.ClearReflectionAttackTotalDisplay();
        }

        await FinishAfterPlayerOrdinReflectAsync(bm, context, ct);
    }

    private static async Task RunEnemyOrdinReflectAsync(
        BattleManager bm,
        List<CardData> incomingAttack,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();
        var processor = bm.battleProcessor;
        if (processor == null || player == null || enemy == null)
            return;

        bm.ClearReflectionAttackTotalDisplay();
        int incomingPower = processor.ComputeReflectionIncomingAttackPower(incomingAttack, player, enemy);

        await RunOrdinBouncePresentationAsync(
            enemy,
            incomingAttack,
            slideTowardPlayer: false,
            incomingPower,
            bm,
            player,
            player,
            ct);

        try
        {
            await PhysicalReflectionFlow.RunReflectionChainLoopAsync(
                bm,
                processor,
                bm.HandRefill,
                bm.GetEnemyAI(),
                incomingAttack,
                incomingPower,
                PlayerType.Player,
                sessionMagic: false,
                ct,
                player,
                player);
        }
        finally
        {
            bm.ClearReflectionAttackTotalDisplay();
        }

        await FinishAfterEnemyOrdinReflectAsync(bm, context, ct);
    }

    private static async Task RunOrdinBouncePresentationAsync(
        PlayerStatus popupOwner,
        List<CardData> incomingAttack,
        bool slideTowardPlayer,
        int incomingPower,
        BattleManager bm,
        PlayerStatus reflectionBlessingAttacker,
        PlayerStatus reflectionBlessingDefender,
        CancellationToken ct)
    {
        await OrdinSlashReflectPresentation.RunCutInAsync(ct);
        if (ct.IsCancellationRequested) return;

        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowOrdinReflectionBouncePopup(popupOwner)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(bounceSec, ct);
        if (ct.IsCancellationRequested) return;

        if (BattleUIManager.I != null)
            await BattleUIManager.I.SlideReflectionAttackSheetsAsync(
                incomingAttack, slideTowardPlayer, SlideDurationSec, ct);
        SoundEffectPlayer.I?.Play(CardDealAudio.NormalPath);
        bm.SetReflectionAttackTotalDisplayAfterSlide(
            incomingAttack,
            totalAtkOnPlayerSide: slideTowardPlayer,
            reflectionBlessingAttacker,
            reflectionBlessingDefender,
            incomingPower);
    }

    private static async Task FinishAfterPlayerOrdinReflectAsync(
        BattleManager bm,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        if (await bm.TryHandleDeathIfAnyAsync(ct)) return;

        switch (context)
        {
            case OrdinInterceptContext.Intervention:
                bm.ClearInterventionDefenseWait();
                BattleUIManager.I?.HideAllCardDetails();
                bm.ClearStatsDisplaySequenceCards();
                return;
            case OrdinInterceptContext.Disaster:
                bm.ClearDisasterPlayerDefenseWait();
                BattleUIManager.I?.HideAllCardDetails();
                bm.ClearStatsDisplaySequenceCards();
                return;
            default:
                if (bm.Sequences != null)
                    await bm.Sequences.RunAfterCombatSharedCleanupAsync(ct);
                else
                    await RunFallbackAfterCombatCleanupAsync(bm, ct);
                break;
        }
    }

    private static async Task FinishAfterEnemyOrdinReflectAsync(
        BattleManager bm,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        if (await bm.TryHandleDeathIfAnyAsync(ct)) return;

        bm.ClearMagicalExplosionComboMpPoolSnapshot();
        bm.ClearMillionDollarBazookaComboGpPoolSnapshot();
        bm.ClearTributeBloodHpPaidSnapshot();
        bm.ClearHammadnessRollSnapshot();
        BattleUIManager.I?.HideAllCardDetails();
        bm.ClearStatsDisplaySequenceCards();
        bm.SetCurrentAttackCard(null);
        bm.SetSuppressEnemyStaleAttackerInTotalByOrb(false);
        bm.UpdateTotalATKDEFDisplay();

        if (context == OrdinInterceptContext.NormalDefensePhase
            || context == OrdinInterceptContext.DualBladeSecondDefense)
            bm.SetGameState(GameState.CombatResolvePhase);
    }

    private static async Task RunFallbackAfterCombatCleanupAsync(BattleManager bm, CancellationToken ct)
    {
        if (await bm.TryPreparePlayerDualBladeSecondDefenseIfNeededAsync(ct))
            return;

        BattleUIManager.I?.HideAllCardDetails();
        bm.ClearStatsDisplaySequenceCards();
        bm.SetCurrentAttackCard(null);
        bm.ClearIncomingAttackForceNoneElement();
        bm.SetGameState(GameState.CombatResolvePhase);
    }

    private static List<CardData> CopyAttackList(IReadOnlyList<CardData> incomingAttack)
    {
        var list = new List<CardData>(incomingAttack.Count);
        for (int i = 0; i < incomingAttack.Count; i++)
        {
            if (incomingAttack[i] != null)
                list.Add(incomingAttack[i]);
        }
        return list;
    }
}
