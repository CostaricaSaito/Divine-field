using System;
using UnityEngine;

[Serializable]
public struct DamagePopupStyleEntry
{
    public DamagePopupKind kind;
    [TextArea(1, 2)] public string message;

    [Header("Background")]
    public MessagePopupBackgroundMode backgroundMode;
    [Tooltip("SolidColor mode. Alpha 0 keeps prefab panel color (numeric damage default).")]
    public Color backgroundColor;
    [Tooltip("Sprite mode. PNG with transparent corners (pill shape baked in).")]
    public Sprite backgroundSprite;

    [Header("Text")]
    public Color textColor;
    public Color outlineColor;
    [Tooltip("Reflection bounce: per-character rainbow rich text.")]
    public bool useRainbowText;

    public bool UsesSpriteBackground =>
        backgroundMode == MessagePopupBackgroundMode.Sprite && backgroundSprite != null;
}

/// <summary>
/// Inspector-driven colors, sprites, and text for <see cref="DamagePopup"/> variants.
/// </summary>
[CreateAssetMenu(fileName = "DamagePopupSettings", menuName = "DivineField/UI/Damage Popup Settings")]
public sealed class DamagePopupSettings : ScriptableObject
{
    [SerializeField] private DamagePopupStyleEntry[] entries;

    [Header("Motion / Timing")]
    [SerializeField] private PopupMotionTiming motion = PopupMotionTiming.DamageDefaults;

    [Tooltip("Fallback when Show* returns 0 (spawn failed).")]
    [SerializeField] [Min(0.01f)] private float defaultFadeDurationIfUnknown = 1f;

    [Header("Sequence Delays (ms)")]
    [Tooltip("After damage popup, before WithDamageThrough status effects.")]
    [SerializeField] private int preStatusEffectAfterDamagePopupDelayMs = 500;
    [Tooltip("After last popup in combat resolve, before TurnEnd-style transition.")]
    [SerializeField] private int postLastPresentationBeforeCombatResolveMs = 400;
    [Tooltip("Before immediate card effect resolution.")]
    [SerializeField] private int preImmediateEffectDelayMs = 250;
    [Tooltip("Beat before numeric damage popup appears.")]
    [SerializeField] private int preDamagePopupBeatMs = 500;

    public PopupMotionTiming MotionOrDefault => motion.WithDefaults(PopupMotionTiming.DamageDefaults);

    public float FloatSpeed => MotionOrDefault.floatSpeed;
    public float FadeDuration => MotionOrDefault.fadeDuration;
    public int PostPopupIntervalMs => MotionOrDefault.postPopupIntervalMs;
    public float DefaultFadeDurationIfUnknown => defaultFadeDurationIfUnknown > 0.001f
        ? defaultFadeDurationIfUnknown
        : 1f;
    public int PreStatusEffectAfterDamagePopupDelayMs => preStatusEffectAfterDamagePopupDelayMs;
    public int PostLastPresentationBeforeCombatResolveMs => postLastPresentationBeforeCombatResolveMs;
    public int PreImmediateEffectDelayMs => preImmediateEffectDelayMs;
    public int PreDamagePopupBeatMs => preDamagePopupBeatMs;

    public bool TryGetEntry(DamagePopupKind kind, out DamagePopupStyleEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].kind == kind)
                {
                    entry = entries[i];
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    public DamagePopupStyleEntry GetEntryOrDefault(DamagePopupKind kind)
    {
        if (TryGetEntry(kind, out var entry)) return entry;
        foreach (var e in DefaultEntries())
        {
            if (e.kind == kind) return e;
        }
        return default;
    }

    private static DamagePopupStyleEntry[] DefaultEntries()
    {
        return new[]
        {
            NumericEntry(DamagePopupKind.DamageToPlayer,
                new Color(0.25f, 0.95f, 1f), Color.white, new Color(0f, 0f, 0f, 0f)),
            NumericEntry(DamagePopupKind.DamageToEnemy,
                new Color(0.92f, 0.12f, 0.18f), Color.white, new Color(0f, 0f, 0f, 0f)),
            MessageEntry(DamagePopupKind.NoDamage, "\u7121\u50b7",
                new Color(49f / 255f, 49f / 255f, 49f / 255f, 1f),
                new Color(0f, 1f, 72f / 255f), Color.white),
            PanelOnlyEntry(DamagePopupKind.DarkFollowupDamage,
                new Color(0.28f, 0.1f, 0.42f, 0.94f)),
            RainbowMessageEntry(DamagePopupKind.ReflectionBounce, "\u5f3e\u304d\u8fd4\u3059",
                new Color(0.82f, 0.08f, 0.1f, 0.96f)),
            MessageEntry(DamagePopupKind.BlockingNullify, "\u8b77\u8eab",
                new Color(0f, 0f, 0f, 0.96f),
                new Color(0.55f, 0.55f, 0.55f, 1f), Color.white),
            MessageEntry(DamagePopupKind.ParryIntro, "\u6253\u3061\u6255\u3046",
                new Color(247f / 255f, 211f / 255f, 88f / 255f, 0.96f),
                Color.white, Color.black),
            TextOnlyEntry(DamagePopupKind.Heal, string.Empty, Color.green, Color.white),
            TextOnlyEntry(DamagePopupKind.HealMp, string.Empty,
                new Color(0.35f, 0.92f, 1f), Color.white),
            TextOnlyEntry(DamagePopupKind.HealGp, string.Empty,
                new Color(1f, 0.84f, 0.15f), new Color(0.35f, 0.22f, 0f, 1f)),
            TextOnlyEntry(DamagePopupKind.Miss, "\u30df\u30b9", Color.yellow, Color.white),
            TextOnlyEntry(DamagePopupKind.CombatHitConfirmed, "\u7684\u4e2d",
                new Color(1f, 0.92f, 0.35f), Color.white),
            HandReloadEntry(DamagePopupKind.HandReload, "\u30ea\u30ed\u30fc\u30c9"),
            HandReloadEntry(DamagePopupKind.HandDiscardRestart, "\u5f15\u304d\u76f4\u3057"),
            MessageEntry(DamagePopupKind.OrdinReflectionBounce, "\u5f3e\u304d\u8fd4\u3059",
                new Color(0.78f, 0.8f, 0.84f, 1f), Color.black, Color.white),
        };
    }

    private static DamagePopupStyleEntry NumericEntry(
        DamagePopupKind kind, Color text, Color outline, Color panel)
    {
        return new DamagePopupStyleEntry
        {
            kind = kind,
            message = string.Empty,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = panel,
            backgroundSprite = null,
            textColor = text,
            outlineColor = outline,
            useRainbowText = false,
        };
    }

    private static DamagePopupStyleEntry PanelOnlyEntry(DamagePopupKind kind, Color panel)
    {
        return new DamagePopupStyleEntry
        {
            kind = kind,
            message = string.Empty,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = panel,
            backgroundSprite = null,
            textColor = Color.white,
            outlineColor = Color.white,
            useRainbowText = false,
        };
    }

    private static DamagePopupStyleEntry MessageEntry(
        DamagePopupKind kind, string message, Color panel, Color text, Color outline)
    {
        return new DamagePopupStyleEntry
        {
            kind = kind,
            message = message,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = panel,
            backgroundSprite = null,
            textColor = text,
            outlineColor = outline,
            useRainbowText = false,
        };
    }

    private static DamagePopupStyleEntry RainbowMessageEntry(
        DamagePopupKind kind, string message, Color panel)
    {
        return new DamagePopupStyleEntry
        {
            kind = kind,
            message = message,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = panel,
            backgroundSprite = null,
            textColor = Color.white,
            outlineColor = Color.black,
            useRainbowText = true,
        };
    }

    private static DamagePopupStyleEntry TextOnlyEntry(
        DamagePopupKind kind, string message, Color text, Color outline)
    {
        return new DamagePopupStyleEntry
        {
            kind = kind,
            message = message,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = new Color(0f, 0f, 0f, 0f),
            backgroundSprite = null,
            textColor = text,
            outlineColor = outline,
            useRainbowText = false,
        };
    }

    private static DamagePopupStyleEntry HandReloadEntry(DamagePopupKind kind, string message)
    {
        return new DamagePopupStyleEntry
        {
            kind = kind,
            message = message,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = new Color(140f / 255f, 96f / 255f, 138f / 255f, 1f),
            backgroundSprite = null,
            textColor = Color.white,
            outlineColor = new Color(212f / 255f, 62f / 255f, 212f / 255f, 1f),
            useRainbowText = false,
        };
    }

    private static DamagePopupSettings _runtimeFallback;

    public static DamagePopupSettings GetRuntimeFallback()
    {
        if (_runtimeFallback != null) return _runtimeFallback;
        var loaded = Resources.Load<DamagePopupSettings>("DamagePopupSettings");
        if (loaded != null) return loaded;
        _runtimeFallback = CreateInstance<DamagePopupSettings>();
        _runtimeFallback.name = "DamagePopupSettings (Runtime Fallback)";
        _runtimeFallback.entries = DefaultEntries();
        _runtimeFallback.motion = PopupMotionTiming.DamageDefaults;
        return _runtimeFallback;
    }
}
