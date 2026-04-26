using System;
using System.Collections.Generic;

/// <summary>
/// 通算・直近50試合の集計（表示・スナップショット用）。
/// 勝率: 勝利 / (勝利+敗北)。分引きは分母に含めない。
/// 使用率: 当該召喚獣の試合数 / 総試合数（分母は totalMatches）。
/// 召喚獣別勝率: 当該召喚獣使用試合における勝利数 / 当該試合数。
/// </summary>
public static class PlayerProfileStatistics
{
    public static float GetLifetimeWinRate(PlayerPersistedData d)
    {
        if (d == null) return 0f;
        int den = d.wins + d.losses;
        return den <= 0 ? 0f : (float)d.wins / den;
    }

    public static float GetRecentWinRateLast50(PlayerPersistedData d)
    {
        if (d?.recentMatches == null || d.recentMatches.Length == 0)
            return 0f;

        int w = 0, l = 0;
        for (int i = 0; i < d.recentMatches.Length; i++)
        {
            var o = d.recentMatches[i].outcome;
            if (o == (int)MatchOutcome.Victory) w++;
            else if (o == (int)MatchOutcome.Defeat) l++;
        }

        int den = w + l;
        return den <= 0 ? 0f : (float)w / den;
    }

    public static Dictionary<string, float> GetLifetimeSummonUsageRates(PlayerPersistedData d)
    {
        var dict = new Dictionary<string, float>(StringComparer.Ordinal);
        if (d == null || d.totalMatches <= 0 || d.summonLifetime == null)
            return dict;

        float inv = 1f / d.totalMatches;
        for (int i = 0; i < d.summonLifetime.Length; i++)
        {
            var e = d.summonLifetime[i];
            if (e == null || string.IsNullOrEmpty(e.summonId)) continue;
            dict[e.summonId] = e.gamesPlayed * inv;
        }

        return dict;
    }

    public static Dictionary<string, float> GetLifetimeSummonWinRates(PlayerPersistedData d)
    {
        var dict = new Dictionary<string, float>(StringComparer.Ordinal);
        if (d?.summonLifetime == null) return dict;

        for (int i = 0; i < d.summonLifetime.Length; i++)
        {
            var e = d.summonLifetime[i];
            if (e == null || string.IsNullOrEmpty(e.summonId)) continue;
            dict[e.summonId] = e.gamesPlayed <= 0 ? 0f : (float)e.wins / e.gamesPlayed;
        }

        return dict;
    }

    public static Dictionary<string, float> GetRecentSummonUsageRates(PlayerPersistedData d)
    {
        var dict = new Dictionary<string, float>(StringComparer.Ordinal);
        if (d?.recentMatches == null || d.recentMatches.Length == 0)
            return dict;

        int n = d.recentMatches.Length;
        float inv = 1f / n;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < n; i++)
        {
            string sid = d.recentMatches[i].summonId;
            if (string.IsNullOrEmpty(sid)) sid = "unknown";
            counts.TryGetValue(sid, out int c);
            counts[sid] = c + 1;
        }

        foreach (var kv in counts)
            dict[kv.Key] = kv.Value * inv;

        return dict;
    }

    public static Dictionary<string, float> GetRecentSummonWinRates(PlayerPersistedData d)
    {
        var dict = new Dictionary<string, float>(StringComparer.Ordinal);
        if (d?.recentMatches == null || d.recentMatches.Length == 0)
            return dict;

        var games = new Dictionary<string, int>(StringComparer.Ordinal);
        var wins = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < d.recentMatches.Length; i++)
        {
            var r = d.recentMatches[i];
            string sid = r.summonId;
            if (string.IsNullOrEmpty(sid)) sid = "unknown";

            games.TryGetValue(sid, out int g);
            games[sid] = g + 1;

            if (r.outcome == (int)MatchOutcome.Victory)
            {
                wins.TryGetValue(sid, out int w);
                wins[sid] = w + 1;
            }
        }

        foreach (var kv in games)
        {
            int w = wins.TryGetValue(kv.Key, out int ww) ? ww : 0;
            dict[kv.Key] = kv.Value <= 0 ? 0f : (float)w / kv.Value;
        }

        return dict;
    }

    public static MatchOutcome FromGameResultKind(GameResultController.ResultKind kind)
    {
        return kind switch
        {
            GameResultController.ResultKind.Victory => MatchOutcome.Victory,
            GameResultController.ResultKind.Defeat => MatchOutcome.Defeat,
            _ => MatchOutcome.Stalemate,
        };
    }
}
