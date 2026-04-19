/// <summary>
/// 「劣勢時」判定：HP + MP + GP の合計が閾値以下。
/// </summary>
public static class DisadvantageRules
{
    /// <summary>劣勢とみなすリソース合計の上限（この値以下で劣勢）。</summary>
    public const int TotalResourceThreshold = 10;

    public static bool IsDisadvantaged(PlayerStatus ps)
    {
        if (ps == null) return false;
        return ps.currentHP + ps.currentMP + ps.currentGP <= TotalResourceThreshold;
    }
}
