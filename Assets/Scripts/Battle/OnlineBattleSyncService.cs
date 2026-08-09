using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Online PvP: host-authoritative ResolveState / TurnBoundary sync and dev debug inject.
/// </summary>
public sealed class OnlineBattleSyncService
{
    private readonly IOnlineBattleSyncHost _host;

    private string _hostNearDeathCardName;
    private string _clientNearDeathCardName;

    private const int ResolveSyncTimeoutMs = 20000;
    private const int TurnSyncTimeoutMs = 45000;

    public OnlineBattleSyncService(IOnlineBattleSyncHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void RecordNearDeathConsumption(PlayerType ownerSide, string cardName)
    {
        if (!_host.IsOnlineMatch || string.IsNullOrEmpty(cardName)) return;

        if (OnlineMatchContext.IsHost)
        {
            if (ownerSide == PlayerType.Player)
                _hostNearDeathCardName = cardName;
            else
                _clientNearDeathCardName = cardName;
        }
        else
        {
            if (ownerSide == PlayerType.Player)
                _clientNearDeathCardName = cardName;
            else
                _hostNearDeathCardName = cardName;
        }
    }

    public async Task RunResolveStateSyncAsync(CancellationToken ct)
    {
        if (!_host.IsOnlineMatch || _host.IsGameEndTriggered) return;

        try
        {
            if (OnlineMatchContext.IsHost)
            {
                NetworkBattleBridge.SendResolveState(new NetworkBattleBridge.ResolveStateSync
                {
                    TurnTag = _host.GetOnlineTurnTag(),
                    Host = CaptureSideStatus(_host.PlayerStatus),
                    Client = CaptureSideStatus(_host.EnemyStatus),
                    HostNearDeathCardName = _hostNearDeathCardName ?? "",
                    ClientNearDeathCardName = _clientNearDeathCardName ?? "",
                });
                ClearNearDeathSnapshot();
                return;
            }

            NetworkBattleBridge.ResolveStateSync sync;
            for (int attempt = 0; ; attempt++)
            {
                var waitTask = NetworkBattleBridge.WaitForResolveStateAsync(ct);
                var finished = await Task.WhenAny(waitTask, Task.Delay(ResolveSyncTimeoutMs, ct));
                if (finished != waitTask || ct.IsCancellationRequested)
                {
                    Debug.LogWarning("[OnlineSync] ResolveState timed out. Continuing with local values (turn boundary may correct).");
                    return;
                }

                sync = await waitTask;
                if (sync.TurnTag >= _host.GetOnlineTurnTag() || attempt >= 3) break;
                Debug.Log($"[OnlineSync] Discarding stale ResolveState (tag={sync.TurnTag})");
            }

            ApplyAuthoritativeSideStatus(_host.PlayerStatus, sync.Client, "self");
            ApplyAuthoritativeSideStatus(_host.EnemyStatus, sync.Host, "opponent");
            _host.UpdateBattleStatusUi();

            if (!string.IsNullOrEmpty(sync.HostNearDeathCardName))
            {
                NearDeathEffectProcessor.ApplySyncedCardConsumption(
                    _host.Manager, _host.BattleProcessor, _host.HandRefill,
                    PlayerType.Enemy, sync.HostNearDeathCardName);
            }

            if (!string.IsNullOrEmpty(sync.ClientNearDeathCardName))
            {
                NearDeathEffectProcessor.ApplySyncedCardConsumption(
                    _host.Manager, _host.BattleProcessor, _host.HandRefill,
                    PlayerType.Player, sync.ClientNearDeathCardName);
            }

            await _host.TryHandleDeathIfAnyAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[OnlineSync] ResolveState sync cancelled");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    /// <returns>true when turn owner was applied by sync (caller should skip ToggleTurnOwner).</returns>
    public async Task<bool> RunTurnBoundarySyncAsync(CancellationToken ct)
    {
        if (!_host.IsOnlineMatch || _host.IsGameEndTriggered) return false;

        try
        {
            if (OnlineMatchContext.IsHost)
            {
                var readyTask = NetworkBattleBridge.WaitForTurnReadyAsync(ct);
                var finished = await Task.WhenAny(readyTask, Task.Delay(TurnSyncTimeoutMs, ct));
                if (finished != readyTask)
                    Debug.LogWarning("[OnlineSync] TurnReady timed out. Sending sync and continuing.");

                var nextOwner = _host.CurrentTurnOwner == PlayerType.Player
                    ? PlayerType.Enemy
                    : PlayerType.Player;
                var counters = _host.SummonTurnCounters;

                NetworkBattleBridge.SendTurnSync(new NetworkBattleBridge.TurnBoundarySync
                {
                    TurnTag = _host.GetOnlineTurnTag(),
                    HostOwnsNextTurn = nextOwner == PlayerType.Player,
                    Host = CaptureSideStatus(_host.PlayerStatus),
                    Client = CaptureSideStatus(_host.EnemyStatus),
                    HostSummonIndex = FindSummonIndex(_host.PlayerStatus?.summonData),
                    ClientSummonIndex = FindSummonIndex(_host.EnemyStatus?.summonData),
                    HostOwnTurnsEnded = counters.PlayerOwnTurnsEnded,
                    ClientOwnTurnsEnded = counters.EnemyOwnTurnsEnded,
                    HostHand = CollectCardNames(_host.PlayerHand),
                    ClientHand = CollectCardNames(_host.CpuHand),
                    HostArchMagic = CaptureArchMagicSideSync(_host.PlayerStatus),
                    ClientArchMagic = CaptureArchMagicSideSync(_host.EnemyStatus),
                });

                _host.CurrentTurnOwner = nextOwner;
                Debug.Log($"[Turn] Turn owner changed (host authority): {_host.CurrentTurnOwner}");
                return true;
            }

            NetworkBattleBridge.SendTurnReady(_host.GetOnlineTurnTag());

            NetworkBattleBridge.TurnBoundarySync sync;
            for (int attempt = 0; ; attempt++)
            {
                var syncTask = NetworkBattleBridge.WaitForTurnSyncAsync(ct);
                var finished = await Task.WhenAny(syncTask, Task.Delay(TurnSyncTimeoutMs, ct));
                if (finished != syncTask || ct.IsCancellationRequested)
                {
                    Debug.LogWarning("[OnlineSync] TurnSync timed out. Continuing with local values.");
                    return false;
                }

                sync = await syncTask;
                if (sync.TurnTag >= _host.GetOnlineTurnTag() || attempt >= 3) break;
                Debug.Log($"[OnlineSync] Discarding stale TurnSync (tag={sync.TurnTag})");
            }

            ApplyAuthoritativeTurnBoundary(sync);
            await _host.TryHandleDeathIfAnyAsync(ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[OnlineSync] Turn boundary sync cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void HandleDebugInjectReceived(string cardName, bool targetIsHostPlayer)
        => TryApplyDebugCardInject(cardName, targetIsHostPlayer);

    public bool TryApplyDebugCardInject(string cardName, bool targetIsHostPlayer)
    {
        if (!_host.IsOnlineMatch || _host.CardDealer == null || string.IsNullOrEmpty(cardName))
            return false;

        List<CardData> hand;
        bool withUi;
        if (targetIsHostPlayer)
        {
            hand = OnlineMatchContext.IsHost ? _host.PlayerHand : _host.CpuHand;
            withUi = OnlineMatchContext.IsHost;
        }
        else
        {
            hand = OnlineMatchContext.IsHost ? _host.CpuHand : _host.PlayerHand;
            withUi = !OnlineMatchContext.IsHost;
        }

        if (hand == null || hand.Count >= _host.MaxHandCards)
        {
            Debug.LogWarning("[OnlineSync] Debug inject: hand full or null");
            return false;
        }

        var template = _host.CardDealer.FindTemplateByDisplayOrAssetName(cardName);
        if (template == null)
        {
            Debug.LogWarning($"[OnlineSync] Debug inject: template not found ({cardName})");
            return false;
        }

        var instance = _host.CardDealer.InstantiateCardFromTemplate(template);
        if (instance == null) return false;

        hand.Add(instance);
        if (withUi)
        {
            _host.CardDealer.CreateCardUIForHand(instance);
            instance.cardUI?.Reveal();
            _host.UpdateTotalATKDefDisplay();
            BattleUIManager.I?.RefreshMagicCardInteractivity(_host.PlayerHand);
            _host.UpdateBattleStatusUi();
            _host.RefreshPlayerDefensePhaseInteractivity();
        }

        Debug.Log($"[OnlineSync] Debug inject: {cardName} -> {(targetIsHostPlayer ? "host player" : "client player")} (localUi={withUi})");
        return true;
    }

    public bool HostBroadcastDebugCardInject(string cardName, bool targetIsHostPlayer)
    {
        if (!_host.IsOnlineMatch || !OnlineMatchContext.IsHost || string.IsNullOrEmpty(cardName))
            return false;
        if (!TryApplyDebugCardInject(cardName, targetIsHostPlayer))
            return false;
        NetworkBattleBridge.SendDebugInjectCard(cardName, targetIsHostPlayer);
        return true;
    }

    public bool RequestDebugCardInject(string cardName, bool targetIsHostPlayer)
    {
        if (!_host.IsOnlineMatch || string.IsNullOrEmpty(cardName))
            return false;
        if (OnlineMatchContext.IsHost)
            return HostBroadcastDebugCardInject(cardName, targetIsHostPlayer);
        NetworkBattleBridge.SendDebugInjectCardRequest(cardName, targetIsHostPlayer);
        return true;
    }
#endif

    private void ClearNearDeathSnapshot()
    {
        _hostNearDeathCardName = null;
        _clientNearDeathCardName = null;
    }

    private void ApplyAuthoritativeTurnBoundary(NetworkBattleBridge.TurnBoundarySync sync)
    {
        ApplyAuthoritativeSideStatus(_host.PlayerStatus, sync.Client, "self");
        ApplyAuthoritativeSideStatus(_host.EnemyStatus, sync.Host, "opponent");

        var counters = _host.SummonTurnCounters;
        counters.PlayerOwnTurnsEnded = sync.ClientOwnTurnsEnded;
        counters.EnemyOwnTurnsEnded = sync.HostOwnTurnsEnded;

        VerifyOrFixSummon(_host.PlayerStatus, sync.ClientSummonIndex, "self");
        VerifyOrFixSummon(_host.EnemyStatus, sync.HostSummonIndex, "opponent");

        ReconcileHandToAuthoritative(_host.PlayerHand, sync.ClientHand, withUi: true, label: "player hand");
        ReconcileHandToAuthoritative(_host.CpuHand, sync.HostHand, withUi: false, label: "opponent hand");

        ApplyAuthoritativeArchMagicSide(_host.PlayerStatus, sync.ClientArchMagic);
        ApplyAuthoritativeArchMagicSide(_host.EnemyStatus, sync.HostArchMagic);

        _host.CurrentTurnOwner = sync.HostOwnsNextTurn ? PlayerType.Enemy : PlayerType.Player;
        Debug.Log($"[Turn] Turn owner changed (host authority): {_host.CurrentTurnOwner}");

        _host.UpdateBattleStatusUi();
        _host.RefreshTurnCountDisplay();
    }

    private static NetworkBattleBridge.SideStatus CaptureSideStatus(PlayerStatus s) => new NetworkBattleBridge.SideStatus
    {
        Hp = s != null ? s.currentHP : 0,
        Mp = s != null ? s.currentMP : 0,
        Gp = s != null ? s.currentGP : 0,
    };

    private static void ApplyAuthoritativeSideStatus(
        PlayerStatus target, NetworkBattleBridge.SideStatus authoritative, string label)
    {
        if (target == null) return;
        if (target.currentHP == authoritative.Hp
            && target.currentMP == authoritative.Mp
            && target.currentGP == authoritative.Gp)
            return;

        Debug.LogWarning(
            $"[OnlineSync] Correcting {label} status to host values: " +
            $"HP {target.currentHP}->{authoritative.Hp}, MP {target.currentMP}->{authoritative.Mp}, GP {target.currentGP}->{authoritative.Gp}");
        target.currentHP = Mathf.Clamp(authoritative.Hp, 0, target.maxHP);
        target.currentMP = Mathf.Clamp(authoritative.Mp, 0, target.maxMP);
        target.currentGP = Mathf.Clamp(authoritative.Gp, 0, target.maxGP);
    }

    private static int FindSummonIndex(SummonData data)
    {
        var list = SummonSelectionManager.I != null ? SummonSelectionManager.I.GetAllSummonData() : null;
        if (list == null || data == null) return -1;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == data) return i;
            if (list[i] != null && list[i].name == data.name) return i;
        }

        return -1;
    }

    private static void VerifyOrFixSummon(PlayerStatus status, int authoritativeIndex, string label)
    {
        if (status == null || authoritativeIndex < 0) return;
        int local = FindSummonIndex(status.summonData);
        if (local == authoritativeIndex) return;

        var list = SummonSelectionManager.I != null ? SummonSelectionManager.I.GetAllSummonData() : null;
        if (list == null || authoritativeIndex >= list.Length || list[authoritativeIndex] == null) return;

        Debug.LogWarning(
            $"[OnlineSync] {label} summon mismatch (local={local}, host={authoritativeIndex}). Applying host value.");
        status.SetSummonData(list[authoritativeIndex]);
    }

    private static NetworkBattleBridge.ArchMagicSideSync CaptureArchMagicSideSync(PlayerStatus status)
    {
        if (status == null || !status.IsCastingArchMagic)
            return default;

        var card = status.archMagicCastingCard;
        return new NetworkBattleBridge.ArchMagicSideSync
        {
            RemainingTurns = status.archMagicRemainingTurns,
            BarrierRemaining = status.archMagicBarrierRemaining,
            CardName = card != null ? (string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName) : "",
            TargetSelf = status.archMagicEffectTarget == status,
        };
    }

    private void ApplyAuthoritativeArchMagicSide(PlayerStatus status, NetworkBattleBridge.ArchMagicSideSync sync)
    {
        if (status == null) return;

        if (sync.RemainingTurns <= 0 || string.IsNullOrEmpty(sync.CardName))
        {
            if (status.IsCastingArchMagic && !status.archMagicCancelPending)
                status.ClearArchMagicCastingState();
            _host.RefreshArchMagicBarrierUi(status);
            return;
        }

        var template = ArchMagicRules.FindTemplateByDisplayOrAssetName(sync.CardName);
        if (template == null)
        {
            Debug.LogWarning($"[OnlineSync] ArchMagic template not found: {sync.CardName}");
            return;
        }

        PlayerStatus effectTarget = _host.ResolveArchMagicEffectTarget(status, sync.TargetSelf);
        status.ApplyAuthoritativeArchMagicCasting(template, sync.RemainingTurns, effectTarget, sync.BarrierRemaining);
        _host.RefreshArchMagicBarrierUi(status);
    }

    private static List<string> CollectCardNames(List<CardData> hand)
    {
        var names = new List<string>(hand != null ? hand.Count : 0);
        if (hand == null) return names;
        foreach (var c in hand)
        {
            if (c != null)
                names.Add(c.cardName ?? "");
        }

        return names;
    }

    private void ReconcileHandToAuthoritative(
        List<CardData> hand, List<string> authoritative, bool withUi, string label)
    {
        if (hand == null || authoritative == null) return;

        var need = new Dictionary<string, int>();
        foreach (var n in authoritative)
        {
            if (string.IsNullOrEmpty(n)) continue;
            need.TryGetValue(n, out int c);
            need[n] = c + 1;
        }

        bool changed = false;
        var cardDealer = _host.CardDealer;

        for (int i = hand.Count - 1; i >= 0; i--)
        {
            var card = hand[i];
            string nm = card != null ? card.cardName : null;
            if (!string.IsNullOrEmpty(nm) && need.TryGetValue(nm, out int remain) && remain > 0)
            {
                need[nm] = remain - 1;
                continue;
            }

            Debug.LogWarning($"[OnlineSync] {label}: removing card not on host '{nm}'");
            if (withUi && card != null && card.cardUI != null)
                UnityEngine.Object.Destroy(card.cardUI.gameObject);
            hand.RemoveAt(i);
            changed = true;
        }

        foreach (var kv in need)
        {
            for (int k = 0; k < kv.Value; k++)
            {
                var template = cardDealer != null ? cardDealer.FindTemplateByName(kv.Key) : null;
                if (template == null)
                {
                    Debug.LogError($"[OnlineSync] {label}: template not found for '{kv.Key}'");
                    continue;
                }

                var instance = cardDealer.InstantiateCardFromTemplate(template);
                if (instance == null) continue;
                hand.Add(instance);
                if (withUi)
                {
                    var ui = cardDealer.CreateCardUIForHand(instance);
                    ui?.Reveal();
                }

                changed = true;
                Debug.LogWarning($"[OnlineSync] {label}: added missing card '{kv.Key}' to match host");
            }
        }

        if (changed && withUi)
            _host.SetIntroModeUi();
    }
}
