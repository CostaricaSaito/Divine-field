using System;
using UnityEngine;

[Serializable]
public struct StatusEffectDescriptionEntry
{
    public StatusEffectType effectType;

    [TextArea(2, 8)]
    public string description;
}

/// <summary>
/// Hold-detail UI: player-facing descriptions and severity badge colors.
/// Name fill/outline colors come from <see cref="StatusEffectPopupSettings"/> (same as grant popups).
/// </summary>
[CreateAssetMenu(fileName = "StatusEffectDetailSettings", menuName = "DivineField/Status/Status Effect Detail Settings")]
public sealed class StatusEffectDetailSettings : ScriptableObject
{
    [SerializeField] private StatusEffectDescriptionEntry[] entries;

    [Header("Severity badge (StatusEffectSeverityBG)")]
    [SerializeField] private Color mildSeverityBackground = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color severeSeverityBackground = new Color(0.85f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color specialSeverityBackground = new Color(1f, 0f, 0.82f, 1f);

    private static StatusEffectDetailSettings _runtimeFallback;

    public static StatusEffectDetailSettings GetRuntimeFallback()
    {
        if (_runtimeFallback != null) return _runtimeFallback;

        var loaded = Resources.Load<StatusEffectDetailSettings>("StatusEffectDetailSettings");
        if (loaded != null) return loaded;

        _runtimeFallback = CreateInstance<StatusEffectDetailSettings>();
        _runtimeFallback.name = "StatusEffectDetailSettings (Runtime Fallback)";
        _runtimeFallback.entries = BuildDefaultDescriptionEntries();
        return _runtimeFallback;
    }

    public string GetDescription(StatusEffectType type)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].effectType == type && !string.IsNullOrWhiteSpace(entries[i].description))
                    return entries[i].description;
            }
        }

        foreach (var e in BuildDefaultDescriptionEntries())
        {
            if (e.effectType == type) return e.description;
        }

        return "説明は準備中です。";
    }

    public Color GetSeverityBackground(StatusEffectAilmentCategory category)
    {
        return category switch
        {
            StatusEffectAilmentCategory.Severe => severeSeverityBackground,
            StatusEffectAilmentCategory.Special => specialSeverityBackground,
            _ => mildSeverityBackground,
        };
    }

    public static string GetSeverityLabel(StatusEffectAilmentCategory category)
    {
        return category switch
        {
            StatusEffectAilmentCategory.Severe => "重症",
            StatusEffectAilmentCategory.Special => "特殊",
            _ => "軽症",
        };
    }

    public static StatusEffectDescriptionEntry[] BuildDefaultDescriptionEntries()
    {
        return new[]
        {
            Desc(StatusEffectType.Sickness,
                "ターン終了時にダメージを受けます。エンドフェイズで段階が進むと重病へ移行します。"),
            Desc(StatusEffectType.SevereSickness,
                "ターン終了時により大きなダメージを受けます。進行すると煉獄病へ移行します。"),
            Desc(StatusEffectType.PurgatorySickness,
                "ターン終了時に強いダメージを受けます。進行すると楽園病へ移行します。"),
            Desc(StatusEffectType.ParadiseSickness,
                "ターン終了時に特大ダメージを受けます。病系の最終段階です。"),
            Desc(StatusEffectType.Weaken,
                "与えるダメージが減少します。"),
            Desc(StatusEffectType.EyeStrain,
                "手札の一部が見えにくくなります。"),
            Desc(StatusEffectType.ClusterHeadache,
                "ターン終了時などにダメージを受けることがあります。"),
            Desc(StatusEffectType.Smoke,
                "命中率が下がります。"),
            Desc(StatusEffectType.Misfortune,
                "不利な効果を受けやすくなります。"),
            Desc(StatusEffectType.Freeze,
                "攻撃選択などの行動が制限されます。ターン経過で自然解除される場合があります。"),
            Desc(StatusEffectType.Fog,
                "自分に付与されるとステータスや手札数の表示が「？」に隠されます。"),
            Desc(StatusEffectType.Confusion,
                "カードの選択先が意図とずれることがあります。"),
            Desc(StatusEffectType.Intervention,
                "反射や特殊な防御連鎖に干渉します。解除が難しい重症です。"),
            Desc(StatusEffectType.CurseBind,
                "行動やカード使用が呪いにより制限されます。"),
            Desc(StatusEffectType.Restraint,
                "移動や経済アクションなどが制限され、体が重い演出が入ることがあります。"),
            Desc(StatusEffectType.Kannaduki,
                "物理攻撃の威力が上がります。試合終了まで解除できない特殊状態です。"),
            Desc(StatusEffectType.Zantestuken,
                "次の相手向け攻撃が防御を貫通します。命中で消費される特殊状態です。"),
            Desc(StatusEffectType.MilleniumKingdom,
                "相手の魔法・属性攻撃を無効化し続けます。試合終了までの特殊状態です。"),
            Desc(StatusEffectType.DiabolicEmission,
                "次の相手向け攻撃が闇属性として解決されます。命中で消費される特殊状態です。"),
            Desc(StatusEffectType.CyberPsychosis,
                "混乱に近い誤選択のほか、鋼属性の残留防御が付きます。重症扱いです。"),
            Desc(StatusEffectType.InfiniteManasource,
                "魔力に関する特殊効果です。解除範囲の対象外となることがあります。"),
        };
    }

    private static StatusEffectDescriptionEntry Desc(StatusEffectType type, string description)
    {
        return new StatusEffectDescriptionEntry { effectType = type, description = description };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entries == null || entries.Length == 0)
            entries = BuildDefaultDescriptionEntries();
    }
#endif
}
