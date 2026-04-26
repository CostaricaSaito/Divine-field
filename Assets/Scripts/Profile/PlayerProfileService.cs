using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤープロファイルのメモリキャッシュと更新API。
/// </summary>
public static class PlayerProfileService
{
    private static PlayerPersistedData _data;
    private static bool _loaded;

    public static PlayerPersistedData Data
    {
        get
        {
            EnsureLoaded();
            return _data;
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;

        _loaded = true;
        var fromDisk = PlayerProfileStore.LoadOrDefault();
        if (fromDisk != null)
        {
            _data = fromDisk;
            TryMigrateNameFromPlayerPrefsOnce();
            PlayerProfileStore.Save(_data);
            return;
        }

        _data = CreateNewProfile();
        PlayerProfileStore.Save(_data);
    }

    private static void TryMigrateNameFromPlayerPrefsOnce()
    {
        if (_data == null) return;

        string prefsName = PlayerPrefs.GetString(TitleNameInput.PlayerNameKey, "");
        if (string.IsNullOrWhiteSpace(_data.displayName) ||
            _data.displayName == "プレイヤー")
        {
            if (!string.IsNullOrWhiteSpace(prefsName))
                _data.displayName = prefsName.Trim();
        }
    }

    private static PlayerPersistedData CreateNewProfile()
    {
        string guid = Guid.NewGuid().ToString("N");
        string shortId = guid.Length >= 8 ? guid.Substring(0, 8).ToUpperInvariant() : guid.ToUpperInvariant();

        string prefsName = PlayerPrefs.GetString(TitleNameInput.PlayerNameKey, "");
        string name = string.IsNullOrWhiteSpace(prefsName) ? "プレイヤー" : prefsName.Trim();

        return new PlayerPersistedData
        {
            schemaVersion = PlayerProfileStore.CurrentSchemaVersion,
            playerGuid = guid,
            publicIdShort = shortId,
            displayName = name,
            currentRp = PlayerRank.DefaultStartingRp,
            totalMatches = 0,
            wins = 0,
            losses = 0,
            stalemates = 0,
            summonLifetime = Array.Empty<SummonLifetimeEntry>(),
            recentMatches = Array.Empty<MatchRecordData>(),
            unlockedBadgeIds = Array.Empty<string>(),
        };
    }

    /// <summary>表示名を更新して保存（タイトル画面等から）。</summary>
    public static void SetDisplayNameAndSave(string displayName)
    {
        EnsureLoaded();
        if (_data == null) return;

        _data.displayName = string.IsNullOrWhiteSpace(displayName) ? "プレイヤー" : displayName.Trim();
        PlayerPrefs.SetString(TitleNameInput.PlayerNameKey, _data.displayName);
        PlayerPrefs.Save();
        PlayerProfileStore.Save(_data);
    }

    /// <summary>
    /// リザルト適用後に呼ぶ。<paramref name="rpAfterBattle"/> は演出と同一の絶対値（GameProfile 非依存で渡す）。
    /// </summary>
    public static void RecordMatchEnd(GameResultController.ResultKind kind, string summonId, int rpAfterBattle)
    {
        EnsureLoaded();
        if (_data == null) return;

        var outcome = PlayerProfileStatistics.FromGameResultKind(kind);
        if (string.IsNullOrWhiteSpace(summonId))
            summonId = "unknown";

        _data.totalMatches++;
        switch (outcome)
        {
            case MatchOutcome.Victory:
                _data.wins++;
                break;
            case MatchOutcome.Defeat:
                _data.losses++;
                break;
            default:
                _data.stalemates++;
                break;
        }

        AddOrUpdateSummonLifetime(summonId, outcome == MatchOutcome.Victory);

        var recent = new List<MatchRecordData>(_data.recentMatches ?? Array.Empty<MatchRecordData>());
        recent.Add(MatchRecordData.Create(outcome, summonId, DateTime.UtcNow));
        while (recent.Count > 50)
            recent.RemoveAt(0);
        _data.recentMatches = recent.ToArray();

        _data.currentRp = Mathf.Max(0, rpAfterBattle);
        PlayerProfileStore.Save(_data);
    }

    private static void AddOrUpdateSummonLifetime(string summonId, bool wonThisMatch)
    {
        var list = new List<SummonLifetimeEntry>(_data.summonLifetime ?? Array.Empty<SummonLifetimeEntry>());
        int idx = list.FindIndex(e => e != null && e.summonId == summonId);
        if (idx < 0)
        {
            list.Add(new SummonLifetimeEntry
            {
                summonId = summonId,
                gamesPlayed = 1,
                wins = wonThisMatch ? 1 : 0,
            });
        }
        else
        {
            list[idx].gamesPlayed++;
            if (wonThisMatch)
                list[idx].wins++;
        }

        _data.summonLifetime = list.ToArray();
    }

    /// <summary>起動時に <see cref="GameProfile"/> へ名前・RP を流し込む。</summary>
    public static void ApplyPersistedStateToGameProfile(GameProfile gameProfile)
    {
        if (gameProfile == null) return;
        EnsureLoaded();
        if (_data == null) return;

        gameProfile.ApplyPersistedPlayerState(_data.displayName, _data.currentRp);
    }

    public static PublicProfileSnapshot BuildPublicSnapshot(GameProfile gameProfile)
    {
        EnsureLoaded();
        return PublicProfileSnapshot.FromLocal(_data, gameProfile);
    }
}
