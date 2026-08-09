/// <summary>
/// HP/MP/GP ステータス行とバリア被ダメポップアップの整数カウント速度（1 tick あたり秒）。
/// </summary>
public static class BattleStatCountRules
{
    public const float ValueStepSec = 0.0375f;

    /// <summary>整数カウント表示が from から to へ到達する tick 数。</summary>
    public static int EstimateCountdownSteps(int fromValue, int toValue) =>
        System.Math.Abs(fromValue - toValue);

    /// <summary>整数カウント表示の所要時間（ミリ秒）。</summary>
    public static int EstimateCountdownDurationMs(int fromValue, int toValue) =>
        EstimateCountdownSteps(fromValue, toValue) > 0
            ? (int)System.Math.Round(EstimateCountdownSteps(fromValue, toValue) * ValueStepSec * 1000d)
            : 0;
}
