using System;

/// <summary>
/// クライアントに保存するプレイヤープロファイル本体（将来サーバ権威と差し替え可能な形）。
/// </summary>
[Serializable]
public class PlayerPersistedData
{
    public int schemaVersion = 1;
    public string playerGuid;
    public string publicIdShort;
    public string displayName = "プレイヤー";
    public int currentRp = PlayerRank.DefaultStartingRp;
    public int totalMatches;
    public int wins;
    public int losses;
    public int stalemates;
    public SummonLifetimeEntry[] summonLifetime = Array.Empty<SummonLifetimeEntry>();
    public MatchRecordData[] recentMatches = Array.Empty<MatchRecordData>();
    public string[] unlockedBadgeIds = Array.Empty<string>();
}
