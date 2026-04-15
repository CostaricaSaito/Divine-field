using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状態異常ポップアップ用の表示名・配色。増やすときはここだけ触ればよい。
/// </summary>
public static class StatusEffectPresentation
{
    /// <summary>公式名（病・衰弱 …）。None は空文字。</summary>
    public static string GetDisplayName(StatusEffectType type)
    {
        int id = StatusEffectCatalog.ToOfficialId(type);
        if (id < 1 || id > 15) return string.Empty;
        return StatusEffectCatalog.OfficialDisplayNames[id - 1];
    }

    /// <summary>ポップアップのパネル背景とメイン文字色。</summary>
    public static void GetPopupColors(StatusEffectType type, out Color panelBackground, out Color textFill)
    {
        if (!_styles.TryGetValue(type, out var pair))
        {
            panelBackground = new Color(0.18f, 0.18f, 0.22f, 0.92f);
            textFill = new Color(1f, 0.88f, 0.35f);
            return;
        }

        panelBackground = pair.bg;
        textFill = pair.fg;
    }

    private static readonly Dictionary<StatusEffectType, (Color bg, Color fg)> _styles =
        new Dictionary<StatusEffectType, (Color bg, Color fg)>
        {
            // 病系：緑〜紫系
            { StatusEffectType.Sickness, (new Color(0.12f, 0.28f, 0.14f, 0.93f), new Color(0.65f, 1f, 0.55f)) },
            { StatusEffectType.SevereSickness, (new Color(0.22f, 0.18f, 0.32f, 0.93f), new Color(0.85f, 0.55f, 1f)) },
            { StatusEffectType.PurgatorySickness, (new Color(0.35f, 0.1f, 0.12f, 0.93f), new Color(1f, 0.45f, 0.35f)) },
            { StatusEffectType.ParadiseSickness, (new Color(0.4f, 0.28f, 0.55f, 0.93f), new Color(1f, 0.75f, 0.95f)) },
            { StatusEffectType.Weaken, (new Color(0.25f, 0.22f, 0.2f, 0.93f), new Color(0.85f, 0.75f, 0.6f)) },
            { StatusEffectType.EyeStrain, (new Color(0.28f, 0.3f, 0.18f, 0.93f), new Color(1f, 0.95f, 0.45f)) },
            { StatusEffectType.ClusterHeadache, (new Color(0.32f, 0.15f, 0.15f, 0.93f), new Color(1f, 0.5f, 0.45f)) },
            { StatusEffectType.Smoke, (new Color(0.2f, 0.2f, 0.2f, 0.93f), new Color(0.85f, 0.85f, 0.85f)) },
            { StatusEffectType.Misfortune, (new Color(0.22f, 0.16f, 0.12f, 0.93f), new Color(1f, 0.65f, 0.35f)) },
            { StatusEffectType.Seal, (new Color(0.12f, 0.18f, 0.38f, 0.93f), new Color(0.55f, 0.75f, 1f)) },
            { StatusEffectType.Fog, (new Color(0.18f, 0.22f, 0.28f, 0.93f), new Color(0.75f, 0.88f, 1f)) },
            { StatusEffectType.Confusion, (new Color(0.32f, 0.2f, 0.38f, 0.93f), new Color(1f, 0.55f, 0.95f)) },
            { StatusEffectType.Intervention, (new Color(0.18f, 0.28f, 0.22f, 0.93f), new Color(0.55f, 1f, 0.75f)) },
            { StatusEffectType.CurseBind, (new Color(0.15f, 0.1f, 0.22f, 0.93f), new Color(0.75f, 0.45f, 1f)) },
            { StatusEffectType.Restraint, (new Color(0.22f, 0.22f, 0.28f, 0.93f), new Color(0.95f, 0.95f, 1f)) },
        };
}
