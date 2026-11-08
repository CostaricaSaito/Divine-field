using UnityEngine;

/// <summary>
/// 状態異常の重症度分類（軽症・重症・特殊）。回復カードの解除範囲判定に使用。
/// </summary>
public enum StatusEffectAilmentCategory
{
    /// <summary>未分類（既定：軽症として扱う）。</summary>
    Unspecified = 0,

    [InspectorName("状態異常（軽症）")]
    Mild = 1,

    [InspectorName("状態異常（重症）")]
    Severe = 2,

    [InspectorName("状態異常（特殊）")]
    Special = 3,
}

/// <summary>
/// 回復カードが解除できる状態異常の範囲。
/// </summary>
public enum StatusEffectCureScope
{
    None = 0,

    /// <summary>軽症回復：状態異常（軽症）のみ解除。</summary>
    PartialCure = 1,

    /// <summary>重症回復：状態異常（軽症・重症）を解除。特殊は対象外。</summary>
    AllCure = 2,
}
