using System;
using UnityEngine;

public enum MessagePopupBackgroundMode
{
    /// <summary>Root Image color + prefab default rounded sprite.</summary>
    SolidColor = 0,
    /// <summary>Custom sprite on root Image (alpha defines pill shape).</summary>
    Sprite = 1,
}

[Serializable]
public struct MessagePopupStyleEntry
{
    public MessagePopupKind kind;
    [TextArea(1, 2)] public string message;

    [Header("Background")]
    public MessagePopupBackgroundMode backgroundMode;
    [Tooltip("SolidColor mode. Ignored when backgroundMode is Sprite and backgroundSprite is set.")]
    public Color backgroundColor;
    [Tooltip("Sprite mode. PNG with transparent corners (pill shape baked in). See MessagePopup panel size.")]
    public Sprite backgroundSprite;

    [Header("Text")]
    public Color textColor;
    public Color outlineColor;

    public bool UsesSpriteBackground =>
        backgroundMode == MessagePopupBackgroundMode.Sprite && backgroundSprite != null;
}

/// <summary>
/// Inspector-driven colors, sprites, and text for <see cref="MessagePopup"/> variants.
/// </summary>
[CreateAssetMenu(fileName = "MessagePopupSettings", menuName = "DivineField/UI/Message Popup Settings")]
public sealed class MessagePopupSettings : ScriptableObject
{
    public const string ParadiseHeavenBackgroundResourcePath = "UI/MessagePopup/ParadiseHeavenBackground";

    [SerializeField] private MessagePopupStyleEntry[] entries;

    [Header("Motion / Timing")]
    [SerializeField] private PopupMotionTiming motion = PopupMotionTiming.MessageDefaults;

    public PopupMotionTiming MotionOrDefault => motion.WithDefaults(PopupMotionTiming.MessageDefaults);

    public float FloatSpeed => MotionOrDefault.floatSpeed;
    public float FadeDuration => MotionOrDefault.fadeDuration;
    public int PostPopupIntervalMs => MotionOrDefault.postPopupIntervalMs;

    /// <summary>Wait for message popup fade + post interval (uses this asset's timing).</summary>
    public static async System.Threading.Tasks.Task WaitAfterLifetimeAsync(
        float fadeSecondsReturnedFromShow,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var timing = GetRuntimeFallback().MotionOrDefault;
        float fade = fadeSecondsReturnedFromShow > 0.001f
            ? fadeSecondsReturnedFromShow
            : timing.fadeDuration;
        await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(fade), cancellationToken);
        await System.Threading.Tasks.Task.Delay(timing.postPopupIntervalMs, cancellationToken);
    }

    public bool TryGetEntry(MessagePopupKind kind, out MessagePopupStyleEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].kind == kind)
                {
                    entry = ResolveRuntimeFields(entries[i]);
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    public MessagePopupStyleEntry GetEntryOrDefault(MessagePopupKind kind)
    {
        if (TryGetEntry(kind, out var entry)) return entry;
        foreach (var e in DefaultEntries())
        {
            if (e.kind == kind) return ResolveRuntimeFields(e);
        }
        return default;
    }

    private static Sprite _cachedParadiseHeavenBackgroundSprite;

    /// <summary>Fills optional Resources sprite when Inspector slot is empty. Call at runtime only.</summary>
    public static MessagePopupStyleEntry ResolveRuntimeFields(MessagePopupStyleEntry entry)
    {
        if (entry.backgroundMode == MessagePopupBackgroundMode.Sprite
            && entry.backgroundSprite == null
            && entry.kind == MessagePopupKind.ParadiseHeavenState)
        {
            if (_cachedParadiseHeavenBackgroundSprite == null)
                _cachedParadiseHeavenBackgroundSprite =
                    Resources.Load<Sprite>(ParadiseHeavenBackgroundResourcePath);
            entry.backgroundSprite = _cachedParadiseHeavenBackgroundSprite;
        }
        return entry;
    }

    private static MessagePopupStyleEntry[] DefaultEntries()
    {
        return new[]
        {
            Entry(MessagePopupKind.ShivaFreezeApplied, "\u8eab\u4f53\u304c\u51cd\u308a\u3064\u304f...!",
                new Color(0.12f, 0.18f, 0.38f, 0.93f), new Color(0f, 240f / 255f, 1f), Color.black),
            Entry(MessagePopupKind.FreezeCannotMove, "\u51cd\u308a\u3064\u3044\u3066\u52d5\u3051\u306a\u3044",
                new Color(0.12f, 0.18f, 0.38f, 0.93f), new Color(0.55f, 0.85f, 1f), Color.black),
            Entry(MessagePopupKind.FreezeMelted, "\u6c37\u304c\u6eb6\u3051\u305f\uff01",
                new Color(0.12f, 0.18f, 0.38f, 0.93f), new Color(0.55f, 0.85f, 1f), Color.black),
            Entry(MessagePopupKind.DiseaseErodeBody, "\u75c5\u304c\u4f53\u3092\u8755\u3080",
                new Color(0f, 0f, 0f, 0.94f), Color.white, Color.black),
            Entry(MessagePopupKind.DiseaseWorsened, "\u4f53\u8abf\u304c\u60aa\u304f\u306a\u3063\u305f",
                new Color(0f, 0f, 0f, 0.94f), Color.white, Color.black),
            Entry(MessagePopupKind.DiseasePoisonFlipped, "\u6bd2\u304c\u88cf\u8fd4\u3063\u305f\uff1f\uff01",
                new Color(0f, 0f, 0f, 0.94f), Color.white, Color.black),
            HeavenEntry(),
            Entry(MessagePopupKind.ParryFailedReturn, "\u3053\u3061\u3089\u306b\u843d\u3061\u3066\u304f\u308b\uff01",
                new Color(247f / 255f, 211f / 255f, 88f / 255f, 0.96f), Color.white, Color.black),
            Entry(MessagePopupKind.InterventionAttack, "\u672a\u77e5\u306e\u529b\u304c\u653e\u305f\u308c\u308b",
                new Color(0.18f, 0.28f, 0.22f, 0.93f), new Color(0.55f, 1f, 0.75f), Color.black),
            Entry(MessagePopupKind.PhoenixBlessing, "\u4e0d\u6b7b\u9ce5\u306e\u52a0\u8b77",
                new Color(0.85f, 0.35f, 0.08f, 0.94f), new Color(1f, 0.92f, 0.55f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterEruption, "\u4e16\u754c\u304c\u7126\u571f\u306b\u5305\u307e\u308c\u308b",
                new Color(0.45f, 0.08f, 0.02f, 0.94f), new Color(1f, 0.72f, 0.2f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterSolarEclipse, "\u6697\u9ed2\u306e\u592a\u967d\u304c\u88c1\u304d\u3092\u4e0b\u3059",
                new Color(0.08f, 0.06f, 0.14f, 0.94f), new Color(0.75f, 0.55f, 1f), Color.white),
            DisasterEntry(MessagePopupKind.DisasterLunarEclipse, "\u6697\u95c7\u306e\u6708\u304c\u529b\u3092\u596a\u3046",
                new Color(0.06f, 0.06f, 0.12f, 0.94f), new Color(0.85f, 0.85f, 0.95f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterKannaduki, "\u529b\u304c\u66b4\u8d70\u3059\u308b\u2026!?",
                new Color(0.35f, 0.08f, 0.12f, 0.94f), new Color(1f, 0.45f, 0.35f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterBlackMonday, "\u30b9\u30c8\u30c3\u30d7\u5b89\u3060\uff01",
                new Color(0.05f, 0.18f, 0.08f, 0.94f), new Color(0.55f, 1f, 0.55f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterRealityBending, "\u4e16\u754c\u304c\u66f8\u304d\u63db\u3048\u3089\u308c\u308b",
                new Color(0.22f, 0.08f, 0.45f, 0.94f), new Color(0.85f, 0.65f, 1f), Color.white),
            DisasterEntry(MessagePopupKind.DisasterRampageZantetsuken, "\u30aa\u30fc\u30c7\u30a3\u30f3\u306e\u6012\u308a",
                new Color(0.18f, 0.2f, 0.28f, 0.94f), new Color(0.85f, 0.9f, 1f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterMiracleArk, "\u65b9\u821f\u304c\u5149\u3092\u653e\u3064",
                new Color(0.12f, 0.28f, 0.55f, 0.94f), new Color(0.95f, 0.98f, 1f), new Color(0.1f, 0.25f, 0.55f)),
            DisasterEntry(MessagePopupKind.DisasterManaStream, "\u9b54\u529b\u304c\u6e26\u3092\u5dfb\u304f",
                new Color(0.08f, 0.15f, 0.42f, 0.94f), new Color(0.55f, 0.85f, 1f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterChaosAttractor, "\u30ab\u30aa\u30b9\u3092\u3053\u3048\u3066\u7d42\u672b\u304c\u8fd1\u3065\u304f",
                new Color(0.18f, 0.05f, 0.22f, 0.94f), new Color(0.95f, 0.55f, 0.95f), Color.black),
            DisasterEntry(MessagePopupKind.DisasterInfection, "\u7149\u7363\u306e\u98a8\u304c\u5439\u304d\u8352\u308c\u308b",
                new Color(0.28f, 0.05f, 0.02f, 0.94f), new Color(1f, 0.55f, 0.2f), Color.black),
        };
    }

    private static MessagePopupStyleEntry DisasterEntry(
        MessagePopupKind kind, string message, Color bg, Color text, Color outline)
        => Entry(kind, message, bg, text, outline);

    private static MessagePopupStyleEntry HeavenEntry()
    {
        return new MessagePopupStyleEntry
        {
            kind = MessagePopupKind.ParadiseHeavenState,
            message = "\u30d8\u30d6\u30f3\u72b6\u614b\uff01",
            backgroundMode = MessagePopupBackgroundMode.Sprite,
            backgroundColor = new Color(0.4f, 0.28f, 0.55f, 0.93f),
            backgroundSprite = null,
            textColor = new Color(1f, 0.75f, 0.88f),
            outlineColor = Color.white,
        };
    }

    private static MessagePopupStyleEntry Entry(
        MessagePopupKind kind, string message, Color bg, Color text, Color outline)
    {
        return new MessagePopupStyleEntry
        {
            kind = kind,
            message = message,
            backgroundMode = MessagePopupBackgroundMode.SolidColor,
            backgroundColor = bg,
            backgroundSprite = null,
            textColor = text,
            outlineColor = outline,
        };
    }

    private static MessagePopupSettings _runtimeFallback;

    public static MessagePopupSettings GetRuntimeFallback()
    {
        if (_runtimeFallback != null) return _runtimeFallback;
        var loaded = Resources.Load<MessagePopupSettings>("MessagePopupSettings");
        if (loaded != null) return loaded;
        _runtimeFallback = CreateInstance<MessagePopupSettings>();
        _runtimeFallback.name = "MessagePopupSettings (Runtime Fallback)";
        _runtimeFallback.entries = DefaultEntries();
        _runtimeFallback.motion = PopupMotionTiming.MessageDefaults;
        return _runtimeFallback;
    }
}
