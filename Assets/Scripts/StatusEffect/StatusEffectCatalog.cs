using UnityEngine;

/// <summary>
/// 状態異常（公式ID 1〜15）の一覧。デバッグUIやループ処理用。
/// </summary>
public static class StatusEffectCatalog
{
    /// <summary>None を除く 15 種。インデックス 0 = 公式ID 1（病）。</summary>
    public static StatusEffectType[] AllAilments => _all;

    private static readonly StatusEffectType[] _all =
    {
        StatusEffectType.Sickness,
        StatusEffectType.SevereSickness,
        StatusEffectType.PurgatorySickness,
        StatusEffectType.ParadiseSickness,
        StatusEffectType.Weaken,
        StatusEffectType.EyeStrain,
        StatusEffectType.ClusterHeadache,
        StatusEffectType.Smoke,
        StatusEffectType.Misfortune,
        StatusEffectType.Seal,
        StatusEffectType.Fog,
        StatusEffectType.Confusion,
        StatusEffectType.Intervention,
        StatusEffectType.CurseBind,
        StatusEffectType.Restraint,
    };

    /// <summary>公式ID 1〜15 に対応する日本語名（デバッグUI用）。</summary>
    public static readonly string[] OfficialDisplayNames =
    {
        "病", "重病", "煉獄病", "楽園病", "衰弱", "眼精疲労", "群発頭痛",
        "煙幕", "不運", "封印", "濃霧", "混乱", "介入", "呪縛", "拘束",
    };

    /// <summary>公式ID 1〜15 を列挙値に変換。範囲外は None。</summary>
    public static StatusEffectType FromOfficialId(int officialId)
    {
        if (officialId < 1 || officialId > 15) return StatusEffectType.None;
        return _all[officialId - 1];
    }

    /// <summary>列挙値の公式ID（1〜15）。None・<see cref="StatusEffectType.RandomOneAilment"/> などは 0。</summary>
    public static int ToOfficialId(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return 0;
        for (int i = 0; i < _all.Length; i++)
        {
            if (_all[i] == type) return i + 1;
        }
        return 0;
    }

    /// <summary><see cref="AllAilments"/> から等確率で1つを選ぶ（<see cref="StatusEffectType.RandomOneAilment"/> の本体）。</summary>
    public static StatusEffectType PickRandomAilmentUniform()
    {
        var a = AllAilments;
        if (a == null || a.Length == 0) return StatusEffectType.None;
        return a[BattleRandom.Range(0, a.Length)];
    }
}
