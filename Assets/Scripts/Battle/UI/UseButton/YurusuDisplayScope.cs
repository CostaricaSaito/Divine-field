using System;

/// <summary>
/// CPU が防御0枚で戦闘解決中のみ、相手側「許す」装飾を表示する。
/// using + await の try/finally と同等（Dispose で必ず非表示）。
/// </summary>
public sealed class YurusuDisplayScope : IDisposable
{
    private readonly bool _shown;

    private YurusuDisplayScope(bool show)
    {
        _shown = show;
        if (_shown)
            BattleUIManager.I?.ShowYurusuDisplay();
    }

    public static YurusuDisplayScope ShowIf(bool condition) => new YurusuDisplayScope(condition);

    public void Dispose()
    {
        if (_shown)
            BattleUIManager.I?.HideYurusuButton();
    }
}
