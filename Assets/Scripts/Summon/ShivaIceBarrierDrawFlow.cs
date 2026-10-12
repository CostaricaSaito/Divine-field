using UnityEngine;

/// <summary>
/// Shiva passive Ice Barrier: after a qualifying opponent draw, roll and optionally replace with FROZEN.
/// </summary>
public static class ShivaIceBarrierDrawFlow
{
    private static ShivaIceBarrierSettings _settings;
    private static ShivaIceBarrierSettings _fallbackInstance;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Next weighted draw skips ice barrier (debug inject / cheat).</summary>
    public static bool DebugSkipIceBarrierOnce { get; set; }

    public enum DebugIceBarrierChanceMode
    {
        UseSettings = 0,
        Force50Percent = 50,
        Force100Percent = 100,
    }

    /// <summary>
    /// Overrides chance for every ice-barrier roll where the drawer is the opponent of a Shiva summon.
    /// Does not enable ice barrier without Shiva on the blessing owner.
    /// </summary>
    public static DebugIceBarrierChanceMode DebugIceBarrierChanceOverride { get; set; }
#endif

    public static void BindSettings(ShivaIceBarrierSettings settings) => _settings = settings;

    private static ShivaIceBarrierSettings Active
    {
        get
        {
            if (_settings != null) return _settings;
            if (_fallbackInstance == null)
            {
                _fallbackInstance = ScriptableObject.CreateInstance<ShivaIceBarrierSettings>();
                _fallbackInstance.name = "ShivaIceBarrierSettings (Runtime Fallback)";
            }

            return _fallbackInstance;
        }
    }

    /// <summary>
    /// Applies ice barrier to a card already drawn from the weighted pool. May destroy and replace <paramref name="drawn"/>.
    /// </summary>
    public static CardData ApplyAfterWeightedDraw(CardDealer dealer, CardData drawn, PlayerType drawerSide)
    {
        if (drawn == null || dealer == null) return drawn;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugSkipIceBarrierOnce)
        {
            DebugSkipIceBarrierOnce = false;
            return drawn;
        }
#endif

        if (!TryGetBlessingOwnerForDrawer(drawerSide, out var blessingOwner)) return drawn;
        if (blessingOwner.HasCurseBindEffect()) return drawn;

        var s = Active;
        if (BattleRandom.Range(0, 100) >= ResolveIceBarrierChancePercent(s)) return drawn;

        var frozenTemplate = dealer.FindTemplateByDisplayOrAssetName(IceBarrierTransformRegistry.FrozenTemplateAssetName);
        if (frozenTemplate == null)
        {
            Debug.LogWarning("[ShivaIceBarrierDrawFlow] FROZEN template not found");
            return drawn;
        }

        string originalAsset = drawn.name;
        bool trackExo = SealedExoDionRules.IsSealedExoDion(drawn);
        Object.Destroy(drawn);
        var frozen = dealer.InstantiateCardFromTemplate(frozenTemplate);
        if (frozen == null) return null;

        IceBarrierTransformRegistry.Register(frozen, originalAsset);
        if (trackExo)
            SealedExoDionHandTracker.RegisterAddedToHand(frozen, dealer);
        return frozen;
    }

    public static bool TryGetBlessingOwnerForDrawer(PlayerType drawerSide, out PlayerStatus blessingOwner)
    {
        blessingOwner = null;
        var bm = BattleManager.I;
        if (bm == null) return false;

        var player = bm.GetPlayerStatus();
        var enemy = bm.GetEnemyStatus();

        if (drawerSide == PlayerType.Player)
        {
            if (enemy?.summonData != null && enemy.summonData.IsShivaIceBarrier())
            {
                blessingOwner = enemy;
                return true;
            }
        }
        else
        {
            if (player?.summonData != null && player.summonData.IsShivaIceBarrier())
            {
                blessingOwner = player;
                return true;
            }
        }

        return false;
    }

    public static string GetOriginalAssetNameForSync(CardData card)
    {
        return IceBarrierTransformRegistry.TryGetOriginalTemplateAssetName(card, out var name) ? name : string.Empty;
    }

    private static int ResolveIceBarrierChancePercent(ShivaIceBarrierSettings settings)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return DebugIceBarrierChanceOverride switch
        {
            DebugIceBarrierChanceMode.Force50Percent => 50,
            DebugIceBarrierChanceMode.Force100Percent => 100,
            _ => settings != null ? settings.iceBarrierChancePercent : 0,
        };
#else
        return settings != null ? settings.iceBarrierChancePercent : 0;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static string DebugIceBarrierChanceOverrideLabel()
    {
        return DebugIceBarrierChanceOverride switch
        {
            DebugIceBarrierChanceMode.Force50Percent => "50%",
            DebugIceBarrierChanceMode.Force100Percent => "100%",
            _ => "通常(SO)",
        };
    }
#endif
}
