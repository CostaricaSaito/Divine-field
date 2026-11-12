/// <summary>
/// HP/MP/GP ステータス行の表示方針。
/// 実値（<see cref="PlayerStatus"/>）が変わったら、通常は <see cref="BattleStatusUI"/> の整数カウント演出で追従する。
/// </summary>
public static class BattleHpmgpDisplayPolicy
{
    /// <summary>
    /// <see cref="BattleUIManager.UpdateStatus"/> の既定。バトル中のダメージ・回復・病系などは常に false（カウント演出あり）。
    /// <c>snapHpmgpNumbers: true</c> は通常のリソース変動では使わない。濃霧の「？」表示／復帰は <see cref="BattleStatusUI"/> が内部で同期する。
    /// </summary>
    public const bool SnapHpmgpNumbersForGameplayChanges = false;
}
