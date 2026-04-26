/// <summary>
/// 試合結果（永続化・統計用）。分引きは勝率の分母から除外する想定で別カウントする。
/// </summary>
public enum MatchOutcome
{
    Victory = 0,
    Defeat = 1,
    Stalemate = 2,
}
