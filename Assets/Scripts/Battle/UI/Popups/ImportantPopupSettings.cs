﻿using System;
using UnityEngine;

[Serializable]
public struct ImportantPopupStyleEntry
{
    public ImportantPopupKind kind;
    [TextArea(1, 2)] public string message;

    [Header("Background")]
    public MessagePopupBackgroundMode backgroundMode;
    [Tooltip("SolidColor: tint GoldFrame. Sprite: replace frame sprite.")]
    public Color backgroundColor;
    public Sprite backgroundSprite;

    [Header("Text")]
    public Color textColor;
    public Color outlineColor;
    [Tooltip("0 = keep prefab font size.")]
    [Min(0f)] public float fontSize;

    [Header("Sound (optional)")]
    [Tooltip("Addressable path e.g. Assets/SE/サイレン.mp3")]
    public string showSoundAddress;

    public bool UsesSpriteBackground =>
        backgroundMode == MessagePopupBackgroundMode.Sprite && backgroundSprite != null;
}

[Serializable]
public struct ImportantPopupEntranceTiming
{
    [Tooltip("Final position offset downward at spawn (px).")]
    public float entryOffsetY;
    public float riseDuration;
    public float holdDuration;
    public float fadeDuration;
    [Tooltip("Single-line horizontal squeeze limit (px).")]
    public float maxTextWidthPx;

    public static ImportantPopupEntranceTiming Defaults => new ImportantPopupEntranceTiming
    {
        entryOffsetY = 80f,
        riseDuration = 0.45f,
        holdDuration = 0.85f,
        fadeDuration = 0.25f,
        maxTextWidthPx = 900f,
    };

    public ImportantPopupEntranceTiming WithDefaults(ImportantPopupEntranceTiming defaults)
    {
        var r = this;
        if (r.entryOffsetY <= 0f) r.entryOffsetY = defaults.entryOffsetY;
        if (r.riseDuration <= 0f) r.riseDuration = defaults.riseDuration;
        if (r.holdDuration < 0f) r.holdDuration = defaults.holdDuration;
        if (r.fadeDuration <= 0f) r.fadeDuration = defaults.fadeDuration;
        if (r.maxTextWidthPx <= 0f) r.maxTextWidthPx = defaults.maxTextWidthPx;
        return r;
    }
}

/// <summary>
/// Inspector-driven styles and motion for <see cref="ImportantPopup"/>.
/// </summary>
[CreateAssetMenu(fileName = "ImportantPopupSettings", menuName = "DivineField/UI/Important Popup Settings")]
public sealed class ImportantPopupSettings : ScriptableObject
{
    public const string DefaultSirenSoundAddress = "Assets/SE/サイレン.mp3";

    [SerializeField] private ImportantPopupStyleEntry[] entries;
    [SerializeField] private ImportantPopupEntranceTiming entrance = ImportantPopupEntranceTiming.Defaults;

    public ImportantPopupEntranceTiming EntranceOrDefault =>
        entrance.WithDefaults(ImportantPopupEntranceTiming.Defaults);

    public bool TryGetEntry(ImportantPopupKind kind, out ImportantPopupStyleEntry entry)
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

    public ImportantPopupStyleEntry GetEntryOrDefault(ImportantPopupKind kind)
    {
        if (TryGetEntry(kind, out var entry)) return entry;
        foreach (var e in DefaultEntries())
        {
            if (e.kind == kind) return e;
        }

        return new ImportantPopupStyleEntry
        {
            kind = ImportantPopupKind.RuntimeCustom,
            textColor = Color.white,
            outlineColor = Color.black,
        };
    }

    public static async System.Threading.Tasks.Task WaitAfterLifetimeAsync(
        float sequenceSeconds,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (sequenceSeconds > 0.001f)
            await System.Threading.Tasks.Task.Delay(
                System.TimeSpan.FromSeconds(sequenceSeconds), cancellationToken);
    }

    private static ImportantPopupStyleEntry[] DefaultEntries()
    {
        return new[]
        {
            Entry(ImportantPopupKind.DisasterIntro, "\u7a7a\u306f\u88c2\u3051\u3001\u5927\u5730\u304c\u9707\u3048\u308b",
                new Color(0.95f, 0.45f, 0.22f), Color.white, DefaultSirenSoundAddress),
            DisasterEntry(ImportantPopupKind.DisasterEruption, "\u4e16\u754c\u304c\u7126\u571f\u306b\u5305\u307e\u308c\u308b",
                new Color(1f, 0.72f, 0.2f), Color.black),
            DisasterEntry(ImportantPopupKind.DisasterSolarEclipse, "\u6697\u9ed2\u306e\u592a\u967d\u304c\u88c1\u304d\u3092\u4e0b\u3059",
                new Color(0.75f, 0.55f, 1f), Color.white),
            DisasterEntry(ImportantPopupKind.DisasterLunarEclipse, "\u6697\u95c7\u306e\u6708\u304c\u529b\u3092\u596a\u3046",
                new Color(0.85f, 0.85f, 0.95f), Color.black),
            DisasterEntry(ImportantPopupKind.DisasterKannaduki, "\u529b\u304c\u66b4\u8d70\u3059\u308b\u2026!?",
                new Color(1f, 0.45f, 0.35f), Color.black),
            DisasterEntry(ImportantPopupKind.DisasterBlackMonday, "\u30b9\u30c8\u30c3\u30d7\u5b89\u3060\uff01",
                new Color(0.55f, 1f, 0.55f), Color.black),
            DisasterEntry(ImportantPopupKind.DisasterRealityBending, "\u4e16\u754c\u304c\u66f8\u304d\u63db\u3048\u3089\u308c\u308b",
                new Color(0.85f, 0.65f, 1f), Color.white),
            DisasterEntry(ImportantPopupKind.DisasterRampageZantetsuken, "\u30aa\u30fc\u30c7\u30a3\u30f3\u306e\u6012\u308a",
                new Color(0.85f, 0.9f, 1f), Color.black),
            DisasterEntry(ImportantPopupKind.DisasterMiracleArk, "\u65b9\u821f\u304c\u5149\u3092\u653e\u3064",
                new Color(0.95f, 0.98f, 1f), new Color(0.1f, 0.25f, 0.55f)),
            DisasterEntry(ImportantPopupKind.DisasterManaStream, "\u9b54\u529b\u304c\u6e26\u3092\u5dfb\u304f",
                new Color(0.55f, 0.85f, 1f), Color.black),
            DisasterEntry(ImportantPopupKind.DisasterChaosAttractor, "\u30ab\u30aa\u30b9\u3092\u3053\u3048\u3066\u7d42\u672b\u304c\u8fd1\u3065\u304f",
                new Color(0.95f, 0.55f, 0.95f), Color.black),
            DisasterEntry(ImportantPopupKind.DisasterInfection, "\u7149\u7363\u306e\u98a8\u304c\u5439\u304d\u8352\u308c\u308b",
                new Color(1f, 0.55f, 0.2f), Color.black),
            Entry(ImportantPopupKind.ArchMagicCast, "\u9b54\u529b\u304c\u5439\u304d\u8352\u308c\u308b",
                new Color(0.75f, 0.45f, 0.95f), Color.white, null),
            Entry(ImportantPopupKind.ArchMagicFocus, "\u9b54\u529b\u3092\u96c6\u4e2d\u3057\u308d\uff01",
                new Color(0.55f, 0.7f, 0.95f), Color.white, null),
            Entry(ImportantPopupKind.ArchMagicRelease, string.Empty,
                new Color(0.95f, 0.85f, 0.3f), Color.black, null),
        };
    }

    private static ImportantPopupStyleEntry DisasterEntry(
        ImportantPopupKind kind, string message, Color text, Color outline)
        => Entry(kind, message, text, outline, null);

    private static ImportantPopupStyleEntry Entry(
        ImportantPopupKind kind,
        string message,
        Color text,
        Color outline,
        string sound)
    {
        return new ImportantPopupStyleEntry
        {
            kind = kind,
            message = message,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = Color.white,
            backgroundSprite = null,
            textColor = text,
            outlineColor = outline,
            fontSize = 0f,
            showSoundAddress = sound ?? string.Empty,
        };
    }

    private static ImportantPopupSettings _runtimeFallback;

    public static ImportantPopupSettings GetRuntimeFallback()
    {
        if (_runtimeFallback != null) return _runtimeFallback;
        var loaded = Resources.Load<ImportantPopupSettings>("ImportantPopupSettings");
        if (loaded != null) return loaded;
        _runtimeFallback = CreateInstance<ImportantPopupSettings>();
        _runtimeFallback.name = "ImportantPopupSettings (Runtime Fallback)";
        _runtimeFallback.entries = DefaultEntries();
        _runtimeFallback.entrance = ImportantPopupEntranceTiming.Defaults;
        return _runtimeFallback;
    }
}
