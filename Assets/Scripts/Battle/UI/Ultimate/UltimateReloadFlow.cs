using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Ascendant Shade mulligan: UltimateReloadPopup selection and hand replace/reveal (player only UI).
/// </summary>
public static class UltimateReloadFlow
{
    private static bool _popupOpen;
    private static bool _sequenceRunning;
    private static readonly List<CardData> _selection = new List<CardData>(18);
    private static TaskCompletionSource<IReadOnlyList<CardData>> _confirmTcs;
    private static GameObject _popupInstance;
    private static ReloadPopupView _popupView;

    public static bool IsPopupOpen => _popupOpen;
    public static bool IsSequenceRunning => _sequenceRunning;
    public static bool IsUiBlocking => _popupOpen || _sequenceRunning;

    public static bool IsSelected(CardData card)
    {
        if (card == null) return false;
        int id = card.GetInstanceID();
        for (int i = 0; i < _selection.Count; i++)
        {
            if (_selection[i] != null && _selection[i].GetInstanceID() == id)
                return true;
        }
        return false;
    }

    public static void OnHandCardClicked(CardData card)
    {
        if (!_popupOpen || card == null) return;

        int id = card.GetInstanceID();
        bool removed = false;
        for (int i = _selection.Count - 1; i >= 0; i--)
        {
            if (_selection[i] != null && _selection[i].GetInstanceID() == id)
            {
                _selection.RemoveAt(i);
                removed = true;
                break;
            }
        }

        if (!removed)
        {
            _selection.Add(card);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        }
        else
        {
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        }

        SyncPopupThumbnails();
        RefreshHandHighlights();
    }

    public static async Task<IReadOnlyList<CardData>> RunPlayerSelectionAsync(CancellationToken ct)
    {
        if (BattleManager.I == null || BattleUIManager.I == null)
            return Array.Empty<CardData>();

        BattleUIManager.I.HideAllCardDetails();
        BattleUIManager.I.ClearAllSelections();
        BattleUIManager.I.SetUseButtonInteractable(false);
        BattleUIManager.I.UpdateEconomicActionButtons();
        _selection.Clear();

        var prefab = Resources.Load<GameObject>("Prefab/UltimateReloadPopup");
        if (prefab == null)
        {
            Debug.LogError("[UltimateReloadFlow] Resources/Prefab/UltimateReloadPopup not found");
            return Array.Empty<CardData>();
        }

        var canvas = BattleUIManager.I.GetMainUICanvas();
        if (canvas == null) return Array.Empty<CardData>();

        _popupInstance = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
        _popupView = _popupInstance.GetComponent<ReloadPopupView>();
        if (_popupView == null)
            _popupView = _popupInstance.AddComponent<ReloadPopupView>();

        _popupView.Bind(OnConfirmClicked, null);
        _popupView.SetConfirmInteractable(true);
        _popupView.RefreshReloadCardsThumbnails(_selection, BattleManager.I.playerHand);

        _popupOpen = true;
        BattleUIManager.I?.SetHandClickable(true);
        ApplyHandInteractivity();

        _confirmTcs = new TaskCompletionSource<IReadOnlyList<CardData>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using (ct.Register(() => _confirmTcs.TrySetCanceled()))
        {
            IReadOnlyList<CardData> picked;
            try
            {
                picked = await _confirmTcs.Task;
            }
            catch (OperationCanceledException)
            {
                ClosePopupOnly();
                return Array.Empty<CardData>();
            }

            return picked ?? Array.Empty<CardData>();
        }
    }

    public static async Task RunPlayerMulliganSequenceAsync(
        BattleManager bm,
        HandRefillService handRefill,
        IReadOnlyList<CardData> toReplace,
        CancellationToken ct)
    {
        if (bm == null || handRefill == null || toReplace == null || toReplace.Count == 0) return;
        if (_sequenceRunning) return;

        _sequenceRunning = true;
        BattleUIManager.I?.SetHandClickable(false);

        IReadOnlyList<HandRefillService.HandReloadSlotWork> work;
        try
        {
            work = handRefill.BeginHandReloadReplaceAllFaceDown(toReplace, bm.playerHand);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            _sequenceRunning = false;
            BattleUIManager.I?.SetHandClickable(true);
            return;
        }

        if (work == null || work.Count == 0)
        {
            _sequenceRunning = false;
            BattleUIManager.I?.SetHandClickable(true);
            return;
        }

        float fade = BattleUIManager.I != null
            ? BattleUIManager.I.ShowHandReloadPopup(bm.GetPlayerStatus())
            : 0f;

        await DamagePopup.WaitAfterPopupLifetimeAsync(fade, ct);
        await Task.Delay(HandRefillService.HandReloadAfterPopupWaitMs, ct);

        await handRefill.RevealHandReloadSlotsSequentially(work, ct);

        _sequenceRunning = false;
        BattleUIManager.I?.SetHandClickable(true);
        bm.UpdateTotalATKDEFDisplay();
    }

    public static void ReplaceEnemyHandForMulligan(
        HandRefillService handRefill,
        IReadOnlyList<CardData> toReplace,
        List<CardData> enemyHand)
    {
        handRefill?.ReplaceEnemyHandCardsForMulligan(toReplace, enemyHand);
    }

    private static void OnConfirmClicked()
    {
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");
        var copy = new List<CardData>(_selection);
        ClosePopupOnly();
        _confirmTcs?.TrySetResult(copy);
    }

    private static void ClosePopupOnly()
    {
        _popupOpen = false;
        _selection.Clear();
        ClearHandHighlights();
        BattleUIManager.I?.SetHandClickable(false);

        if (_popupInstance != null)
        {
            UnityEngine.Object.Destroy(_popupInstance);
            _popupInstance = null;
        }

        _popupView = null;
    }

    private static void SyncPopupThumbnails()
    {
        if (_popupView == null) return;
        _popupView.RefreshReloadCardsThumbnails(_selection, BattleManager.I?.playerHand);
    }

    private static void RefreshHandHighlights()
    {
        var hand = BattleManager.I?.playerHand;
        if (hand == null) return;
        for (int i = 0; i < hand.Count; i++)
        {
            var c = hand[i];
            if (c?.cardUI == null) continue;
            c.cardUI.SetHighlight(IsSelected(c));
        }
    }

    private static void ClearHandHighlights()
    {
        var hand = BattleManager.I?.playerHand;
        if (hand == null) return;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i]?.cardUI != null)
                hand[i].cardUI.SetHighlight(false);
        }
    }

    private static void ApplyHandInteractivity()
    {
        var hand = BattleManager.I?.playerHand;
        if (hand == null) return;

        var allowed = new List<CardData>(hand.Count);
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] != null) allowed.Add(hand[i]);
        }

        BattleUIManager.I?.UpdateHandInteractivity(hand, allowed);
        BattleUIManager.I?.RefreshMagicCardInteractivity(hand);
    }
}
