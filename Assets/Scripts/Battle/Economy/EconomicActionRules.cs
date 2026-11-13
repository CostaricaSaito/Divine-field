/// <summary>
/// 経済アクション（買う／売る）のゲームルール定数と判定。
/// 両替は本ルールの対象外。
/// </summary>
public static class EconomicActionRules
{
    /// <summary>
    /// 買う／売るが使用可能になる <see cref="SummonTurnCounterState.CurrentBattleTurnDisplay"/> の値。
    /// 後攻プレイヤーの3手目（合計6手目）の手番開始時から解放。
    /// </summary>
    public const int BuySellUnlockBattleTurnDisplay = 6;

    public static bool IsBuySellUnlocked(SummonTurnCounterState counters)
    {
        if (counters == null) return false;
        return counters.CurrentBattleTurnDisplay >= BuySellUnlockBattleTurnDisplay;
    }

    /// <summary>解放までの残り「表示ターン」数（解放済みなら 0）。</summary>
    public static int GetBuySellTurnsUntilUnlock(SummonTurnCounterState counters)
    {
        if (counters == null)
            return BuySellUnlockBattleTurnDisplay;

        int remaining = BuySellUnlockBattleTurnDisplay - counters.CurrentBattleTurnDisplay;
        return remaining > 0 ? remaining : 0;
    }
}
