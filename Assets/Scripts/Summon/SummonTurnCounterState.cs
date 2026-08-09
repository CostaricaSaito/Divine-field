/// <summary>
/// 召喚獣ライフサイクル用：各プレイヤーが「自分のターン」を終えた回数（先攻の初手番＝1）。
/// UI の「ターンxx」表示にも流用（両者の終了回数の和＋1＝現在の手番が何ターン目か）。
/// </summary>
public sealed class SummonTurnCounterState
{
    public int PlayerOwnTurnsEnded;
    public int EnemyOwnTurnsEnded;

    /// <summary>バトル UI 用：先攻の初手＝1。TurnEnd 直後に増える前の値は、まだ当該手番の番号。</summary>
    public int CurrentBattleTurnDisplay => PlayerOwnTurnsEnded + EnemyOwnTurnsEnded + 1;

    /// <summary>デバッグ：<see cref="CurrentBattleTurnDisplay"/> を指定値に合わせる。</summary>
    public void DebugSetCurrentBattleTurnDisplay(int targetDisplay)
    {
        int endedTotal = targetDisplay > 0 ? targetDisplay - 1 : 0;
        PlayerOwnTurnsEnded = endedTotal / 2;
        EnemyOwnTurnsEnded = endedTotal - PlayerOwnTurnsEnded;
    }
}
