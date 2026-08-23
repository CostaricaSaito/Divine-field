using System;
using UnityEngine;

/// <summary>Persistent app settings (SE/BGM volume, language). Loaded before first scene.</summary>
public static class GameSettings
{
    public const int VolumeStepCount = 10;
    public const float VolumeStep = 0.1f;

    const string SeVolumeKey = "DivineField.SeVolumeStep";
    const string BgmVolumeKey = "DivineField.BgmVolumeStep";
    const string LanguageKey = "DivineField.Language";

    static bool _loaded;

    public static float SeVolume { get; private set; } = 1f;
    public static float BgmVolume { get; private set; } = 1f;
    public static GameLanguage Language { get; private set; } = GameLanguage.Japanese;

    public static event Action SeVolumeChanged;
    public static event Action BgmVolumeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Load();
    }

    public static void Load()
    {
        var seStep = MigrateLegacyVolumeStep(PlayerPrefs.GetInt(SeVolumeKey, VolumeStepCount));
        var bgmStep = MigrateLegacyVolumeStep(PlayerPrefs.GetInt(BgmVolumeKey, VolumeStepCount));
        var language = PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Japanese);

        SeVolume = StepToVolume(seStep);
        BgmVolume = StepToVolume(bgmStep);
        Language = Enum.IsDefined(typeof(GameLanguage), language)
            ? (GameLanguage)language
            : GameLanguage.Japanese;
        _loaded = true;
    }

    public static int GetSeVolumeStep() => VolumeToStep(SeVolume);
    public static int GetBgmVolumeStep() => VolumeToStep(BgmVolume);

    public static void SetSeVolumeStep(int step)
    {
        step = ClampStep(step);
        var volume = StepToVolume(step);
        if (Mathf.Approximately(SeVolume, volume) && _loaded)
            return;

        SeVolume = volume;
        PlayerPrefs.SetInt(SeVolumeKey, step);
        PlayerPrefs.Save();
        SeVolumeChanged?.Invoke();
    }

    public static void SetBgmVolumeStep(int step)
    {
        step = ClampStep(step);
        var volume = StepToVolume(step);
        if (Mathf.Approximately(BgmVolume, volume) && _loaded)
            return;

        BgmVolume = volume;
        PlayerPrefs.SetInt(BgmVolumeKey, step);
        PlayerPrefs.Save();
        BgmVolumeChanged?.Invoke();
    }

    public static void SetLanguage(GameLanguage language)
    {
        if (Language == language && _loaded)
            return;

        Language = language;
        PlayerPrefs.SetInt(LanguageKey, (int)language);
        PlayerPrefs.Save();
    }

    public static float ScaleBgmVolume(float baseVolume) => baseVolume * BgmVolume;

    public static int VolumeToStep(float volume)
        => ClampStep(Mathf.RoundToInt(Mathf.Clamp01(volume) / VolumeStep));

    public static float StepToVolume(int step) => ClampStep(step) * VolumeStep;

    public static string FormatVolumePercent(float volume)
        => $"{Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f)}%";

    static int ClampStep(int step) => Mathf.Clamp(step, 0, VolumeStepCount);

    /// <summary>Converts legacy 5% steps (0..20) to 10% steps (0..10).</summary>
    static int MigrateLegacyVolumeStep(int step)
    {
        if (step <= VolumeStepCount)
            return ClampStep(step);

        return ClampStep(Mathf.RoundToInt(step / 2f));
    }
}

public enum GameLanguage
{
    Japanese = 0,
    English = 1,
    Korean = 2,
    TraditionalChinese = 3,
    SimplifiedChinese = 4,
}
