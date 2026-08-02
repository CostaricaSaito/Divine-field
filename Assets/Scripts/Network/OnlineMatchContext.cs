/// <summary>
/// Static session state for the current online match.
/// Populated by the matchmaking handshake before the Battle scene loads,
/// consumed by BattleManager and cleared when the match ends.
/// </summary>
public static class OnlineMatchContext
{
    public static bool IsOnline { get; private set; }
    public static bool IsHost { get; private set; }
    public static int RandomSeed { get; private set; }
    /// <summary>True when the local player takes the first turn.</summary>
    public static bool LocalPlayerGoesFirst { get; private set; }
    public static int RemoteSummonIndex { get; private set; }
    public static string RemotePlayerName { get; private set; }
    public static int RemoteRankPoints { get; private set; }

    public static void BeginOnlineMatch(
        bool isHost,
        int randomSeed,
        bool localPlayerGoesFirst,
        int remoteSummonIndex,
        string remotePlayerName,
        int remoteRankPoints)
    {
        IsOnline = true;
        IsHost = isHost;
        RandomSeed = randomSeed;
        LocalPlayerGoesFirst = localPlayerGoesFirst;
        RemoteSummonIndex = remoteSummonIndex;
        RemotePlayerName = remotePlayerName;
        RemoteRankPoints = remoteRankPoints;
    }

    public static void Clear()
    {
        IsOnline = false;
        IsHost = false;
        RandomSeed = 0;
        LocalPlayerGoesFirst = false;
        RemoteSummonIndex = 0;
        RemotePlayerName = null;
        RemoteRankPoints = 0;
    }
}
