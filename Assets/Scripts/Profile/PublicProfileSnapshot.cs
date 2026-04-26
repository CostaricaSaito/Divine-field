using System;
using System.Collections.Generic;

/// <summary>
/// 他プレイヤー表示・マッチング提出・ランキング等に使う公開用スナップショット（クライアント生成・将来サーバ検証）。
/// </summary>
[Serializable]
public class PublicProfileSnapshot
{
    public string displayName;
    public string publicId;
    public int currentRp;
    public string rankDisplayName;
    public string[] unlockedBadgeIds = Array.Empty<string>();
    public float lifetimeWinRate;
    public float recentWinRate50;
    public PublicSummonRateEntry[] lifetimeSummonRates = Array.Empty<PublicSummonRateEntry>();
    public PublicSummonRateEntry[] recentSummonRates = Array.Empty<PublicSummonRateEntry>();

    /// <summary>ローカル永続データと <see cref="GameProfile"/> からスナップショットを組み立てる。</summary>
    public static PublicProfileSnapshot FromLocal(PlayerPersistedData data, GameProfile gameProfile)
    {
        var snap = new PublicProfileSnapshot();
        if (data != null)
        {
            snap.displayName = data.displayName;
            snap.publicId = data.publicIdShort;
            snap.currentRp = data.currentRp;
            snap.unlockedBadgeIds = data.unlockedBadgeIds != null
                ? (string[])data.unlockedBadgeIds.Clone()
                : Array.Empty<string>();
            snap.lifetimeWinRate = PlayerProfileStatistics.GetLifetimeWinRate(data);
            snap.recentWinRate50 = PlayerProfileStatistics.GetRecentWinRateLast50(data);
            snap.lifetimeSummonRates = BuildEntries(PlayerProfileStatistics.GetLifetimeSummonUsageRates(data),
                PlayerProfileStatistics.GetLifetimeSummonWinRates(data));
            snap.recentSummonRates = BuildEntries(PlayerProfileStatistics.GetRecentSummonUsageRates(data),
                PlayerProfileStatistics.GetRecentSummonWinRates(data));
        }

        if (gameProfile != null)
            snap.rankDisplayName = gameProfile.RankDisplayName;
        else if (data != null)
            snap.rankDisplayName = PlayerRank.GetDisplayName(data.currentRp);

        return snap;
    }

    private static PublicSummonRateEntry[] BuildEntries(
        Dictionary<string, float> usage,
        Dictionary<string, float> winRates)
    {
        if (usage == null || usage.Count == 0)
            return Array.Empty<PublicSummonRateEntry>();

        var list = new List<PublicSummonRateEntry>(usage.Count);
        foreach (var kv in usage)
        {
            winRates.TryGetValue(kv.Key, out float wr);
            list.Add(new PublicSummonRateEntry
            {
                summonId = kv.Key,
                usageRate = kv.Value,
                winRate = wr,
            });
        }

        list.Sort((a, b) => string.CompareOrdinal(a.summonId, b.summonId));
        return list.ToArray();
    }
}

/// <summary>召喚獣ごとの使用率・勝率（公開スナップショット用）。</summary>
[Serializable]
public class PublicSummonRateEntry
{
    public string summonId;
    public float usageRate;
    public float winRate;
}
