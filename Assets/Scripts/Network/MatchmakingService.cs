using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

/// <summary>
/// 1v1 rank match matchmaking:
/// Lobby QuickJoin -> (no open lobby) create lobby + Relay allocation and wait.
/// The Relay join code travels via lobby data; the summon / seed / first-turn
/// handshake runs over <see cref="NetworkBattleBridge"/> once NGO is connected.
/// On success <see cref="OnlineMatchContext"/> is populated and the caller can
/// load the Battle scene.
/// </summary>
public static class MatchmakingService
{
    const string LobbyName = "DivineFieldRankMatch";
    const string JoinCodeKey = "joinCode";
    const string ConnectionType = "dtls";
    const float HeartbeatIntervalSec = 15f;
    const float QuickJoinRetryIntervalSec = 1.5f;
    /// <summary>How long to keep trying QuickJoin before creating a host lobby.</summary>
    const float QuickJoinBecomeHostAfterSec = 7f;
    /// <summary>Per QuickJoin HTTP call timeout (prevents infinite hang).</summary>
    const float QuickJoinAttemptTimeoutSec = 12f;

    const string StatusConnectingServer = "サーバに接続しています";
    const string StatusPreparing = "対戦を準備しています";
    const string StatusSearching = "対戦相手を探しています";

    static Lobby _currentLobby;
    static CancellationTokenSource _heartbeatCts;

    public static bool IsBusy { get; private set; }

    /// <summary>
    /// Run the full matchmaking + handshake flow.
    /// Returns true when the match is ready (context populated, NGO connected).
    /// </summary>
    public static async Task<bool> FindMatchAsync(CancellationToken ct, IProgress<string> status = null)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[Matchmaking] Already running");
            return false;
        }

        IsBusy = true;
        try
        {
            status?.Report(StatusConnectingServer);
            await NetworkServiceBootstrap.EnsureServicesInitializedAsync();
            ct.ThrowIfCancellationRequested();

            status?.Report(StatusPreparing);

            // Drop a stale NGO session from a prior cancelled match attempt.
            if (NetworkManager.Singleton != null
                && (NetworkManager.Singleton.IsClient
                    || NetworkManager.Singleton.IsServer
                    || NetworkManager.Singleton.IsHost))
            {
                NetworkServiceBootstrap.ShutdownNetworkSession();
                NetworkBattleBridge.Reset();
            }

            var nm = NetworkServiceBootstrap.EnsureNetworkManager();
            Debug.Log("[Matchmaking] NetworkManager ready; searching for an open lobby...");

            // Retry QuickJoin so a slower peer has time to create a lobby first.
            // Give up after a short window and become host so at least one lobby exists.
            Lobby lobby = await TryQuickJoinLobbyWithRetryAsync(ct);

            ct.ThrowIfCancellationRequested();

            if (lobby != null)
            {
                Debug.Log($"[Matchmaking] Joined lobby {lobby.Id} as client");
                return await RunClientFlowAsync(nm, lobby, ct, status);
            }

            Debug.Log("[Matchmaking] No lobby found; starting host flow...");

            // Brief jitter so two peers finishing the retry window at once are less
            // likely to both create host lobbies.
            await Task.Delay(UnityEngine.Random.Range(200, 800), ct);
            return await RunHostFlowAsync(nm, ct, status);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[Matchmaking] Cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            status?.Report("マッチングに失敗しました");
            return false;
        }
        finally
        {
            if (!OnlineMatchContext.IsOnline)
                await CleanupAfterFailureAsync();
            else
                await CleanupLobbyOnlyAsync();
            IsBusy = false;
        }
    }

    // ==================== Client (joined an existing lobby) ====================

    static async Task<bool> RunClientFlowAsync(
        NetworkManager nm, Lobby lobby, CancellationToken ct, IProgress<string> status)
    {
        _currentLobby = lobby;
        if (lobby.Data == null || !lobby.Data.TryGetValue(JoinCodeKey, out var codeData)
            || string.IsNullOrEmpty(codeData.Value))
        {
            Debug.LogError("[Matchmaking] Lobby has no relay join code");
            return false;
        }

        var joinAllocation = await WithTimeoutAsync(
            RelayService.Instance.JoinAllocationAsync(codeData.Value),
            20f,
            "Relay JoinAllocation",
            ct);
        ct.ThrowIfCancellationRequested();

        var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
        transport.SetRelayServerData(new RelayServerData(joinAllocation, ConnectionType));

        if (!nm.StartClient())
        {
            Debug.LogError("[Matchmaking] StartClient failed");
            return false;
        }

        Debug.Log("[Matchmaking] StartClient OK; initializing NetworkBattleBridge...");
        NetworkBattleBridge.Initialize();
        status?.Report(StatusSearching);
        await WaitForLocalConnectionAsync(nm, ct);

        status?.Report("対戦相手が見つかりました！");

        // Handshake: send my profile, wait for the host's match config.
        NetworkBattleBridge.SendHello(BuildLocalProfile());
        var config = await NetworkBattleBridge.WaitForMatchConfigAsync(ct);

        OnlineMatchContext.BeginOnlineMatch(
            isHost: false,
            randomSeed: config.Seed,
            localPlayerGoesFirst: !config.HostGoesFirst,
            remoteSummonIndex: config.HostProfile.SummonIndex,
            remotePlayerName: config.HostProfile.PlayerName,
            remoteRankPoints: config.HostProfile.RankPoints);

        ApplyRemoteProfileToGame(config.HostProfile);
        status?.Report("対戦開始！");
        return true;
    }

    // ==================== Host (created a new lobby, waiting) ====================

    static async Task<bool> RunHostFlowAsync(
        NetworkManager nm, CancellationToken ct, IProgress<string> status)
    {
        Debug.Log("[Matchmaking] Host flow: creating Relay allocation...");
        var allocation = await WithTimeoutAsync(
            RelayService.Instance.CreateAllocationAsync(1),
            20f,
            "Relay CreateAllocation",
            ct);
        string joinCode = await WithTimeoutAsync(
            RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId),
            15f,
            "Relay GetJoinCode",
            ct);
        ct.ThrowIfCancellationRequested();

        var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
        transport.SetRelayServerData(new RelayServerData(allocation, ConnectionType));

        if (!nm.StartHost())
        {
            Debug.LogError("[Matchmaking] StartHost failed");
            return false;
        }

        Debug.Log("[Matchmaking] StartHost OK; initializing NetworkBattleBridge...");
        NetworkBattleBridge.Initialize();
        status?.Report(StatusSearching);

        Debug.Log("[Matchmaking] Creating rank-match lobby...");
        var lobbyOptions = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new Dictionary<string, DataObject>
            {
                { JoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
            },
        };
        _currentLobby = await WithTimeoutAsync(
            LobbyService.Instance.CreateLobbyAsync(LobbyName, 2, lobbyOptions),
            15f,
            "CreateLobby",
            ct);
        StartHeartbeat(_currentLobby.Id);
        Debug.Log($"[Matchmaking] Lobby created (id={_currentLobby.Id}); waiting for remote client...");

        // Wait for one remote client to connect via Relay.
        ulong remoteClientId = await WaitForRemoteClientAsync(nm, ct);
        NetworkBattleBridge.SetRemoteClientId(remoteClientId);

        status?.Report("対戦相手が見つかりました！");

        var hello = await NetworkBattleBridge.WaitForHelloAsync(ct);

        // Host decides seed and first turn, then shares them.
        int seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
        bool hostGoesFirst = UnityEngine.Random.Range(0, 2) == 0;
        NetworkBattleBridge.SendMatchConfig(new NetworkBattleBridge.MatchConfig
        {
            Seed = seed,
            HostGoesFirst = hostGoesFirst,
            HostProfile = BuildLocalProfile(),
        });

        OnlineMatchContext.BeginOnlineMatch(
            isHost: true,
            randomSeed: seed,
            localPlayerGoesFirst: hostGoesFirst,
            remoteSummonIndex: hello.SummonIndex,
            remotePlayerName: hello.PlayerName,
            remoteRankPoints: hello.RankPoints);

        ApplyRemoteProfileToGame(hello);
        status?.Report("対戦開始！");
        return true;
    }

    // ==================== Helpers ====================

    static async Task<Lobby> TryQuickJoinLobbyWithRetryAsync(CancellationToken ct)
    {
        var options = new QuickJoinLobbyOptions
        {
            Filter = new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.Name, LobbyName, QueryFilter.OpOptions.EQ),
            },
        };

        float elapsed = 0f;
        int attempt = 0;
        while (elapsed < QuickJoinBecomeHostAfterSec)
        {
            ct.ThrowIfCancellationRequested();
            attempt++;
            Debug.Log($"[Matchmaking] QuickJoin attempt {attempt} (elapsed {elapsed:F1}s)...");

            try
            {
                var lobby = await WithTimeoutAsync(
                    LobbyService.Instance.QuickJoinLobbyAsync(options),
                    QuickJoinAttemptTimeoutSec,
                    "QuickJoinLobby",
                    ct);
                Debug.Log($"[Matchmaking] QuickJoin succeeded (lobbyId={lobby?.Id})");
                return lobby;
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"[Matchmaking] QuickJoin attempt {attempt} timed out: {ex.Message}");
            }
            catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.NoOpenLobbies)
            {
                Debug.Log($"[Matchmaking] QuickJoin attempt {attempt}: no open lobbies yet");
            }
            catch (LobbyServiceException ex)
            {
                Debug.LogWarning($"[Matchmaking] QuickJoin attempt {attempt} failed: {ex.Reason} - {ex.Message}");
            }

            if (elapsed + QuickJoinRetryIntervalSec >= QuickJoinBecomeHostAfterSec)
                break;

            await Task.Delay(TimeSpan.FromSeconds(QuickJoinRetryIntervalSec), ct);
            elapsed += QuickJoinRetryIntervalSec;
        }

        Debug.Log($"[Matchmaking] No lobby after {elapsed:F1}s; will create one as host");
        return null;
    }

    static async Task<T> WithTimeoutAsync<T>(
        Task<T> task,
        float timeoutSec,
        string label,
        CancellationToken ct)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSec), ct);
        var completed = await Task.WhenAny(task, timeoutTask);
        if (completed != task)
            throw new TimeoutException($"{label} timed out after {timeoutSec:F0}s");

        return await task;
    }

    static NetworkBattleBridge.PeerProfile BuildLocalProfile()
    {
        return new NetworkBattleBridge.PeerProfile
        {
            SummonIndex = SummonSelectionManager.I != null ? SummonSelectionManager.I.SelectedIndex : 0,
            RankPoints = GameProfile.I != null ? GameProfile.I.CurrentRP : 0,
            PlayerName = GameProfile.I != null ? GameProfile.I.PlayerName : "プレイヤー",
        };
    }

    static void ApplyRemoteProfileToGame(NetworkBattleBridge.PeerProfile remote)
    {
        if (GameProfile.I != null && !string.IsNullOrWhiteSpace(remote.PlayerName))
            GameProfile.I.SetEnemyName(remote.PlayerName);
    }

    static async Task WaitForLocalConnectionAsync(NetworkManager nm, CancellationToken ct)
    {
        if (nm.IsConnectedClient)
        {
            Debug.Log("[Matchmaking] Client already connected");
            return;
        }

        // NGO client: OnClientConnectedCallback fires for remote peers, not for the local
        // client finishing its connect handshake. Poll IsConnectedClient instead.
        const float timeoutSec = 45f;
        float elapsed = 0f;
        while (!nm.IsConnectedClient)
        {
            ct.ThrowIfCancellationRequested();
            if (elapsed >= timeoutSec)
            {
                Debug.LogError("[Matchmaking] Client connection timed out waiting for host");
                throw new TimeoutException("Client failed to connect to host via Relay");
            }

            await Task.Delay(100, ct);
            elapsed += 0.1f;
        }

        Debug.Log($"[Matchmaking] Client connected (LocalClientId={nm.LocalClientId})");
    }

    static async Task<ulong> WaitForRemoteClientAsync(NetworkManager nm, CancellationToken ct)
    {
        foreach (var c in nm.ConnectedClientsList)
        {
            if (c.ClientId != nm.LocalClientId)
                return c.ClientId;
        }

        var tcs = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnConnected(ulong clientId)
        {
            if (clientId != nm.LocalClientId)
                tcs.TrySetResult(clientId);
        }

        nm.OnClientConnectedCallback += OnConnected;
        try
        {
            using (ct.Register(() => tcs.TrySetCanceled()))
                return await tcs.Task;
        }
        finally
        {
            nm.OnClientConnectedCallback -= OnConnected;
        }
    }

    static void StartHeartbeat(string lobbyId)
    {
        StopHeartbeat();
        _heartbeatCts = new CancellationTokenSource();
        _ = HeartbeatLoopAsync(lobbyId, _heartbeatCts.Token);
    }

    static void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
    }

    static async Task HeartbeatLoopAsync(string lobbyId, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSec), ct);
                await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Matchmaking] Heartbeat stopped: {ex.Message}");
        }
    }

    /// <summary>Delete / leave the lobby (it is only needed until Relay connects).</summary>
    static async Task CleanupLobbyOnlyAsync()
    {
        StopHeartbeat();
        var lobby = _currentLobby;
        _currentLobby = null;
        if (lobby == null) return;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            if (lobby.HostId == playerId)
                await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
            else
                await LobbyService.Instance.RemovePlayerAsync(lobby.Id, playerId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Matchmaking] Lobby cleanup failed: {ex.Message}");
        }
    }

    static async Task CleanupAfterFailureAsync()
    {
        await CleanupLobbyOnlyAsync();
        NetworkBattleBridge.Reset();
        NetworkServiceBootstrap.ShutdownNetworkSession();
        OnlineMatchContext.Clear();
    }

    /// <summary>Tear down everything after a match ends (result screen -> main menu).</summary>
    public static void EndOnlineSession()
    {
        StopHeartbeat();
        _currentLobby = null;
        NetworkBattleBridge.Reset();
        NetworkServiceBootstrap.ShutdownNetworkSession();
        BattleRandom.ClearOnline();
        OnlineMatchContext.Clear();
    }
}
