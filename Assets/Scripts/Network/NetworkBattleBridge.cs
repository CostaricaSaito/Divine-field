using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Message bridge for the online battle, built on NGO custom named messages
/// (no NetworkObject / prefab registration required).
///
/// Both clients run the full battle simulation locally; this bridge carries
/// the player inputs (card selections + choice popups), the initial handshake,
/// and the host-authoritative state sync (HP/MP/GP, hands, turn owner).
/// Randomness is kept in sync via <see cref="BattleRandom"/> seeding.
/// </summary>
public static class NetworkBattleBridge
{
    const string MessageName = "DF_BATTLE";

    enum MsgType : byte
    {
        Hello = 1,        // client -> host : my profile / summon
        MatchConfig = 2,  // host -> client : seed, first turn, host profile / summon
        Attack = 3,       // attacker -> defender : main action card names (0 = pass) + target-self flag
        Defense = 4,      // defender -> attacker : defense card names (0 = allow)
        MagicalSword = 5, // attacker -> defender : optional MP payment choice for Magical Sword
        ResolveState = 6, // host -> client : authoritative HP/MP/GP right after combat resolution
        TurnReady = 7,    // client -> host : finished EndPhase presentation, ready for turn sync
        TurnSync = 8,     // host -> client : authoritative turn-boundary state (status, hands, next turn)
        SummonTurnEndEffects = 9, // host -> client : Garuda/Indra turn-end passive effects (5n)
        TributeBlood = 10, // attacker -> defender : HP paid for Tribute Blood
        DebugInjectCard = 11,        // host -> client : dev-only synchronized hand inject
        DebugInjectCardRequest = 12, // client -> host : dev-only inject request (host applies + forwards)
    }

    public enum RemoteEconomicKind : byte
    {
        None = 0,
        Buy = 1,
        Sell = 2,
        Exchange = 3,
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Development only: both peers apply the same inject (cardName, host-player hand?).</summary>
    public static event Action<string, bool> OnlineDebugInjectReceived;
#endif

    /// <summary>Host -> client: summon turn-end passive effect batch.</summary>
    public struct SummonTurnEndEffectsSync
    {
        public int TurnTag;
        public List<SummonTurnEndEffectEntry> Effects;
    }

    public struct PeerProfile
    {
        public int SummonIndex;
        public int RankPoints;
        public string PlayerName;
    }

    public struct MatchConfig
    {
        public int Seed;
        public bool HostGoesFirst;
        public PeerProfile HostProfile;
    }

    /// <summary>Attacker's main action: card names plus the self-target toggle state.</summary>
    public struct RemoteAttack
    {
        public List<string> CardNames;
        /// <summary>TOTAL tap toggle: attack aimed at self / recovery aimed at the opponent.</summary>
        public bool TargetSelf;
        public RemoteEconomicKind EconomicKind;
        /// <summary>Buy: card taken from defender hand. Sell: card sold from attacker hand.</summary>
        public string EconomicCardName;
        public int ExchangeAfterHp;
        public int ExchangeAfterMp;
        public int ExchangeAfterGp;
    }

    /// <summary>Magical Sword optional MP payment choice (sent even when declined).</summary>
    public struct MagicalSwordChoice
    {
        public bool Paid;
        public int PowerBonus;
        public int MpCost;
    }

    /// <summary>Tribute Blood HP payment (sent even when 0).</summary>
    public struct TributeBloodChoice
    {
        public int HpPaid;
    }

    /// <summary>Host-authoritative status snapshot (host side / client side, network perspective).</summary>
    public struct SideStatus
    {
        public int Hp;
        public int Mp;
        public int Gp;
    }

    /// <summary>Host -> client: authoritative HP/MP/GP right after combat resolution.</summary>
    public struct ResolveStateSync
    {
        public int TurnTag;
        public SideStatus Host;
        public SideStatus Client;
        /// <summary>Host-side player consumed near-death card name (empty if none).</summary>
        public string HostNearDeathCardName;
        /// <summary>Client-side player consumed near-death card name (empty if none).</summary>
        public string ClientNearDeathCardName;
    }

    /// <summary>Host -> client: authoritative turn-boundary state.</summary>
    public struct TurnBoundarySync
    {
        public int TurnTag;
        /// <summary>True when the HOST owns the next turn.</summary>
        public bool HostOwnsNextTurn;
        public SideStatus Host;
        public SideStatus Client;
        public int HostSummonIndex;
        public int ClientSummonIndex;
        public int HostOwnTurnsEnded;
        public int ClientOwnTurnsEnded;
        public List<string> HostHand;
        public List<string> ClientHand;
        public ArchMagicSideSync HostArchMagic;
        public ArchMagicSideSync ClientArchMagic;
    }

    /// <summary>大魔法詠唱状態（ターン境界同期）。RemainingTurns が 0 以下なら非詠唱。</summary>
    public struct ArchMagicSideSync
    {
        public int RemainingTurns;
        public int BarrierRemaining;
        public string CardName;
        /// <summary>効果対象が詠唱者本人か。</summary>
        public bool TargetSelf;
    }

    static bool _registered;
    static ulong _remoteClientId;

    static readonly Queue<RemoteAttack> _attackQueue = new();
    static readonly Queue<List<string>> _defenseQueue = new();
    static readonly Queue<MagicalSwordChoice> _magicalSwordQueue = new();
    static readonly Queue<TributeBloodChoice> _tributeBloodQueue = new();
    static readonly Queue<ResolveStateSync> _resolveStateQueue = new();
    static readonly Queue<int> _turnReadyQueue = new();
    static readonly Queue<TurnBoundarySync> _turnSyncQueue = new();
    static readonly Queue<SummonTurnEndEffectsSync> _summonTurnEndEffectsQueue = new();

    static TaskCompletionSource<RemoteAttack> _attackWaiter;
    static TaskCompletionSource<List<string>> _defenseWaiter;
    static TaskCompletionSource<MagicalSwordChoice> _magicalSwordWaiter;
    static TaskCompletionSource<TributeBloodChoice> _tributeBloodWaiter;
    static TaskCompletionSource<ResolveStateSync> _resolveStateWaiter;
    static TaskCompletionSource<int> _turnReadyWaiter;
    static TaskCompletionSource<TurnBoundarySync> _turnSyncWaiter;
    static TaskCompletionSource<SummonTurnEndEffectsSync> _summonTurnEndEffectsWaiter;
    static TaskCompletionSource<PeerProfile> _helloWaiter;
    static TaskCompletionSource<MatchConfig> _configWaiter;

    /// <summary>Raised when the remote peer disconnects mid-session.</summary>
    public static event Action RemoteDisconnected;

    public static bool IsInitialized => _registered;

    public static void Initialize()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[NetworkBattleBridge] NetworkManager missing");
            return;
        }

        if (_registered) return;
        _registered = true;

        nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessageReceived);
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        _remoteClientId = NetworkManager.ServerClientId; // valid for the client role
        Debug.Log("[NetworkBattleBridge] Initialized");
    }

    /// <summary>Host only: remember the connected client as the message target.</summary>
    public static void SetRemoteClientId(ulong clientId) => _remoteClientId = clientId;

    public static void Reset()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && _registered)
        {
            nm.CustomMessagingManager?.UnregisterNamedMessageHandler(MessageName);
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        _registered = false;
        _attackQueue.Clear();
        _defenseQueue.Clear();
        _magicalSwordQueue.Clear();
        _tributeBloodQueue.Clear();
        _resolveStateQueue.Clear();
        _turnReadyQueue.Clear();
        _turnSyncQueue.Clear();
        _summonTurnEndEffectsQueue.Clear();
        _attackWaiter?.TrySetCanceled();
        _defenseWaiter?.TrySetCanceled();
        _magicalSwordWaiter?.TrySetCanceled();
        _tributeBloodWaiter?.TrySetCanceled();
        _resolveStateWaiter?.TrySetCanceled();
        _turnReadyWaiter?.TrySetCanceled();
        _turnSyncWaiter?.TrySetCanceled();
        _summonTurnEndEffectsWaiter?.TrySetCanceled();
        _helloWaiter?.TrySetCanceled();
        _configWaiter?.TrySetCanceled();
        _attackWaiter = null;
        _defenseWaiter = null;
        _magicalSwordWaiter = null;
        _tributeBloodWaiter = null;
        _resolveStateWaiter = null;
        _turnReadyWaiter = null;
        _turnSyncWaiter = null;
        _summonTurnEndEffectsWaiter = null;
        _helloWaiter = null;
        _configWaiter = null;
    }

    static void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        // On the client role any disconnect means the session ended.
        // On the host role only the remote client matters.
        if (nm != null && nm.IsHost && clientId == nm.LocalClientId) return;
        Debug.LogWarning($"[NetworkBattleBridge] Remote disconnected (clientId={clientId})");
        RemoteDisconnected?.Invoke();
    }

    // ==================== Handshake ====================

    public static void SendHello(PeerProfile profile)
    {
        using var writer = new FastBufferWriter(512, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.Hello);
        writer.WriteValueSafe(profile.SummonIndex);
        writer.WriteValueSafe(profile.RankPoints);
        writer.WriteValueSafe(profile.PlayerName ?? "");
        Send(writer);
    }

    public static void SendMatchConfig(MatchConfig config)
    {
        using var writer = new FastBufferWriter(512, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.MatchConfig);
        writer.WriteValueSafe(config.Seed);
        writer.WriteValueSafe(config.HostGoesFirst);
        writer.WriteValueSafe(config.HostProfile.SummonIndex);
        writer.WriteValueSafe(config.HostProfile.RankPoints);
        writer.WriteValueSafe(config.HostProfile.PlayerName ?? "");
        Send(writer);
    }

    public static Task<PeerProfile> WaitForHelloAsync(CancellationToken ct)
    {
        _helloWaiter = new TaskCompletionSource<PeerProfile>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _helloWaiter?.TrySetCanceled());
        return _helloWaiter.Task;
    }

    public static Task<MatchConfig> WaitForMatchConfigAsync(CancellationToken ct)
    {
        _configWaiter = new TaskCompletionSource<MatchConfig>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _configWaiter?.TrySetCanceled());
        return _configWaiter.Task;
    }

    // ==================== Battle inputs ====================

    public static void SendAttackSelection(IReadOnlyList<CardData> cards, bool targetSelf = false)
    {
        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.Attack);
        WriteCardNames(writer, cards, out int count);
        writer.WriteValueSafe(targetSelf);
        WriteEconomicPayload(writer, RemoteEconomicKind.None, "", 0, 0, 0);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent Attack ({count} cards, targetSelf={targetSelf})");
    }

    public static void SendEconomicBuy(string targetCardName)
    {
        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.Attack);
        writer.WriteValueSafe(1);
        writer.WriteValueSafe(EconomicActionNames.Buy);
        writer.WriteValueSafe(false);
        WriteEconomicPayload(writer, RemoteEconomicKind.Buy, targetCardName, 0, 0, 0);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent Economic Buy (target={targetCardName})");
    }

    public static void SendEconomicSell(string soldCardName)
    {
        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.Attack);
        writer.WriteValueSafe(1);
        writer.WriteValueSafe(EconomicActionNames.Sell);
        writer.WriteValueSafe(false);
        WriteEconomicPayload(writer, RemoteEconomicKind.Sell, soldCardName, 0, 0, 0);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent Economic Sell (card={soldCardName})");
    }

    public static void SendEconomicExchange(int afterHp, int afterMp, int afterGp)
    {
        using var writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.Attack);
        writer.WriteValueSafe(0);
        writer.WriteValueSafe(false);
        WriteEconomicPayload(writer, RemoteEconomicKind.Exchange, "", afterHp, afterMp, afterGp);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent Economic Exchange (HP={afterHp}, MP={afterMp}, GP={afterGp})");
    }

    static void WriteEconomicPayload(
        FastBufferWriter writer,
        RemoteEconomicKind kind,
        string cardName,
        int afterHp,
        int afterMp,
        int afterGp)
    {
        writer.WriteValueSafe((byte)kind);
        writer.WriteValueSafe(cardName ?? "");
        writer.WriteValueSafe(afterHp);
        writer.WriteValueSafe(afterMp);
        writer.WriteValueSafe(afterGp);
    }

    static void ReadEconomicPayload(FastBufferReader reader, ref RemoteAttack attack)
    {
        reader.ReadValueSafe(out byte kindRaw);
        attack.EconomicKind = (RemoteEconomicKind)kindRaw;
        reader.ReadValueSafe(out attack.EconomicCardName);
        reader.ReadValueSafe(out attack.ExchangeAfterHp);
        reader.ReadValueSafe(out attack.ExchangeAfterMp);
        reader.ReadValueSafe(out attack.ExchangeAfterGp);
    }

    public static void SendDefenseSelection(IReadOnlyList<CardData> cards)
    {
        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.Defense);
        WriteCardNames(writer, cards, out int count);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent Defense ({count} cards)");
    }

    public static void SendMagicalSwordChoice(bool paid, int powerBonus, int mpCost)
    {
        using var writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.MagicalSword);
        writer.WriteValueSafe(paid);
        writer.WriteValueSafe(powerBonus);
        writer.WriteValueSafe(mpCost);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent MagicalSword choice (paid={paid}, bonus={powerBonus})");
    }

    public static void SendTributeBloodChoice(int hpPaid)
    {
        using var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.TributeBlood);
        writer.WriteValueSafe(hpPaid);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent TributeBlood choice (hpPaid={hpPaid})");
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Host only: notify client to apply the same debug hand inject.</summary>
    public static void SendDebugInjectCard(string cardName, bool targetIsHostPlayer)
    {
        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.DebugInjectCard);
        writer.WriteValueSafe(cardName ?? "");
        writer.WriteValueSafe(targetIsHostPlayer);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent DebugInjectCard ({cardName}, hostPlayer={targetIsHostPlayer})");
    }

    /// <summary>Client -> host: ask host to inject symmetrically and forward to client.</summary>
    public static void SendDebugInjectCardRequest(string cardName, bool targetIsHostPlayer)
    {
        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.DebugInjectCardRequest);
        writer.WriteValueSafe(cardName ?? "");
        writer.WriteValueSafe(targetIsHostPlayer);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent DebugInjectCardRequest ({cardName}, hostPlayer={targetIsHostPlayer})");
    }
#endif

    /// <summary>Wait for the opponent's main action (empty list = pass).</summary>
    public static Task<RemoteAttack> WaitForRemoteAttackAsync(CancellationToken ct)
        => WaitFromQueue(_attackQueue, ref _attackWaiter, ct);

    /// <summary>Wait for the opponent's defense pick (empty list = allow).</summary>
    public static Task<List<string>> WaitForRemoteDefenseAsync(CancellationToken ct)
        => WaitFromQueue(_defenseQueue, ref _defenseWaiter, ct);

    /// <summary>Wait for the attacker's Magical Sword MP payment choice.</summary>
    public static Task<MagicalSwordChoice> WaitForMagicalSwordChoiceAsync(CancellationToken ct)
        => WaitFromQueue(_magicalSwordQueue, ref _magicalSwordWaiter, ct);

    /// <summary>Wait for the attacker's Tribute Blood HP payment choice.</summary>
    public static Task<TributeBloodChoice> WaitForTributeBloodChoiceAsync(CancellationToken ct)
        => WaitFromQueue(_tributeBloodQueue, ref _tributeBloodWaiter, ct);

    // ==================== Host-authoritative state sync ====================

    public static void SendResolveState(ResolveStateSync sync)
    {
        using var writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.ResolveState);
        writer.WriteValueSafe(sync.TurnTag);
        WriteSideStatus(writer, sync.Host);
        WriteSideStatus(writer, sync.Client);
        writer.WriteValueSafe(sync.HostNearDeathCardName ?? "");
        writer.WriteValueSafe(sync.ClientNearDeathCardName ?? "");
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent ResolveState (tag={sync.TurnTag})");
    }

    public static void SendTurnReady(int turnTag)
    {
        using var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe((byte)MsgType.TurnReady);
        writer.WriteValueSafe(turnTag);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent TurnReady (tag={turnTag})");
    }

    public static void SendTurnSync(TurnBoundarySync sync)
    {
        using var writer = new FastBufferWriter(4096, Allocator.Temp, 65536);
        writer.WriteValueSafe((byte)MsgType.TurnSync);
        writer.WriteValueSafe(sync.TurnTag);
        writer.WriteValueSafe(sync.HostOwnsNextTurn);
        WriteSideStatus(writer, sync.Host);
        WriteSideStatus(writer, sync.Client);
        writer.WriteValueSafe(sync.HostSummonIndex);
        writer.WriteValueSafe(sync.ClientSummonIndex);
        writer.WriteValueSafe(sync.HostOwnTurnsEnded);
        writer.WriteValueSafe(sync.ClientOwnTurnsEnded);
        WriteStringList(writer, sync.HostHand);
        WriteStringList(writer, sync.ClientHand);
        WriteArchMagicSideSync(writer, sync.HostArchMagic);
        WriteArchMagicSideSync(writer, sync.ClientArchMagic);
        Send(writer);
        Debug.Log($"[NetworkBattleBridge] Sent TurnSync (tag={sync.TurnTag}, hostNext={sync.HostOwnsNextTurn})");
    }

    /// <summary>Host only: send Garuda/Indra turn-end passive effects for the current 5n milestone.</summary>
    public static void SendSummonTurnEndEffects(int turnTag, List<SummonTurnEndEffectEntry> effects)
    {
        using var writer = new FastBufferWriter(1024, Allocator.Temp, 65536);
        writer.WriteValueSafe((byte)MsgType.SummonTurnEndEffects);
        writer.WriteValueSafe(turnTag);
        WriteSummonTurnEndEffects(writer, effects);
        Send(writer);
        int count = effects != null ? effects.Count : 0;
        Debug.Log($"[NetworkBattleBridge] Sent SummonTurnEndEffects (tag={turnTag}, count={count})");
    }

    /// <summary>Client only: wait for host-authoritative summon turn-end effects.</summary>
    public static async Task<List<SummonTurnEndEffectEntry>> WaitForSummonTurnEndEffectsAsync(
        int turnTag,
        CancellationToken ct,
        int timeoutMs = 20000)
    {
        for (int attempt = 0; ; attempt++)
        {
            SummonTurnEndEffectsSync sync;
            if (_summonTurnEndEffectsQueue.Count > 0)
            {
                sync = _summonTurnEndEffectsQueue.Dequeue();
            }
            else
            {
                var tcs = new TaskCompletionSource<SummonTurnEndEffectsSync>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _summonTurnEndEffectsWaiter = tcs;
                ct.Register(() => tcs.TrySetCanceled());

                var waitTask = tcs.Task;
                var finished = await Task.WhenAny(waitTask, Task.Delay(timeoutMs, ct));
                if (finished != waitTask || ct.IsCancellationRequested)
                {
                    Debug.LogWarning("[NetworkBattleBridge] SummonTurnEndEffects wait timed out");
                    return new List<SummonTurnEndEffectEntry>();
                }

                sync = await waitTask;
            }

            if (sync.TurnTag >= turnTag || attempt >= 3)
                return sync.Effects ?? new List<SummonTurnEndEffectEntry>();

            Debug.Log($"[NetworkBattleBridge] Discarding stale SummonTurnEndEffects (tag={sync.TurnTag})");
        }
    }

    /// <summary>Client only: wait for the host's post-combat authoritative status.</summary>
    public static Task<ResolveStateSync> WaitForResolveStateAsync(CancellationToken ct)
        => WaitFromQueue(_resolveStateQueue, ref _resolveStateWaiter, ct);

    /// <summary>Host only: wait for the client's end-of-turn ready signal.</summary>
    public static Task<int> WaitForTurnReadyAsync(CancellationToken ct)
        => WaitFromQueue(_turnReadyQueue, ref _turnReadyWaiter, ct);

    /// <summary>Client only: wait for the host's turn-boundary state sync.</summary>
    public static Task<TurnBoundarySync> WaitForTurnSyncAsync(CancellationToken ct)
        => WaitFromQueue(_turnSyncQueue, ref _turnSyncWaiter, ct);

    // ==================== Internals ====================

    static Task<T> WaitFromQueue<T>(
        Queue<T> queue,
        ref TaskCompletionSource<T> waiter,
        CancellationToken ct)
    {
        if (queue.Count > 0)
            return Task.FromResult(queue.Dequeue());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiter = tcs;
        ct.Register(() => tcs.TrySetCanceled());
        return tcs.Task;
    }

    static void Dispatch<T>(Queue<T> queue, ref TaskCompletionSource<T> waiter, T value)
    {
        var tcs = waiter;
        waiter = null;
        if (tcs != null && tcs.TrySetResult(value))
            return;
        queue.Enqueue(value);
    }

    static void WriteCardNames(FastBufferWriter writer, IReadOnlyList<CardData> cards, out int count)
    {
        count = 0;
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) count++;
        }

        writer.WriteValueSafe(count);
        if (cards != null)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;
                writer.WriteValueSafe(cards[i].cardName ?? "");
            }
        }
    }

    static void WriteStringList(FastBufferWriter writer, List<string> list)
    {
        int count = list != null ? list.Count : 0;
        writer.WriteValueSafe(count);
        for (int i = 0; i < count; i++)
            writer.WriteValueSafe(list[i] ?? "");
    }

    static List<string> ReadStringList(FastBufferReader reader)
    {
        reader.ReadValueSafe(out int count);
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out string s);
            list.Add(s);
        }
        return list;
    }

    static void WriteArchMagicSideSync(FastBufferWriter writer, ArchMagicSideSync sync)
    {
        writer.WriteValueSafe(sync.RemainingTurns);
        writer.WriteValueSafe(sync.BarrierRemaining);
        writer.WriteValueSafe(sync.CardName ?? "");
        writer.WriteValueSafe(sync.TargetSelf);
    }

    static ArchMagicSideSync ReadArchMagicSideSync(FastBufferReader reader)
    {
        var sync = new ArchMagicSideSync();
        reader.ReadValueSafe(out sync.RemainingTurns);
        reader.ReadValueSafe(out sync.BarrierRemaining);
        reader.ReadValueSafe(out sync.CardName);
        reader.ReadValueSafe(out sync.TargetSelf);
        return sync;
    }

    static void WriteSideStatus(FastBufferWriter writer, SideStatus s)
    {
        writer.WriteValueSafe(s.Hp);
        writer.WriteValueSafe(s.Mp);
        writer.WriteValueSafe(s.Gp);
    }

    static SideStatus ReadSideStatus(FastBufferReader reader)
    {
        var s = new SideStatus();
        reader.ReadValueSafe(out s.Hp);
        reader.ReadValueSafe(out s.Mp);
        reader.ReadValueSafe(out s.Gp);
        return s;
    }

    static void WriteSummonTurnEndEffects(FastBufferWriter writer, List<SummonTurnEndEffectEntry> effects)
    {
        int count = effects != null ? effects.Count : 0;
        writer.WriteValueSafe(count);
        for (int i = 0; i < count; i++)
            WriteSummonTurnEndEffectEntry(writer, effects[i]);
    }

    static void WriteSummonTurnEndEffectEntry(FastBufferWriter writer, SummonTurnEndEffectEntry entry)
    {
        writer.WriteValueSafe((byte)entry.Kind);
        writer.WriteValueSafe(entry.OwnerIsHostPlayer);
        writer.WriteValueSafe(entry.CardName ?? "");
        writer.WriteValueSafe(entry.VictimHandIndex);
        WriteStringList(writer, entry.DrawnCardNames);
    }

    static SummonTurnEndEffectsSync ReadSummonTurnEndEffectsSync(FastBufferReader reader)
    {
        var sync = new SummonTurnEndEffectsSync();
        reader.ReadValueSafe(out sync.TurnTag);
        reader.ReadValueSafe(out int count);
        sync.Effects = new List<SummonTurnEndEffectEntry>(count);
        for (int i = 0; i < count; i++)
            sync.Effects.Add(ReadSummonTurnEndEffectEntry(reader));
        return sync;
    }

    static SummonTurnEndEffectEntry ReadSummonTurnEndEffectEntry(FastBufferReader reader)
    {
        var entry = new SummonTurnEndEffectEntry();
        reader.ReadValueSafe(out byte kind);
        entry.Kind = (SummonTurnEndEffectKind)kind;
        reader.ReadValueSafe(out entry.OwnerIsHostPlayer);
        reader.ReadValueSafe(out entry.CardName);
        reader.ReadValueSafe(out entry.VictimHandIndex);
        entry.DrawnCardNames = ReadStringList(reader);
        return entry;
    }

    static void Send(FastBufferWriter writer)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null)
        {
            Debug.LogWarning("[NetworkBattleBridge] Cannot send: no network session");
            return;
        }

        ulong target = nm.IsHost ? _remoteClientId : NetworkManager.ServerClientId;
        nm.CustomMessagingManager.SendNamedMessage(
            MessageName, target, writer, NetworkDelivery.ReliableSequenced);
    }

    static void OnMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte rawType);
        var type = (MsgType)rawType;

        switch (type)
        {
            case MsgType.Hello:
            {
                reader.ReadValueSafe(out int summonIndex);
                reader.ReadValueSafe(out int rp);
                reader.ReadValueSafe(out string name);
                SetRemoteClientId(senderClientId);
                var profile = new PeerProfile { SummonIndex = summonIndex, RankPoints = rp, PlayerName = name };
                Debug.Log($"[NetworkBattleBridge] Hello received: {name} (summon={summonIndex})");
                if (_helloWaiter == null || !_helloWaiter.TrySetResult(profile))
                    Debug.LogWarning("[NetworkBattleBridge] Hello received but nobody was waiting");
                break;
            }

            case MsgType.MatchConfig:
            {
                reader.ReadValueSafe(out int seed);
                reader.ReadValueSafe(out bool hostFirst);
                reader.ReadValueSafe(out int summonIndex);
                reader.ReadValueSafe(out int rp);
                reader.ReadValueSafe(out string name);
                var config = new MatchConfig
                {
                    Seed = seed,
                    HostGoesFirst = hostFirst,
                    HostProfile = new PeerProfile { SummonIndex = summonIndex, RankPoints = rp, PlayerName = name },
                };
                Debug.Log($"[NetworkBattleBridge] MatchConfig received (seed={seed}, hostFirst={hostFirst})");
                if (_configWaiter == null || !_configWaiter.TrySetResult(config))
                    Debug.LogWarning("[NetworkBattleBridge] MatchConfig received but nobody was waiting");
                break;
            }

            case MsgType.Attack:
            {
                var names = ReadStringList(reader);
                reader.ReadValueSafe(out bool targetSelf);
                var attack = new RemoteAttack { CardNames = names, TargetSelf = targetSelf };
                ReadEconomicPayload(reader, ref attack);
                Debug.Log(
                    $"[NetworkBattleBridge] Attack received ({names.Count} cards, targetSelf={targetSelf}, economic={attack.EconomicKind})");
                Dispatch(_attackQueue, ref _attackWaiter, attack);
                break;
            }

            case MsgType.Defense:
            {
                var names = ReadStringList(reader);
                Debug.Log($"[NetworkBattleBridge] Defense received ({names.Count} cards)");
                Dispatch(_defenseQueue, ref _defenseWaiter, names);
                break;
            }

            case MsgType.MagicalSword:
            {
                reader.ReadValueSafe(out bool paid);
                reader.ReadValueSafe(out int bonus);
                reader.ReadValueSafe(out int mpCost);
                Debug.Log($"[NetworkBattleBridge] MagicalSword choice received (paid={paid}, bonus={bonus})");
                Dispatch(_magicalSwordQueue, ref _magicalSwordWaiter,
                    new MagicalSwordChoice { Paid = paid, PowerBonus = bonus, MpCost = mpCost });
                break;
            }

            case MsgType.TributeBlood:
            {
                reader.ReadValueSafe(out int hpPaid);
                Debug.Log($"[NetworkBattleBridge] TributeBlood choice received (hpPaid={hpPaid})");
                Dispatch(_tributeBloodQueue, ref _tributeBloodWaiter,
                    new TributeBloodChoice { HpPaid = hpPaid });
                break;
            }

            case MsgType.ResolveState:
            {
                var sync = new ResolveStateSync();
                reader.ReadValueSafe(out sync.TurnTag);
                sync.Host = ReadSideStatus(reader);
                sync.Client = ReadSideStatus(reader);
                reader.ReadValueSafe(out sync.HostNearDeathCardName);
                reader.ReadValueSafe(out sync.ClientNearDeathCardName);
                Debug.Log($"[NetworkBattleBridge] ResolveState received (tag={sync.TurnTag})");
                Dispatch(_resolveStateQueue, ref _resolveStateWaiter, sync);
                break;
            }

            case MsgType.TurnReady:
            {
                reader.ReadValueSafe(out int tag);
                Debug.Log($"[NetworkBattleBridge] TurnReady received (tag={tag})");
                Dispatch(_turnReadyQueue, ref _turnReadyWaiter, tag);
                break;
            }

            case MsgType.TurnSync:
            {
                var sync = new TurnBoundarySync();
                reader.ReadValueSafe(out sync.TurnTag);
                reader.ReadValueSafe(out sync.HostOwnsNextTurn);
                sync.Host = ReadSideStatus(reader);
                sync.Client = ReadSideStatus(reader);
                reader.ReadValueSafe(out sync.HostSummonIndex);
                reader.ReadValueSafe(out sync.ClientSummonIndex);
                reader.ReadValueSafe(out sync.HostOwnTurnsEnded);
                reader.ReadValueSafe(out sync.ClientOwnTurnsEnded);
                sync.HostHand = ReadStringList(reader);
                sync.ClientHand = ReadStringList(reader);
                sync.HostArchMagic = ReadArchMagicSideSync(reader);
                sync.ClientArchMagic = ReadArchMagicSideSync(reader);
                Debug.Log($"[NetworkBattleBridge] TurnSync received (tag={sync.TurnTag}, hostNext={sync.HostOwnsNextTurn})");
                Dispatch(_turnSyncQueue, ref _turnSyncWaiter, sync);
                break;
            }

            case MsgType.SummonTurnEndEffects:
            {
                var sync = ReadSummonTurnEndEffectsSync(reader);
                Debug.Log($"[NetworkBattleBridge] SummonTurnEndEffects received (tag={sync.TurnTag}, count={sync.Effects?.Count ?? 0})");
                Dispatch(_summonTurnEndEffectsQueue, ref _summonTurnEndEffectsWaiter, sync);
                break;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            case MsgType.DebugInjectCard:
            {
                reader.ReadValueSafe(out string cardName);
                reader.ReadValueSafe(out bool targetIsHostPlayer);
                Debug.Log($"[NetworkBattleBridge] DebugInjectCard received ({cardName}, hostPlayer={targetIsHostPlayer})");
                OnlineDebugInjectReceived?.Invoke(cardName, targetIsHostPlayer);
                break;
            }

            case MsgType.DebugInjectCardRequest:
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsHost)
                {
                    Debug.LogWarning("[NetworkBattleBridge] DebugInjectCardRequest ignored (not host)");
                    break;
                }

                reader.ReadValueSafe(out string cardName);
                reader.ReadValueSafe(out bool targetIsHostPlayer);
                Debug.Log($"[NetworkBattleBridge] DebugInjectCardRequest received ({cardName}, hostPlayer={targetIsHostPlayer})");
                OnlineDebugInjectReceived?.Invoke(cardName, targetIsHostPlayer);
                SendDebugInjectCard(cardName, targetIsHostPlayer);
                break;
            }
#endif

            default:
                Debug.LogWarning($"[NetworkBattleBridge] Unknown message type {rawType}");
                break;
        }
    }
}
