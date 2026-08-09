using UnityEngine;

/// <summary>
/// 状態異常ポップアップ用の表示名・配色。配色は <see cref="StatusEffectPopupSettings"/>（Unity Inspector）。
/// </summary>
public static class StatusEffectPresentation
{
    /// <summary>公式名（病・衰弱 …）。None は空文字。</summary>
    public static string GetDisplayName(StatusEffectType type)
    {
        if (type == StatusEffectType.RandomOneAilment)
            return "RANDOM（全状態異常から1つ）";
        if (type == StatusEffectType.Kannaduki)
            return "神無月";
        int id = StatusEffectCatalog.ToOfficialId(type);
        if (id < 1 || id > 15) return string.Empty;
        return StatusEffectCatalog.OfficialDisplayNames[id - 1];
    }

    /// <summary>ポップアップのパネル背景とメイン文字色。</summary>
    public static void GetPopupColors(StatusEffectType type, out Color panelBackground, out Color textFill)
    {
        var entry = GetPopupStyle(type);
        panelBackground = entry.backgroundColor;
        textFill = entry.textColor;
    }

    public static StatusEffectPopupStyleEntry GetPopupStyle(StatusEffectType type) =>
        StatusEffectPopupSettings.GetRuntimeFallback().GetEntryOrDefault(type);
}
