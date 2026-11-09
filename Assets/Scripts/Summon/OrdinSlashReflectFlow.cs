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
    /// Reflection chain: rolls Odin auto-bounce for the side about to defend. Consumes BattleRandom only when eligible.
    /// </summary>
    public static bool TryRollChainAutoBounce(
        BattleManager bm,
        PlayerStatus defender,
        PlayerStatus attacker,
        IReadOnlyList<CardData> incomingAttack)
    {
        if (bm == null || defender == null || attacker == null || incomingAttack == null || incomingAttack.Count == 0)
            return false;
        if (!OrdinSlashReflectRules.CanTrigger(defender, attacker, incomingAttack, bm))
            return false;
        return RollSuccess();
    }

    /// <summary>Cut-in and bounce popup. The card slide is done by <see cref="ReactiveDefenseChain"/>.</summary>
    public static async Task PlayAutoBouncePresentationAsync(PlayerStatus popupOwner, CancellationToken ct)
    {
        await OrdinSlashReflectPresentation.RunCutInAsync(ct);
        ct.ThrowIfCancellationRequested();

        float bounceSec = BattleUIManager.I != null
            ? BattleUIManager.I.ShowOrdinReflectionBouncePopup(popupOwner)
            : DamagePopup.DefaultFadeDurationIfUnknown;
        if (bounceSec <= 0f) bounceSec = DamagePopup.DefaultFadeDurationIfUnknown;
        await DamagePopup.WaitAfterPopupLifetimeAsync(bounceSec, ct);
    }

    private static async Task RunPlayerOrdinReflectAsync(
        BattleManager bm,
        List<CardData> incomingAttack,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        bm.AdHocDefense?.Clear();
        SuppressPlayerDefenseInputForOrdinReflect(bm);

        await ReactiveDefenseChain.RunAsync(
            bm, incomingAttack, PlayerType.Enemy, ReactiveDefenseResponse.OrdinAutoBounce(), ct);

        await FinishAfterPlayerOrdinReflectAsync(bm, context, ct);
    }

    private static async Task RunEnemyOrdinReflectAsync(
        BattleManager bm,
        List<CardData> incomingAttack,
        OrdinInterceptContext context,
        CancellationToken ct)
    {
        await ReactiveDefenseChain.RunAsync(
            bm, incomingAttack, PlayerType.Player, ReactiveDefenseResponse.OrdinAutoBounce(), ct);

        await FinishAfterEnemyOrdinReflectAsync(bm, context, ct);
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
        bm.ClearIncomingAttackElementOverrides();
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

    /// <summary>
    /// Odin auto-reflect skips manual defense. Lock Use (許す) and hand input immediately.
    /// </summary>
    private static void SuppressPlayerDefenseInputForOrdinReflect(BattleManager bm)
    {
        if (bm?.PlayerInput != null)
            bm.PlayerInput.IsProcessingUseButton = true;

        var ui = BattleUIManager.I;
        if (ui == null) return;

        ui.HideYurusuButton();
        ui.SetHandClickable(false);
        ui.SetUseButtonInteractable(false);
    }
}
