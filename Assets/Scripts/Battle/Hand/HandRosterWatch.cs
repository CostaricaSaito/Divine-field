using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// After a local hand change settles, publish that hand's roster once.
/// The owner of a hand is authoritative. A mirror proposal is applied to the
/// real hand only when that owner has not changed membership since the identity
/// revision the sender last accepted.
/// </summary>
public sealed class HandRosterWatch
{
    const float QuietSeconds = 0.3f;

    readonly IOnlineBattleSyncHost _host;

    readonly List<NetworkBattleBridge.HandRosterSlot> _currentOwn = new();
    readonly List<NetworkBattleBridge.HandRosterSlot> _currentMirror = new();
    readonly List<NetworkBattleBridge.HandRosterSlot> _observedOwn = new();
    readonly List<NetworkBattleBridge.HandRosterSlot> _observedMirror = new();
    readonly List<NetworkBattleBridge.HandRosterSlot> _sentOwn = new();
    readonly List<NetworkBattleBridge.HandRosterSlot> _sentMirror = new();
    readonly List<CardData> _built = new();
    readonly List<CardData> _removed = new();
    readonly List<bool> _reused = new();

    bool _disposed;
    bool _seenCard;
    int _ownRevision;
    int _ownIdentityRevision;
    int _mirrorRevision;
    int _lastAppliedPeerOwnRevision;
    int _lastAppliedPeerIdentityRevision;
    int _lastAppliedProposalRevision;
    float _lastChangeUnscaled = -1f;

    bool _hasPendingOwn;
    int _pendingOwnRevision;
    int _pendingOwnIdentityRevision;
    List<NetworkBattleBridge.HandRosterSlot> _pendingOwnSlots;

    bool _hasPendingMirror;
    int _pendingMirrorRevision;
    int _pendingObservedIdentityRevision;
    List<NetworkBattleBridge.HandRosterSlot> _pendingMirrorSlots;

    public HandRosterWatch(IOnlineBattleSyncHost host)
    {
        _host = host;
        NetworkBattleBridge.HandRosterReceived += OnRosterReceived;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NetworkBattleBridge.HandRosterReceived -= OnRosterReceived;
    }

    public void Tick()
    {
        if (_disposed || _host == null || !_host.IsOnlineMatch) return;
        if (_host.IsGameEndTriggered || _host.IsOpponentForfeitPending) return;
        if (!NetworkBattleBridge.IsInitialized) return;

        int ownCount = _host.PlayerHand != null ? _host.PlayerHand.Count : 0;
        int mirrorCount = _host.CpuHand != null ? _host.CpuHand.Count : 0;
        if (!_seenCard)
        {
            if (ownCount == 0 && mirrorCount == 0) return;
            _seenCard = true;
        }

        Capture(_host.PlayerHand, playerHand: true, _currentOwn);
        Capture(_host.CpuHand, playerHand: false, _currentMirror);

        if (!IsHandSelectionActive())
        {
            bool mirrorNamesClean = SameNames(_currentMirror, _sentMirror);
            bool ownNamesClean = SameNames(_currentOwn, _sentOwn);
            ApplyPendingOwn(mirrorNamesClean);
            ApplyPendingMirror(ownNamesClean);
            Capture(_host.PlayerHand, playerHand: true, _currentOwn);
            Capture(_host.CpuHand, playerHand: false, _currentMirror);
        }

        bool observedChanged = !Same(_currentOwn, _observedOwn) || !Same(_currentMirror, _observedMirror);
        if (observedChanged)
        {
            _lastChangeUnscaled = Time.unscaledTime;
            Copy(_currentOwn, _observedOwn);
            Copy(_currentMirror, _observedMirror);
        }

        bool needOwn = !Same(_currentOwn, _sentOwn);
        bool needMirror = !Same(_currentMirror, _sentMirror);
        if (!needOwn && !needMirror) return;
        if (_lastChangeUnscaled < 0f) return;
        if (Time.unscaledTime - _lastChangeUnscaled < QuietSeconds) return;

        Publish(needOwn, needMirror);
    }

    void OnRosterReceived(NetworkBattleBridge.HandRosterSync sync)
    {
        if (_disposed || _host == null || !_host.IsOnlineMatch) return;

        if (sync.HasOwn && sync.OwnSlots != null)
        {
            _hasPendingOwn = true;
            _pendingOwnRevision = sync.OwnRevision;
            _pendingOwnIdentityRevision = sync.OwnIdentityRevision;
            _pendingOwnSlots = sync.OwnSlots;
        }

        if (sync.HasMirror && sync.MirrorSlots != null)
        {
            _hasPendingMirror = true;
            _pendingMirrorRevision = sync.MirrorRevision;
            _pendingObservedIdentityRevision = sync.ObservedPeerIdentityRevision;
            _pendingMirrorSlots = sync.MirrorSlots;
        }
    }

    void ApplyPendingOwn(bool mirrorNamesClean)
    {
        if (!_hasPendingOwn) return;
        if (_pendingOwnRevision <= _lastAppliedPeerOwnRevision)
        {
            _hasPendingOwn = false;
            return;
        }

        if (!mirrorNamesClean) return;

        var slots = _pendingOwnSlots;
        _hasPendingOwn = false;
        _lastAppliedPeerOwnRevision = _pendingOwnRevision;
        _lastAppliedPeerIdentityRevision = _pendingOwnIdentityRevision;
        if (SameMembership(_currentMirror, slots))
            return;

        if (!AlignHand(_host.CpuHand, slots, withUi: false))
            return;

        _host.HandRefill?.ApplyHandRosterPresentation(_host.CpuHand, slots, playerHand: false);
        RememberMirrorBaseline();
        Debug.LogWarning($"[HandRoster] opponent mirror aligned to owner ({slots.Count} cards, rev={_pendingOwnRevision})");
    }

    void ApplyPendingMirror(bool ownNamesClean)
    {
        if (!_hasPendingMirror) return;
        if (_pendingMirrorRevision <= _lastAppliedProposalRevision)
        {
            _hasPendingMirror = false;
            return;
        }

        if (_pendingObservedIdentityRevision != _ownIdentityRevision)
        {
            _hasPendingMirror = false;
            return;
        }

        if (!ownNamesClean) return;

        var slots = _pendingMirrorSlots;
        _hasPendingMirror = false;
        _lastAppliedProposalRevision = _pendingMirrorRevision;
        if (SameMembership(_currentOwn, slots))
            return;

        if (!AlignHand(_host.PlayerHand, slots, withUi: true))
            return;

        _host.HandRefill?.ApplyHandRosterPresentation(_host.PlayerHand, slots, playerHand: true);
        RememberOwnObserved();
        RefreshPlayerHandUi();
        Debug.LogWarning($"[HandRoster] player hand aligned to peer effect ({slots.Count} cards, rev={_pendingMirrorRevision})");
    }

    void RememberMirrorBaseline()
    {
        Capture(_host.CpuHand, playerHand: false, _currentMirror);
        Copy(_currentMirror, _sentMirror);
        Copy(_currentMirror, _observedMirror);
    }

    void RememberOwnObserved()
    {
        Capture(_host.PlayerHand, playerHand: true, _currentOwn);
        Copy(_currentOwn, _observedOwn);
    }

    void Publish(bool needOwn, bool needMirror)
    {
        var message = new NetworkBattleBridge.HandRosterSync();
        if (needOwn)
        {
            if (!SameNames(_currentOwn, _sentOwn))
                _ownIdentityRevision++;
            _ownRevision++;
            message.HasOwn = true;
            message.OwnRevision = _ownRevision;
            message.OwnIdentityRevision = _ownIdentityRevision;
            message.OwnSlots = _currentOwn;
        }

        if (needMirror)
        {
            _mirrorRevision++;
            message.HasMirror = true;
            message.MirrorRevision = _mirrorRevision;
            message.ObservedPeerIdentityRevision = _lastAppliedPeerIdentityRevision;
            message.MirrorSlots = _currentMirror;
        }

        NetworkBattleBridge.SendHandRoster(message);

        if (needOwn)
            Copy(_currentOwn, _sentOwn);
        if (needMirror)
            Copy(_currentMirror, _sentMirror);
    }

    bool AlignHand(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots, bool withUi)
    {
        if (hand == null || slots == null) return false;
        if (SameNames(hand, slots))
        {
            if (withUi)
                EnsurePlayerUi(hand, slots);
            return true;
        }

        if (!TryBuild(hand, slots))
            return false;

        hand.Clear();
        for (int i = 0; i < _built.Count; i++)
            hand.Add(_built[i]);

        for (int i = 0; i < _removed.Count; i++)
            DestroyPlayerUi(_removed[i]);

        if (withUi)
            EnsurePlayerUi(hand, slots);

        return true;
    }

    bool TryBuild(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots)
    {
        _built.Clear();
        _removed.Clear();
        _reused.Clear();
        for (int i = 0; i < hand.Count; i++)
            _reused.Add(false);

        var dealer = _host.CardDealer;
        int max = _host.MaxHandCards > 0 ? _host.MaxHandCards : 18;
        int count = slots.Count < max ? slots.Count : max;

        for (int i = 0; i < count; i++)
        {
            int found = FindReusable(hand, i, slots[i].Name);
            if (found >= 0)
            {
                _reused[found] = true;
                _built.Add(hand[found]);
                continue;
            }

            if (dealer == null)
            {
                Debug.LogError($"[HandRoster] no card dealer for '{slots[i].Name}'");
                return false;
            }

            var template = dealer.FindTemplateByDisplayOrAssetName(slots[i].Name);
            if (template == null)
            {
                Debug.LogError($"[HandRoster] template not found for '{slots[i].Name}'");
                DiscardUncommitted(hand);
                return false;
            }

            var instance = dealer.InstantiateCardFromTemplate(template);
            if (instance == null)
            {
                DiscardUncommitted(hand);
                return false;
            }
            SealedExoDionHandTracker.RegisterAddedToHand(instance, dealer);
            _built.Add(instance);
        }

        for (int i = 0; i < hand.Count; i++)
        {
            if (_reused[i] || hand[i] == null) continue;
            _removed.Add(hand[i]);
        }

        return true;
    }

    void DiscardUncommitted(List<CardData> hand)
    {
        for (int i = 0; i < _built.Count; i++)
        {
            var card = _built[i];
            if (card == null || hand.Contains(card)) continue;
            Object.Destroy(card);
        }
    }

    int FindReusable(List<CardData> hand, int preferredIndex, string name)
    {
        if (preferredIndex >= 0 && preferredIndex < hand.Count
            && !_reused[preferredIndex]
            && hand[preferredIndex] != null
            && CardName(hand[preferredIndex]) == name)
            return preferredIndex;

        for (int i = 0; i < hand.Count; i++)
        {
            if (_reused[i] || hand[i] == null) continue;
            if (CardName(hand[i]) == name)
                return i;
        }

        return -1;
    }

    void EnsurePlayerUi(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots)
    {
        var dealer = _host.CardDealer;
        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            if (card == null) continue;

            bool created = card.cardUI == null;
            if (created && dealer != null)
                dealer.CreateCardUIForHand(card);

            if (card.cardUI != null && card.cardUI.transform.GetSiblingIndex() != i)
                card.cardUI.transform.SetSiblingIndex(i);

            bool showFace = i < slots.Count && slots[i].FaceUp && !slots[i].Used;
            if (created && showFace)
                card.cardUI?.Reveal();
        }
    }

    void RefreshPlayerHandUi()
    {
        _host.SetIntroModeUi();
        _host.UpdateTotalATKDefDisplay();
        _host.RefreshPlayerDefensePhaseInteractivity();
        BattleUIManager.I?.RefreshMagicCardInteractivity(_host.PlayerHand);
    }

    static void DestroyPlayerUi(CardData card)
    {
        if (card?.cardUI == null) return;
        var uiObject = card.cardUI.gameObject;
        card.cardUI = null;
        Object.Destroy(uiObject);
    }

    void Capture(List<CardData> hand, bool playerHand, List<NetworkBattleBridge.HandRosterSlot> into)
    {
        into.Clear();
        if (hand == null) return;

        int max = _host.MaxHandCards > 0 ? _host.MaxHandCards : 18;
        var refill = _host.HandRefill;
        for (int i = 0; i < hand.Count && into.Count < max; i++)
        {
            var card = hand[i];
            if (card == null) continue;

            bool used = playerHand
                ? IsPlayerSlotUsed(refill, card)
                : refill != null && refill.IsEnemyCardUsedThisTurn(card);
            // Opponent hand has no card view. Keep face derived from used so a mirror
            // snapshot does not fight the owner's reveal animation.
            bool faceUp = playerHand
                ? (card.cardUI == null ? !used : !card.cardUI.IsFaceDown())
                : !used;
            into.Add(new NetworkBattleBridge.HandRosterSlot
            {
                Name = CardName(card),
                FaceUp = faceUp,
                Used = used,
            });
        }
    }

    static bool IsPlayerSlotUsed(HandRefillService refill, CardData card)
    {
        if (refill != null && refill.IsPlayerCardUsedThisTurn(card))
            return true;
        return card.cardUI != null && card.cardUI.GetCardData() == null;
    }

    static bool IsHandSelectionActive()
    {
        if (CardSelectionManager.I != null && CardSelectionManager.I.SelectedCardCount > 0)
            return true;
        if (HandReloadController.I != null && HandReloadController.I.IsHandReloadUiBlocking)
            return true;
        return HandDiscardSelectionFlow.IsUiBlocking;
    }

    static string CardName(CardData card)
    {
        if (card == null) return "";
        if (!string.IsNullOrEmpty(card.cardName)) return card.cardName;
        return card.name ?? "";
    }

    static bool SameNames(List<CardData> hand, List<NetworkBattleBridge.HandRosterSlot> slots)
    {
        int handCount = 0;
        if (hand != null)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] != null) handCount++;
            }
        }

        int slotCount = slots != null ? slots.Count : 0;
        if (handCount != slotCount) return false;

        int slotIndex = 0;
        for (int i = 0; hand != null && i < hand.Count; i++)
        {
            if (hand[i] == null) continue;
            if (CardName(hand[i]) != slots[slotIndex].Name)
                return false;
            slotIndex++;
        }

        return true;
    }

    static bool SameNames(
        List<NetworkBattleBridge.HandRosterSlot> current,
        List<NetworkBattleBridge.HandRosterSlot> baseline)
    {
        int count = current != null ? current.Count : 0;
        int baseCount = baseline != null ? baseline.Count : 0;
        if (count != baseCount) return false;
        for (int i = 0; i < count; i++)
        {
            if (current[i].Name != baseline[i].Name)
                return false;
        }

        return true;
    }

    static bool SameMembership(
        List<NetworkBattleBridge.HandRosterSlot> current,
        List<NetworkBattleBridge.HandRosterSlot> incoming)
    {
        int count = current != null ? current.Count : 0;
        int incomingCount = incoming != null ? incoming.Count : 0;
        if (count != incomingCount) return false;
        for (int i = 0; i < count; i++)
        {
            if (current[i].Name != incoming[i].Name) return false;
            if (current[i].Used != incoming[i].Used) return false;
        }

        return true;
    }

    static bool Same(
        List<NetworkBattleBridge.HandRosterSlot> a,
        List<NetworkBattleBridge.HandRosterSlot> b)
    {
        int aCount = a != null ? a.Count : 0;
        int bCount = b != null ? b.Count : 0;
        if (aCount != bCount) return false;
        for (int i = 0; i < aCount; i++)
        {
            if (a[i].Name != b[i].Name) return false;
            if (a[i].FaceUp != b[i].FaceUp) return false;
            if (a[i].Used != b[i].Used) return false;
        }

        return true;
    }

    static void Copy(
        List<NetworkBattleBridge.HandRosterSlot> src,
        List<NetworkBattleBridge.HandRosterSlot> dst)
    {
        dst.Clear();
        if (src == null) return;
        for (int i = 0; i < src.Count; i++)
            dst.Add(src[i]);
    }
}
