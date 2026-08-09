using UnityEngine;

/// <summary>
/// Wires battle subsystems at scene startup. Extracted from <see cref="BattleManager.Start"/> (PR-4).
/// </summary>
public sealed class BattleBootstrap
{
    public void RunStartup(IBattleBootstrapHost host)
    {
        ConfigureOnlineMode(host);
        InitializeStatuses(host);
        AssignInitialSummonData(host);
        ApplyDevelopmentSummonOverrides(host);
        ConfigureSummonSkillButtons(host);
        InitializeCardSystems(host);
        InitializeEconomicFeatures(host);
        InitializeMagicPool(host);
        RefreshStartupDisplays(host);
        host.EnsureBattleBgmController();
        BattleBgmController.Instance?.StartBattleSession();
        host.BeginOpeningSequence();
    }

    public void Shutdown(IBattleBootstrapHost host)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (host.IsOnlineMatch)
            host.UnsubscribeOnlineDebugInject();
#endif
    }

    private static void ConfigureOnlineMode(IBattleBootstrapHost host)
    {
        if (!host.IsOnlineMatch) return;

        BattleRandom.InitOnline(OnlineMatchContext.RandomSeed, OnlineMatchContext.IsHost);
        host.EnemyAI = new RemotePlayerAgent();
        Debug.Log(
            $"[BattleBootstrap] Online mode (host={OnlineMatchContext.IsHost}, opponent={OnlineMatchContext.RemotePlayerName})");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        host.SubscribeOnlineDebugInject();
#endif
    }

    private static void InitializeStatuses(IBattleBootstrapHost host)
    {
        var player = new PlayerStatus();
        var enemy = new PlayerStatus();
        player.InitializeAsPlayer();
        enemy.InitializeAsEnemy();
        host.SetPlayerStatus(player);
        host.SetEnemyStatus(enemy);
        HitRateRules.ResetHitRateDisplayMonitor();
    }

    private static void AssignInitialSummonData(IBattleBootstrapHost host)
    {
        if (SummonSelectionManager.I != null)
        {
            host.PlayerStatus.summonData = SummonSelectionManager.I.GetSelectedSummonData();
            host.EnemyStatus.summonData = host.IsOnlineMatch
                ? host.EnemyAI.SelectRandomEnemySummon()
                : SummonSkillCoordinator.ResolveRandomEnemySummon();
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var fallback = Resources.Load<SummonData>("Summons/Garuda");
#else
        SummonData fallback = null;
#endif
        if (fallback == null)
            fallback = Resources.Load<SummonData>("Summons/Ifrit");
        host.PlayerStatus.summonData = fallback;
        host.EnemyStatus.summonData = fallback;
    }

    private static void ApplyDevelopmentSummonOverrides(IBattleBootstrapHost host)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (host.IsOnlineMatch) return;
        host.BattleDebugTools?.ApplyInitialSummonOverrides(host.PlayerStatus, host.EnemyStatus);
#endif
    }

    private static void ConfigureSummonSkillButtons(IBattleBootstrapHost host)
    {
        host.SummonSkillButton?.Configure(host.PlayerStatus, host.EnemyStatus);
        host.EnemySummonSkillButton?.Configure(host.EnemyStatus, host.PlayerStatus);
    }

    private static void InitializeCardSystems(IBattleBootstrapHost host)
    {
        host.CardDealer.Initialize(
            host.PlayerStatus,
            host.EnemyStatus,
            host.HandPanel,
            host.CardUiPrefab,
            host.CardBackSprite);
        host.BattleProcessor.Initialize(host.PlayerStatus, host.EnemyStatus, host.StatusUI, host.CardDealer);
        DiseaseTurnEndProcessor.BindSettings(host.DiseaseTurnEndSettings);
        ShivaDirectAttackFreezeFlow.BindSettings(host.ShivaDirectAttackFreezeSettings);
        host.BattleProcessor.ConfigureStatusEffects(host.StatusProgressionConfig);

        if (host.HandRefill != null)
            host.HandRefill.Initialize(host.HandPanel, host.CardUiPrefab, host.CardBackSprite, host.CardDealer);

        if (host.CardSequenceManager != null)
            host.CardSequenceManager.Initialize(
                host.Manager, host.BattleProcessor, host.HandRefill, host.CardStatsDisplay);
    }

    private static void InitializeEconomicFeatures(IBattleBootstrapHost host)
    {
        host.BuyFeature.Initialize(
            host.Manager,
            host.PlayerStatus,
            host.EnemyStatus,
            host.PlayerHand,
            host.CpuHand,
            host.CardDealer,
            host.CardPurchaseAnimation);

        GameObject sellPopupPrefab = null;
        Canvas popupCanvas = null;
        if (BattleUIManager.I != null)
        {
            sellPopupPrefab = BattleUIManager.I.GetSellConfirmPopupPrefab();
            popupCanvas = BattleUIManager.I.GetPopupCanvas();
            Debug.Log($"[BattleBootstrap] sellPopupPrefab: {(sellPopupPrefab != null ? sellPopupPrefab.name : "null")}");
            Debug.Log($"[BattleBootstrap] popupCanvas: {(popupCanvas != null ? popupCanvas.name : "null")}");
        }
        else
        {
            Debug.LogWarning("[BattleBootstrap] BattleUIManager.I is null");
        }

        host.SellFeature.Initialize(
            host.Manager,
            host.PlayerStatus,
            host.EnemyStatus,
            host.PlayerHand,
            host.CpuHand,
            host.CardDealer,
            sellPopupPrefab,
            popupCanvas,
            host.CardSellAnimation,
            host.HandRefill);

        if (host.ExchangeFeature != null)
        {
            GameObject exchangePopupPrefab = BattleUIManager.I?.GetExchangePopupPrefab();
            GameObject exchangeConfirmPopupPrefab = BattleUIManager.I?.GetExchangeConfirmPopupPrefab();
            host.ExchangeFeature.Initialize(
                host.Manager,
                host.PlayerStatus,
                exchangePopupPrefab,
                exchangeConfirmPopupPrefab,
                popupCanvas);
            Debug.Log("[BattleBootstrap] ExchangeFeature initialized");
        }
        else
        {
            Debug.LogWarning("[BattleBootstrap] ExchangeFeature is not attached");
        }
    }

    private static void InitializeMagicPool(IBattleBootstrapHost host)
    {
        if (host.MagicPoolManager == null) return;

        host.MagicPoolManager.RegisterOnPoolChanged(() =>
        {
            BattleUIManager.I?.UpdateMagicPanel();
            BattleUIManager.I?.RefreshMagicCardInteractivity(host.PlayerHand);
        });
        host.MagicPoolManager.RegisterOnEnemyPoolChanged(() =>
        {
            host.RefreshEnemyMagicPoolSnapshot();
            BattleUIManager.I?.OnEnemyMagicPoolChanged();
        });
        host.RefreshEnemyMagicPoolSnapshot();
        BattleUIManager.I?.OnEnemyMagicPoolChanged();
        Debug.Log("[BattleBootstrap] MagicPoolManager initialized");
    }

    private static void RefreshStartupDisplays(IBattleBootstrapHost host)
    {
        host.CardStatsDisplay?.UpdateDisplay();
    }
}
