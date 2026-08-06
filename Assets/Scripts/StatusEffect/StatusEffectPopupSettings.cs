using System;
using UnityEngine;

[Serializable]
public struct StatusEffectPopupStyleEntry
{
    public StatusEffectType effectType;

    [Header("Background")]
    public MessagePopupBackgroundMode backgroundMode;
    [Tooltip("SolidColor mode.")]
    public Color backgroundColor;
    [Tooltip("Sprite mode. PNG with transparent corners (pill shape baked in).")]
    public Sprite backgroundSprite;

    [Header("Text")]
    public Color textColor;
    public Color outlineColor;

    public bool UsesSpriteBackground =>
        backgroundMode == MessagePopupBackgroundMode.Sprite && backgroundSprite != null;
}

/// <summary>
/// Inspector-driven colors and sprites for status-ailment grant popups (<see cref="DamagePopup"/>).
/// </summary>
[CreateAssetMenu(fileName = "StatusEffectPopupSettings", menuName = "DivineField/UI/Status Effect Popup Settings")]
public sealed class StatusEffectPopupSettings : ScriptableObject
{
    [SerializeField] private StatusEffectPopupStyleEntry[] entries;

    public bool TryGetEntry(StatusEffectType type, out StatusEffectPopupStyleEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].effectType == type)
                {
                    entry = entries[i];
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    public StatusEffectPopupStyleEntry GetEntryOrDefault(StatusEffectType type)
    {
        if (TryGetEntry(type, out var entry)) return entry;
        foreach (var e in DefaultEntries())
        {
            if (e.effectType == type) return e;
        }
        return FallbackEntry();
    }

    private static StatusEffectPopupStyleEntry FallbackEntry()
    {
        return Entry(
            StatusEffectType.None,
            new Color(0.18f, 0.18f, 0.22f, 0.92f),
            new Color(1f, 0.88f, 0.35f));
    }

    private static StatusEffectPopupStyleEntry[] DefaultEntries()
    {
        return new[]
        {
            Entry(StatusEffectType.Sickness,
                new Color(0.12f, 0.28f, 0.14f, 0.93f), new Color(0.65f, 1f, 0.55f)),
            Entry(StatusEffectType.SevereSickness,
                new Color(0.22f, 0.18f, 0.32f, 0.93f), new Color(0.85f, 0.55f, 1f)),
            Entry(StatusEffectType.PurgatorySickness,
                new Color(0.35f, 0.1f, 0.12f, 0.93f), new Color(1f, 0.45f, 0.35f)),
            Entry(StatusEffectType.ParadiseSickness,
                new Color(0.4f, 0.28f, 0.55f, 0.93f), new Color(1f, 0.75f, 0.95f)),
            Entry(StatusEffectType.Weaken,
                new Color(0.25f, 0.22f, 0.2f, 0.93f), new Color(0.85f, 0.75f, 0.6f)),
            Entry(StatusEffectType.EyeStrain,
                new Color(0.28f, 0.3f, 0.18f, 0.93f), new Color(1f, 0.95f, 0.45f)),
            Entry(StatusEffectType.ClusterHeadache,
                new Color(0.32f, 0.15f, 0.15f, 0.93f), new Color(1f, 0.5f, 0.45f)),
            Entry(StatusEffectType.Smoke,
                new Color(0.2f, 0.2f, 0.2f, 0.93f), new Color(0.85f, 0.85f, 0.85f)),
            Entry(StatusEffectType.Misfortune,
                new Color(0.22f, 0.16f, 0.12f, 0.93f), new Color(1f, 0.65f, 0.35f)),
            Entry(StatusEffectType.Freeze,
                new Color(0.12f, 0.18f, 0.38f, 0.93f), new Color(0.55f, 0.75f, 1f)),
            Entry(StatusEffectType.Fog,
                new Color(0.18f, 0.22f, 0.28f, 0.93f), new Color(0.75f, 0.88f, 1f)),
            Entry(StatusEffectType.Confusion,
                new Color(0.32f, 0.2f, 0.38f, 0.93f), new Color(1f, 0.55f, 0.95f)),
            Entry(StatusEffectType.Intervention,
                new Color(0.18f, 0.28f, 0.22f, 0.93f), new Color(0.55f, 1f, 0.75f)),
            Entry(StatusEffectType.CurseBind,
                new Color(0.15f, 0.1f, 0.22f, 0.93f), new Color(0.75f, 0.45f, 1f)),
            Entry(StatusEffectType.Restraint,
                new Color(0.22f, 0.22f, 0.28f, 0.93f), new Color(0.95f, 0.95f, 1f)),
            Entry(StatusEffectType.RandomOneAilment,
                new Color(0.22f, 0.12f, 0.32f, 0.93f), new Color(0.95f, 0.65f, 1f)),
        };
    }

    private static StatusEffectPopupStyleEntry Entry(StatusEffectType type, Color bg, Color fg)
    {
        return new StatusEffectPopupStyleEntry
        {
            effectType = type,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = bg,
            backgroundSprite = null,
            textColor = fg,
            outlineColor = Color.white,
        };
    }

    private static StatusEffectPopupSettings _runtimeFallback;

    public static StatusEffectPopupSettings GetRuntimeFallback()
    {
        if (_runtimeFallback != null) return _runtimeFallback;
        var loaded = Resources.Load<StatusEffectPopupSettings>("StatusEffectPopupSettings");
        if (loaded != null) return loaded;
        _runtimeFallback = CreateInstance<StatusEffectPopupSettings>();
        _runtimeFallback.name = "StatusEffectPopupSettings (Runtime Fallback)";
        _runtimeFallback.entries = DefaultEntries();
        return _runtimeFallback;
    }
}
