using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// <see cref="PlayerPersistedData"/> の JSON 読み書き（atomic write）。
/// </summary>
public static class PlayerProfileStore
{
    public const int CurrentSchemaVersion = 1;
    private const string FileName = "player_profile.json";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static PlayerPersistedData LoadOrDefault()
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var data = JsonUtility.FromJson<PlayerPersistedData>(json);
            if (data == null)
                return null;

            return Migrate(data);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PlayerProfileStore] 読み込み失敗。新規プロファイルを作成します: " + ex.Message);
            return null;
        }
    }

    public static void Save(PlayerPersistedData data)
    {
        if (data == null) return;

        data.schemaVersion = CurrentSchemaVersion;
        EnsureArraysNotNull(data);

        string json = JsonUtility.ToJson(data, prettyPrint: false);
        string tempPath = FilePath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? ".");

        File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (File.Exists(FilePath))
            File.Delete(FilePath);
        File.Move(tempPath, FilePath);
    }

    private static PlayerPersistedData Migrate(PlayerPersistedData data)
    {
        if (data.schemaVersion < 1)
            data.schemaVersion = 1;

        EnsureArraysNotNull(data);

        if (string.IsNullOrEmpty(data.playerGuid))
            data.playerGuid = Guid.NewGuid().ToString("N");

        if (string.IsNullOrEmpty(data.publicIdShort) && !string.IsNullOrEmpty(data.playerGuid))
        {
            string g = data.playerGuid.Replace("-", string.Empty);
            data.publicIdShort = g.Length >= 8 ? g.Substring(0, 8).ToUpperInvariant() : g.ToUpperInvariant();
        }

        return data;
    }

    private static void EnsureArraysNotNull(PlayerPersistedData data)
    {
        data.summonLifetime ??= Array.Empty<SummonLifetimeEntry>();
        data.recentMatches ??= Array.Empty<MatchRecordData>();
        data.unlockedBadgeIds ??= Array.Empty<string>();
    }
}
