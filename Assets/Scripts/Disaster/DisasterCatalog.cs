using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天変地異の抽選プール・表示名・フォールバック効果 SO。
/// </summary>
public static class DisasterCatalog
{
    public const string ImportantPopupMessage = "空は裂け、大地が震える";
    public static readonly Color ImportantPopupColor = new Color(0.95f, 0.45f, 0.22f);

    private static readonly DisasterKind[] AllKindsInternal =
    {
        DisasterKind.Eruption,
        DisasterKind.SolarEclipse,
        DisasterKind.LunarEclipse,
        DisasterKind.Kannaduki,
        DisasterKind.BlackMonday,
        DisasterKind.RealityBending,
        DisasterKind.RampageZantetsuken,
        DisasterKind.MiracleArk,
        DisasterKind.ManaStream,
        DisasterKind.ChaosAttractor,
        DisasterKind.Infection,
    };

    private static readonly Dictionary<DisasterKind, string> DisplayNames = new()
    {
        { DisasterKind.Eruption, "『噴火』" },
        { DisasterKind.SolarEclipse, "『日蝕』" },
        { DisasterKind.LunarEclipse, "『月蝕』" },
        { DisasterKind.Kannaduki, "『神無月』" },
        { DisasterKind.BlackMonday, "『大暴落』" },
        { DisasterKind.RealityBending, "『現実改変』" },
        { DisasterKind.RampageZantetsuken, "『暴走斬鉄剣』" },
        { DisasterKind.MiracleArk, "『奇跡の船出』" },
        { DisasterKind.ManaStream, "『マナの奔流』" },
        { DisasterKind.ChaosAttractor, "『原初の混沌』" },
        { DisasterKind.Infection, "『感染症』" },
    };

    private static readonly Dictionary<DisasterKind, string> Descriptions = new()
    {
        { DisasterKind.Eruption, "世界を焦土に変える" },
        { DisasterKind.SolarEclipse, "暗黒の太陽が裁きを下す" },
        { DisasterKind.LunarEclipse, "暗闇の月が力を奪う" },
        { DisasterKind.Kannaduki, "力が暴走する…!?" },
        { DisasterKind.BlackMonday, "ストップ安だ！" },
        { DisasterKind.RealityBending, "世界が書き換わる" },
        { DisasterKind.RampageZantetsuken, "オーディンの怒り" },
        { DisasterKind.MiracleArk, "方舟が光を放つ" },
        { DisasterKind.ManaStream, "魔力が渦を巻く" },
        { DisasterKind.ChaosAttractor, "カオスをこえて終末が近づく" },
        { DisasterKind.Infection, "煉獄の風が吹き荒れる" },
    };

    private static readonly Dictionary<DisasterKind, string> NotificationMessages = new()
    {
        { DisasterKind.Eruption, "世界が焦土に包まれる" },
        { DisasterKind.SolarEclipse, "暗黒の太陽が裁きを下す" },
        { DisasterKind.LunarEclipse, "暗闇の月が力を奪う" },
        { DisasterKind.Kannaduki, "力が暴走する…!?" },
        { DisasterKind.BlackMonday, "ストップ安だ！" },
        { DisasterKind.RealityBending, "世界が書き換わる" },
        { DisasterKind.RampageZantetsuken, "オーディンの怒り" },
        { DisasterKind.MiracleArk, "方舟が光を放つ" },
        { DisasterKind.ManaStream, "魔力が渦を巻く" },
        { DisasterKind.ChaosAttractor, "カオスをこえて終末が近づく" },
        { DisasterKind.Infection, "煉獄の風が吹き荒れる" },
    };

    private static Dictionary<DisasterKind, DisasterCardEffectSO> _effectsByKind;
    private static Dictionary<DisasterKind, CardData> _cardTemplatesByKind;
    private static bool _cardTemplatesRegistered;

    public static IReadOnlyList<DisasterKind> AllKinds => AllKindsInternal;

    /// <summary>
    /// <see cref="CardDealer"/> 初期化時に呼ぶ。cardType=Disaster かつ disasterCardEffect 付き CardData を Kind で索引化。
    /// </summary>
    public static void RegisterCardTemplates(IEnumerable<CardData> allCards)
    {
        _cardTemplatesByKind = new Dictionary<DisasterKind, CardData>();
        _cardTemplatesRegistered = true;

        if (allCards == null) return;

        foreach (var card in allCards)
        {
            if (card == null || card.cardType != CardType.Disaster) continue;
            if (card.disasterCardEffect == null)
            {
                Debug.LogWarning($"[DisasterCatalog] '{card.name}' is Disaster but has no disasterCardEffect.");
                continue;
            }

            var kind = card.disasterCardEffect.Kind;
            if (_cardTemplatesByKind.ContainsKey(kind))
            {
                Debug.LogWarning(
                    $"[DisasterCatalog] Duplicate DisasterKind {kind}: skip '{card.name}' (already have '{_cardTemplatesByKind[kind].name}').");
                continue;
            }

            _cardTemplatesByKind[kind] = card;
        }
    }

    private static void EnsureCardTemplatesLoaded()
    {
        if (_cardTemplatesRegistered) return;
        RegisterCardTemplates(Resources.LoadAll<CardData>("Cards"));
    }

    public static bool TryGetCardTemplate(DisasterKind kind, out CardData template)
    {
        EnsureCardTemplatesLoaded();
        template = null;
        return _cardTemplatesByKind != null
            && _cardTemplatesByKind.TryGetValue(kind, out template)
            && template != null;
    }

    public static string GetDisplayName(DisasterKind kind)
    {
        if (TryGetCardTemplate(kind, out var card) && !string.IsNullOrEmpty(card.cardName))
            return card.cardName;
        return DisplayNames.TryGetValue(kind, out var name) ? name : kind.ToString();
    }

    public static string GetDescription(DisasterKind kind)
    {
        if (TryGetCardTemplate(kind, out var card) && !string.IsNullOrEmpty(card.description))
            return card.description;
        return Descriptions.TryGetValue(kind, out var desc) ? desc : string.Empty;
    }

    public static string GetNotificationMessage(DisasterKind kind)
    {
        if (TryGetRegisteredEffect(kind, out var registered)
            && !string.IsNullOrEmpty(registered.NotificationMessage))
            return registered.NotificationMessage;
        return NotificationMessages.TryGetValue(kind, out var msg) ? msg : GetDescription(kind);
    }

    public static ImportantPopupKind GetDefaultImportantPopupKind(DisasterKind kind)
    {
        if (TryGetRegisteredEffect(kind, out var registered))
            return registered.ImportantPopupKind;
        return ResolveStaticImportantPopupKind(kind);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Debug: when true, <see cref="RollKind"/> always returns <see cref="DebugPinnedDisasterKind"/>.</summary>
    public static bool DebugPinDisasterKind { get; set; }

    public static DisasterKind DebugPinnedDisasterKind { get; set; } = DisasterKind.Eruption;

    public static void CycleDebugPinnedDisasterKind(int delta)
    {
        if (AllKindsInternal.Length == 0) return;
        int current = 0;
        for (int i = 0; i < AllKindsInternal.Length; i++)
        {
            if (AllKindsInternal[i] == DebugPinnedDisasterKind)
            {
                current = i;
                break;
            }
        }

        int next = (current + delta) % AllKindsInternal.Length;
        if (next < 0) next += AllKindsInternal.Length;
        DebugPinnedDisasterKind = AllKindsInternal[next];
    }
#endif

    public static DisasterKind RollKind(PlayerStatus triggerOwner)
    {
        if (AllKindsInternal.Length == 0) return DisasterKind.Eruption;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugPinDisasterKind)
            return DebugPinnedDisasterKind;
#endif
        PlayerType side = ReferenceEquals(triggerOwner, BattleManager.I?.GetPlayerStatus())
            ? PlayerType.Player
            : PlayerType.Enemy;
        int idx = BattleRandom.DrawRange(side, 0, AllKindsInternal.Length);
        return AllKindsInternal[idx];
    }

    public static DisasterCardEffectSO GetEffect(DisasterKind kind)
    {
        if (TryGetRegisteredEffect(kind, out var registered))
            return registered;

        return CreateRuntimePlaceholderEffect(kind);
    }

    /// <summary>
    /// Card template or Resources/Disaster SO only — never allocates a runtime placeholder.
    /// </summary>
    private static bool TryGetRegisteredEffect(DisasterKind kind, out DisasterCardEffectSO effect)
    {
        EnsureCardTemplatesLoaded();
        if (_cardTemplatesByKind != null
            && _cardTemplatesByKind.TryGetValue(kind, out var card)
            && card != null
            && card.disasterCardEffect != null)
        {
            effect = card.disasterCardEffect;
            return true;
        }

        EnsureEffectsLoaded();
        if (_effectsByKind != null && _effectsByKind.TryGetValue(kind, out effect) && effect != null)
            return true;

        effect = null;
        return false;
    }

    private static PlaceholderDisasterEffectSO CreateRuntimePlaceholderEffect(DisasterKind kind)
    {
        var fallback = ScriptableObject.CreateInstance<PlaceholderDisasterEffectSO>();
        fallback.ConfigureForRuntime(
            kind,
            NotificationMessages.TryGetValue(kind, out var msg) ? msg : GetDescription(kind),
            ResolveStaticImportantPopupKind(kind));
        return fallback;
    }

    private static ImportantPopupKind ResolveStaticImportantPopupKind(DisasterKind kind)
        => (ImportantPopupKind)((int)ImportantPopupKind.DisasterEruption + (int)kind);

    private static void EnsureEffectsLoaded()
    {
        if (_effectsByKind != null) return;

        _effectsByKind = new Dictionary<DisasterKind, DisasterCardEffectSO>();
        var loaded = Resources.LoadAll<DisasterCardEffectSO>("Disaster");
        if (loaded == null) return;

        for (int i = 0; i < loaded.Length; i++)
        {
            var effect = loaded[i];
            if (effect == null) continue;
            _effectsByKind[effect.Kind] = effect;
        }
    }
}
