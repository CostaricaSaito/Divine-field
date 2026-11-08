/// <summary>
/// 状態異常の分類ルール（公式15種以外の特殊効果含む）。
/// </summary>
public static class StatusEffectRules
{
    private static StatusEffectClassificationConfig _activeClassification;

    /// <summary>バトル開始時に BattleBootstrap から注入。未設定時はランタイム既定。</summary>
    public static void BindClassification(StatusEffectClassificationConfig config)
    {
        _activeClassification = config;
    }

    private static StatusEffectClassificationConfig Classification =>
        _activeClassification != null
            ? _activeClassification
            : StatusEffectClassificationConfig.GetRuntimeFallback();

    public static StatusEffectAilmentCategory GetCategory(StatusEffectType type) =>
        Classification.GetCategory(type);

    /// <summary>特殊分類（解除対象外・神無月・Ultimate バフ等）。</summary>
    public static bool IsIndelible(StatusEffectType type) =>
        GetCategory(type) == StatusEffectAilmentCategory.Special;

    /// <summary>指定の回復範囲で解除可能か。</summary>
    public static bool IsCurableBy(StatusEffectType type, StatusEffectCureScope scope)
    {
        if (type == StatusEffectType.None || scope == StatusEffectCureScope.None)
            return false;

        var category = GetCategory(type);
        if (category == StatusEffectAilmentCategory.Special)
            return false;

        return scope switch
        {
            StatusEffectCureScope.PartialCure => category == StatusEffectAilmentCategory.Mild
                || category == StatusEffectAilmentCategory.Unspecified,
            StatusEffectCureScope.AllCure => category == StatusEffectAilmentCategory.Mild
                || category == StatusEffectAilmentCategory.Severe
                || category == StatusEffectAilmentCategory.Unspecified,
            _ => false,
        };
    }
}
