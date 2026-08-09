/// <summary>
/// 天変地異に伴う通常攻撃解決中フラグ（煙幕無効など）。
/// </summary>
public static class DisasterCombatContext
{
    public static bool IsActive { get; private set; }

    public static void Begin() => IsActive = true;

    public static void End() => IsActive = false;
}
