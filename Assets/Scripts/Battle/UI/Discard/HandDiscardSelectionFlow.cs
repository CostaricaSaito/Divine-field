using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Config for modular hand discard selection (Jett Booster, future effects).
/// </summary>
public sealed class HandDiscardSelectionRequest
{
    public int MinSelectCount = 1;
    public int MaxSelectCount = 1;
    public bool AllowCancel;
    public string Title = "DISCARD";
    public string Message = "\u6368\u3066\u308b\u624b\u672d\u3092\u9078\u3079";
    public string SubMessage = "\u30c7\u30a3\u30b9\u30ab\u30fc\u30c9";

    public void Validate()
    {
        if (MinSelectCount < 0) MinSelectCount = 0;
        if (MaxSelectCount < MinSelectCount) MaxSelectCount = MinSelectCount;
    }
}

/// <summary>
/// Modular hand discard: popup selection (player) or CPU pick, then shared destroy presentation.
/// </summary>
public static class HandDiscardSelectionFlow
{
    private static bool _popupOpen;
    private static HandDiscardSelectionRequest _activeRequest;
    private static readonly List<CardData> _selection = new List<CardData>(8);
    private static TaskCompletionSource<IReadOnlyList<CardData>> _confirmTcs;
    private static GameObject _popupInstance;
    private static DiscardPopupView _popupView;

    public static bool IsPopupOpen => _popupOpen;
    public static bool IsUiBlocking => _popupOpen;

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

    /// <summary>
    /// Run selection (player popup or CPU auto-pick), then panel reveal + dissolve for each discarded card.
    /// </summary>
    public static async Task RunDiscardFromHandAsync(
        HandDiscardSelectionRequest request,
        BattleManager bm,
        List<CardData> hand,
        bool isPlayerHand,
        Side displaySide,
        CancellationToken ct)
    {
        if (request == null || bm == null || hand == null || hand.Count == 0)
            return;

        request.Validate();
        if (request.MaxSelectCount <= 0)
            return;

        IReadOnlyList<CardData> picked;
        if (isPlayerHand)
            picked = await RunPlayerSelectionAsync(request, ct);
        else
            picked = HandDiscardEnemyAI.PickDiscardCards(hand, request.MaxSelectCount);

        if (picked == null || picked.Count == 0)
            return;

        for (int i = 0; i < picked.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var card = picked[i];
            if (card == null || !hand.Contains(card)) continue;

            await CardDestroyPresentation.PlayRevealAndDissolveFromHandAsync(
                bm, hand, card, isPlayerHand, displaySide, ct);
        }

        BattleUIManager.I?.RefreshMagicCardInteractivity(hand);
        bm.UpdateTotalATKDEFDisplay();
        if (isPlayerHand)
            bm.RefreshPlayerDefensePhaseInteractivity();
    }

    public static void OnHandCardClicked(CardData card)
    {
        if (!_popupOpen || _activeRequest == null || card == null) return;
        if (!HandDiscardRules.IsSelectableForDiscard(card)) return;

        int id = card.GetInstanceID();
        int existing = -1;
        for (int i = 0; i < _selection.Count; i++)
        {
            if (_selection[i] != null && _selection[i].GetInstanceID() == id)
            {
                existing = i;
                break;
            }
        }

        if (existing >= 0)
        {
            _selection.RemoveAt(existing);
        }
        else
        {
            if (_activeRequest.MaxSelectCount <= 1)
                _selection.Clear();

            if (_selection.Count >= _activeRequest.MaxSelectCount)
                return;

            _selection.Add(card);
        }

        SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        SyncPopupThumbnails();
        RefreshHandHighlights();
        UpdateConfirmButton();
    }

    private static async Task<IReadOnlyList<CardData>> RunPlayerSelectionAsync(
        HandDiscardSelectionRequest request,
        CancellationToken ct)
    {
        var bm = BattleManager.I;
        if (bm == null || BattleUIManager.I == null)
            return Array.Empty<CardData>();

        _activeRequest = request;
        _selection.Clear();

        BattleUIManager.I.HideAllCardDetails();
        BattleUIManager.I.ClearAllSelections();
        BattleUIManager.I.SetUseButtonInteractable(false);
        BattleUIManager.I.UpdateEconomicActionButtons();

        var prefab = Resources.Load<GameObject>("Prefab/DiscardPopup");
        if (prefab == null)
        {
            Debug.LogError("[HandDiscardSelectionFlow] Resources/Prefab/DiscardPopup not found");
            return Array.Empty<CardData>();
        }

        var canvas = BattleUIManager.I.GetMainUICanvas();
        if (canvas == null) return Array.Empty<CardData>();

        _popupInstance = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
        _popupView = _popupInstance.GetComponent<DiscardPopupView>();
        if (_popupView == null)
            _popupView = _popupInstance.AddComponent<DiscardPopupView>();
        _popupView.AutoWireFromHierarchy();
        _popupView.ApplyRequest(request);
        _popupView.Bind(OnConfirmClicked, OnCancelClicked);
        UpdateConfirmButton();

        _popupOpen = true;
        ApplyHandInteractivity();
        BattleUIManager.I?.SetHandClickable(true);

        _confirmTcs = new TaskCompletionSource<IReadOnlyList<CardData>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using (ct.Register(() => _confirmTcs.TrySetCanceled()))
        {
            try
            {
                return await _confirmTcs.Task;
            }
            catch (OperationCanceledException)
            {
                ClosePopupOnly();
                return Array.Empty<CardData>();
            }
        }
    }

    private static void OnConfirmClicked()
    {
        if (_activeRequest == null || _selection.Count < _activeRequest.MinSelectCount)
            return;

        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");
        var copy = new List<CardData>(_selection);
        ClosePopupOnly();
        _confirmTcs?.TrySetResult(copy);
    }

    private static void OnCancelClicked()
    {
        if (_activeRequest == null || !_activeRequest.AllowCancel) return;
        ClosePopupOnly();
        _confirmTcs?.TrySetResult(Array.Empty<CardData>());
    }

    private static void ClosePopupOnly()
    {
        _popupOpen = false;
        _activeRequest = null;
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

    private static void UpdateConfirmButton()
    {
        if (_popupView == null || _activeRequest == null) return;
        bool ok = _selection.Count >= _activeRequest.MinSelectCount
                  && _selection.Count <= _activeRequest.MaxSelectCount;
        _popupView.SetConfirmInteractable(ok);
    }

    private static void SyncPopupThumbnails()
    {
        _popupView?.RefreshDiscardCardsThumbnails(_selection);
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
            if (HandDiscardRules.IsSelectableForDiscard(hand[i]))
                allowed.Add(hand[i]);
        }

        BattleUIManager.I?.UpdateHandInteractivity(hand, allowed);
        BattleUIManager.I?.RefreshMagicCardInteractivity(hand);
    }
}
