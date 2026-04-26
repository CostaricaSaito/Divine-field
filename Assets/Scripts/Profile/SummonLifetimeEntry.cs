using System;

/// <summary>
/// 召喚獣ごとの通算試合数・勝利数。
/// </summary>
[Serializable]
public class SummonLifetimeEntry
{
    public string summonId;
    public int gamesPlayed;
    public int wins;
}
