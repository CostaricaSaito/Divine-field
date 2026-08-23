using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

/// <summary>
/// UGS Leaderboards 上のランクポイント（RP）ランキング。
/// Dashboard で <see cref="GlobalLeaderboardId"/> と同名の Leaderboard を作成してください。
/// </summary>
public static class RankLeaderboardService
{
    public const string GlobalLeaderboardId = "DivineFieldLeaderBoard";
    public const int TopLimit = 100;
    public const int DisplayNameMaxLength = 12;

    /// <summary>Leaderboard metadata（表示名）。</summary>
    [Serializable]
    public sealed class RankLeaderboardMetadata
    {
        public string displayName;
    }

    public readonly struct RankLeaderboardEntry
    {
        public RankLeaderboardEntry(int rank, string playerId, string displayName, int rankPoints)
        {
            Rank = rank;
            PlayerId = playerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RankPoints = rankPoints;
        }

        public int Rank { get; }
        public string PlayerId { get; }
        public string DisplayName { get; }
        public int RankPoints { get; }
    }

    public readonly struct RankLeaderboardFetchResult
    {
        public RankLeaderboardFetchResult(
            IReadOnlyList<RankLeaderboardEntry> topEntries,
            RankLeaderboardEntry? localPlayerEntry)
        {
            TopEntries = topEntries ?? Array.Empty<RankLeaderboardEntry>();
            LocalPlayerEntry = localPlayerEntry;
        }

        public IReadOnlyList<RankLeaderboardEntry> TopEntries { get; }
        public RankLeaderboardEntry? LocalPlayerEntry { get; }
    }

    public static string FormatDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "プレイヤー";

        displayName = displayName.Trim();
        if (displayName.Length <= DisplayNameMaxLength)
            return displayName;

        return displayName.Substring(0, DisplayNameMaxLength) + "…";
    }

    public static string FormatYourRankLine(int rank, string displayName, int rankPoints)
    {
        return $"{rank}位　{FormatDisplayName(displayName)}　{rankPoints}";
    }

    /// <summary>オンラインランクマ終了後に RP を投稿（失敗時はログのみ）。</summary>
    public static async Task TrySubmitScoreAsync(int rankPoints, string displayName)
    {
        try
        {
            await NetworkServiceBootstrap.EnsureServicesInitializedAsync();
            rankPoints = Mathf.Max(0, rankPoints);
            displayName = string.IsNullOrWhiteSpace(displayName) ? "プレイヤー" : displayName.Trim();

            var metadata = new RankLeaderboardMetadata { displayName = displayName };
            await LeaderboardsService.Instance.AddPlayerScoreAsync(
                GlobalLeaderboardId,
                rankPoints,
                new AddPlayerScoreOptions { Metadata = metadata });

            Debug.Log($"[RankLeaderboard] Submitted RP={rankPoints} name={displayName}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[RankLeaderboard] Submit failed: " + ex.Message);
        }
    }

    public static async Task<RankLeaderboardFetchResult> FetchTopAndLocalAsync(CancellationToken ct = default)
    {
        await NetworkServiceBootstrap.EnsureServicesInitializedAsync();
        ct.ThrowIfCancellationRequested();

        var topResponse = await LeaderboardsService.Instance.GetScoresAsync(
            GlobalLeaderboardId,
            new GetScoresOptions
            {
                Offset = 0,
                Limit = TopLimit,
                IncludeMetadata = true,
            });
        ct.ThrowIfCancellationRequested();

        var topEntries = ParseEntries(topResponse?.Results);

        RankLeaderboardEntry? localEntry = null;
        try
        {
            var localResponse = await LeaderboardsService.Instance.GetPlayerScoreAsync(
                GlobalLeaderboardId,
                new GetPlayerScoreOptions { IncludeMetadata = true });
            localEntry = ParseEntry(localResponse);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[RankLeaderboard] Local player score fetch failed: " + ex.Message);
        }

        return new RankLeaderboardFetchResult(topEntries, localEntry);
    }

    static List<RankLeaderboardEntry> ParseEntries(IReadOnlyList<LeaderboardEntry> results)
    {
        var list = new List<RankLeaderboardEntry>();
        if (results == null)
            return list;

        for (var i = 0; i < results.Count; i++)
        {
            var entry = ParseEntry(results[i]);
            if (entry.HasValue)
                list.Add(entry.Value);
        }

        return list;
    }

    static RankLeaderboardEntry? ParseEntry(LeaderboardEntry entry)
    {
        if (entry == null)
            return null;

        int rankPoints = Mathf.Max(0, (int)Math.Round(entry.Score));
        string displayName = ExtractDisplayName(entry);
        return new RankLeaderboardEntry(ToDisplayRank(entry.Rank), entry.PlayerId, displayName, rankPoints);
    }

    /// <summary>UGS の Rank は 0 始まり。UI は 1 位から表示する。</summary>
    static int ToDisplayRank(long apiRank) => Math.Max(1, (int)apiRank + 1);

    static string ExtractDisplayName(LeaderboardEntry entry)
    {
        if (entry.Metadata != null)
        {
            try
            {
                string json = entry.Metadata.ToString();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var meta = JsonUtility.FromJson<RankLeaderboardMetadata>(json);
                    if (!string.IsNullOrWhiteSpace(meta?.displayName))
                        return meta.displayName.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RankLeaderboard] Metadata parse failed: " + ex.Message);
            }
        }

        if (!string.IsNullOrWhiteSpace(entry.PlayerName))
            return entry.PlayerName.Trim();

        return "プレイヤー";
    }

    public static string GetLocalPlayerId()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            return string.Empty;

        return AuthenticationService.Instance.PlayerId ?? string.Empty;
    }
}
