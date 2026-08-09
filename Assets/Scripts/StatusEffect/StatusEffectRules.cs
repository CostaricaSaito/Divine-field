/// <summary>
/// 状態異常の分類ルール（公式15種以外の特殊効果含む）。
/// </summary>
public static class StatusEffectRules
{
    /// <summary>全治・個別解除の対象外（神無月など）。</summary>
    public static bool IsIndelible(StatusEffectType type) =>
        type == StatusEffectType.Kannaduki;
}
