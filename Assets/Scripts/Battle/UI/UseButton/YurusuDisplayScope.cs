using System;

/// <summary>
/// CPU が防御0枚で戦闘解決中のみ、相手側「許す」装飾を表示する。
/// using + await の try/finally と同等（Dispose で必ず非表示）。
/// </summary>
public sealed class YurusuDisplayScope : IDisposable
{
    private static int _activeDepth;

    private readonly bool _shown;

    /// <summary>敵側「許す」表示スコープが1つ以上開いている。</summary>
    public static bool IsActive => _activeDepth > 0;

    private YurusuDisplayScope(bool show)
    {
        _shown = show;
        if (_shown)
        {
            _activeDepth++;
            SoundEffectCatalogSO.Get()?.PlayYurusu();
            BattleUIManager.I?.ShowYurusuDisplay();
        }
    }

    public static YurusuDisplayScope ShowIf(bool condition) => new YurusuDisplayScope(condition);

    public void Dispose()
    {
        if (!_shown) return;
        _activeDepth = Math.Max(0, _activeDepth - 1);
        BattleUIManager.I?.HideYurusuButton();
    }
}
