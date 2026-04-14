/// <summary>
/// 状態異常の種類（全15種 + None）。
/// 数値は公式IDと一致（1=病 … 15=拘束）。シリアライズ互換のため明示指定。
/// </summary>
public enum StatusEffectType
{
    None = 0,

    // --- 公式ID 1〜4：病系（相互排他・段階進行） ---
    /// <summary>1 病</summary>
    Sickness = 1,
    /// <summary>2 重病</summary>
    SevereSickness = 2,
    /// <summary>3 煉獄病</summary>
    PurgatorySickness = 3,
    /// <summary>4 楽園病</summary>
    ParadiseSickness = 4,

    // --- 公式ID 5〜7 ---
    /// <summary>5 衰弱</summary>
    Weaken = 5,
    /// <summary>6 眼精疲労</summary>
    EyeStrain = 6,
    /// <summary>7 群発頭痛</summary>
    ClusterHeadache = 7,

    // --- 公式ID 8〜11 ---
    /// <summary>8 煙幕</summary>
    Smoke = 8,
    /// <summary>9 不運</summary>
    Misfortune = 9,
    /// <summary>10 封印</summary>
    Seal = 10,
    /// <summary>11 濃霧</summary>
    Fog = 11,

    // --- 公式ID 12〜15 ---
    /// <summary>12 混乱</summary>
    Confusion = 12,
    /// <summary>13 介入</summary>
    Intervention = 13,
    /// <summary>14 呪縛</summary>
    CurseBind = 14,
    /// <summary>15 拘束</summary>
    Restraint = 15,
}
