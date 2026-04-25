using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手札リロード（3枚以上同一定義）。<see cref="ReloadPopupView"/> と Hierarchy の入口を想定。<br/>
/// 入口はデフォルト非表示。開幕完了後・<see cref="GameState.AttackPhase"/> かつ <see cref="BattleStep.MainActionSelect"/> で条件を満たしたときだけ表示する。それ以外の Phase は常に非表示。
/// </summary>
public class HandReloadController : MonoBehaviour
{
    public static HandReloadController I;

    [Header("Hierarchy：条件を満たすと有効。押下で ReloadPopup を開く")]
    [SerializeField] private Button reloadEntryButton;
    [Tooltip("同じ行に置くグレーアウト用の未確定ボタン（未割当可）。常に非インタラクティブのままにする")]
    [SerializeField] private Button reloadConfirmOnHud;
    [SerializeField] private GameObject reloadPopupPrefab;
    [Tooltip("未設定時は Resources.Load(\"Prefab/ReloadPopup\")")]
    [SerializeField] private bool useResourcesFallback = true;

    [Header("リロード入口：ビューポート（キャンバス）とスライドする子")]
    [Tooltip("リロードの見た目がスライドする RectTransform。未指定なら入口ボタン自身。親に RectMask2D 付きの「窓」がある想定。")]
    [SerializeField] private RectTransform reloadButtonSlideMovable;
    [Tooltip("マスク枠（この Rect 外は子を描画しない）。未指定なら入口ボタンの親。")]
    [SerializeField] private RectTransform reloadButtonClippingContainer;
    [Header("背景点滅・スライド")]
    [Tooltip("点滅させる背景 Image。未指定なら入口ボタン（Button / Image から取得）。")]
    [SerializeField] private Image reloadEntryBackgroundImage;
    [SerializeField] private float reloadButtonSlideInOffset = 200f;
    [SerializeField] private float reloadButtonSlideInDuration = 0.45f;

    private static readonly Color ReloadEntryBlinkBackgroundAlt = new Color(253f / 255f, 153f / 255f, 1f, 1f);

    private Color _reloadEntryBackgroundColorOriginal;
    private bool _hasReloadEntryBackgroundOriginal;
    private Vector2 _movableLandedAnchored;
    private bool _prevReloadAvailable;
    private CancellationTokenSource _slideCts;
    private CancellationTokenSource _blinkCts;
    private bool _slideInProgress;
    private bool _blinkInProgress;

    private GameObject _popupInstance;
    private ReloadPopupView _popupView;
    private readonly List<CardData> _reloadSelection = new List<CardData>(8);
    private bool _sequenceRunning;
    private bool _popupOpen;

    /// <summary>リロードポップアップ表示中、またはリロード演出シーケンス中。</summary>
    public bool IsHandReloadUiBlocking => _popupOpen || _sequenceRunning;

    public bool IsReloadPopupContentOpen => _popupOpen;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        if (reloadEntryButton != null)
        {
            reloadEntryButton.onClick.AddListener(OnReloadEntryClicked);
        }

        if (reloadEntryBackgroundImage == null && reloadEntryButton != null)
        {
            reloadEntryBackgroundImage = reloadEntryButton.targetGraphic as Image
                ?? reloadEntryButton.GetComponent<Image>();
        }
        if (reloadEntryBackgroundImage != null)
        {
            _reloadEntryBackgroundColorOriginal = reloadEntryBackgroundImage.color;
            _hasReloadEntryBackgroundOriginal = true;
        }

        if (GetSlideMovableOrNull() is { } mr)
        {
            _movableLandedAnchored = mr.anchoredPosition;
        }

        ForceHideReloadEntryControls();
    }

    void Start()
    {
        if (BattleManager.I != null)
            RefreshReloadEntryButton();
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        StopSlideAndBlink();
    }

    /// <summary>シーン上でアクティブのまま置かれていても、入口は隠す（条件成立時は <see cref="RefreshReloadEntryButton"/> のみ表示）。</summary>
    private void ForceHideReloadEntryControls()
    {
        StopSlideAndBlink();
        SetReloadEntryBackgroundOriginal();
        if (reloadEntryButton != null) reloadEntryButton.gameObject.SetActive(false);
        if (reloadConfirmOnHud != null) reloadConfirmOnHud.gameObject.SetActive(false);
        _prevReloadAvailable = false;
    }

    /// <summary>入口ボタンと HUD 上の偽 Confirm の表示を <see cref="PlayerCanUseReloadEntry"/> に同期する。</summary>
    public void RefreshReloadEntryButton()
    {
        if (reloadConfirmOnHud != null)
        {
            reloadConfirmOnHud.interactable = false;
        }

        bool can = PlayerCanUseReloadEntry();
        if (reloadEntryButton == null) return;

        if (!can)
        {
            StopSlideAndBlink();
            SetReloadEntryBackgroundOriginal();
            if (reloadEntryButton.gameObject.activeSelf)
                reloadEntryButton.gameObject.SetActive(false);
            if (reloadConfirmOnHud != null && reloadConfirmOnHud.gameObject.activeSelf)
                reloadConfirmOnHud.gameObject.SetActive(false);
            _prevReloadAvailable = false;
            return;
        }

        if (!reloadEntryButton.gameObject.activeSelf)
            reloadEntryButton.gameObject.SetActive(true);
        if (reloadConfirmOnHud != null && !reloadConfirmOnHud.gameObject.activeSelf)
            reloadConfirmOnHud.gameObject.SetActive(true);

        reloadEntryButton.interactable = can && !IsHandReloadUiBlocking;

        bool wasAvailable = _prevReloadAvailable;
        if (can && !wasAvailable)
        {
            _prevReloadAvailable = true;
            EnsureReloadButtonRectMask2D();
            if (GetSlideMovableOrNull() is { } m0)
            {
                m0.anchoredPosition = GetSlideInStartLocalPosition(m0);
            }
            SoundEffectPlayer.I?.Play("Assets/SE/リロード可能.mp3");
            StartSlideInAsync();
        }
        else if (can && wasAvailable && !_slideInProgress && !_blinkInProgress && !_popupOpen)
        {
            _ = StartBlinkAsync();
        }

        if (_popupOpen) SetReloadEntryBackgroundOriginal();
    }

    /// <summary>
    /// リロード入口を出してよいか。Opening / StandBy / MainAction 以外 / 以降の Phase は不許可（<see cref="BattleManager.CurrentBattleStep"/> で層3を照合）。
    /// </summary>
    public bool PlayerCanUseReloadEntry()
    {
        if (BattleManager.I == null) return false;
        if (!BattleManager.I.IsBattleOpeningSequenceComplete) return false;
        if (BattleManager.I.CurrentState != GameState.AttackPhase) return false;
        if (BattleManager.I.CurrentBattleStep != BattleStep.MainActionSelect) return false;
        if (BattleManager.I.CurrentTurnOwner != PlayerType.Player) return false;
        if (BattleManager.I.GetPlayerStatus() != null && BattleManager.I.GetPlayerStatus().IsCastingArchMagic)
            return false;
        if (BattleManager.I.IsUseButtonLocked) return false;
        if (BattleManager.I.IsGameEndTriggered) return false;
        var hand = BattleManager.I.playerHand;
        if (hand == null || hand.Count < 3) return false;
        return CardDefinitionIdentity.HandHasAnyTripletOrMore(hand);
    }

    public void OnHandCardClickedForReload(CardData card)
    {
        if (!_popupOpen || card == null) return;
        if (!IsCardAllowedForReloadPick(card)) return;

        int id = card.GetInstanceID();
        bool removed = false;
        for (int i = _reloadSelection.Count - 1; i >= 0; i--)
        {
            if (_reloadSelection[i] != null && _reloadSelection[i].GetInstanceID() == id)
            {
                _reloadSelection.RemoveAt(i);
                removed = true;
                break;
            }
        }
        if (!removed)
        {
            var h = BattleManager.I.playerHand;
            var basis = _reloadSelection.Count > 0 ? _reloadSelection[0] : card;
            int maxSame = CardDefinitionIdentity.CountSameInHand(basis, h);
            if (_reloadSelection.Count >= maxSame)
                return;
            _reloadSelection.Add(card);
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");
        }
        else
            SoundEffectPlayer.I?.Play("Assets/SE/普通カード.mp3");

        SyncPopupAfterSelectionChange();
        RefreshHandHighlights();
        ApplyReloadPopupHandInteractivity();
    }

    public bool IsReloadSelected(CardData c)
    {
        if (c == null) return false;
        int id = c.GetInstanceID();
        for (int i = 0; i < _reloadSelection.Count; i++)
        {
            if (_reloadSelection[i] != null && _reloadSelection[i].GetInstanceID() == id)
                return true;
        }
        return false;
    }

    private void OnReloadEntryClicked()
    {
        if (!PlayerCanUseReloadEntry()) return;
        if (IsHandReloadUiBlocking) return;
        CancelSlideCts();
        _slideInProgress = false;
        if (GetSlideMovableOrNull() is { } snapRt)
            snapRt.anchoredPosition = _movableLandedAnchored;
        SoundEffectPlayer.I?.Play("Assets/SE/決定ボタンを押す3.mp3");
        CancelBlinkCts();
        _blinkInProgress = false;
        SetReloadEntryBackgroundOriginal();
        OpenReloadPopup();
    }

    private void OpenReloadPopup()
    {
        if (BattleManager.I == null || BattleUIManager.I == null) return;

        BattleManager.I.CancelCurrentEconomicAction();
        ClearReloadHighlights();
        BattleUIManager.I.HideAllCardDetails();
        BattleUIManager.I.ClearAllSelections();
        BattleUIManager.I.SetUseButtonInteractable(false);
        BattleUIManager.I.UpdateEconomicActionButtons();
        _reloadSelection.Clear();

        var pref = reloadPopupPrefab;
        if (pref == null && useResourcesFallback)
            pref = Resources.Load<GameObject>("Prefab/ReloadPopup");
        if (pref == null)
        {
            Debug.LogError("[HandReloadController] ReloadPopup プレハブが見つかりません (reloadPopupPrefab または Resources/Prefab/ReloadPopup)");
            BattleManager.I?.RefreshUIFromHandReloadClose();
            return;
        }

        var canvas = BattleUIManager.I.GetMainUICanvas();
        if (canvas == null)
        {
            BattleManager.I?.RefreshUIFromHandReloadClose();
            return;
        }

        _popupInstance = Instantiate(pref, canvas.transform, false);
        _popupView = _popupInstance.GetComponent<ReloadPopupView>();
        if (_popupView != null)
        {
            _popupView.Bind(OnReloadConfirmed, OnReloadCancelled);
            _popupView.SetConfirmInteractable(false);
            _popupView.RefreshReloadCardsThumbnails(_reloadSelection, BattleManager.I.playerHand);
        }
        _popupOpen = true;
        CancelBlinkCts();
        _blinkInProgress = false;
        SetReloadEntryBackgroundOriginal();

        if (reloadConfirmOnHud != null) reloadConfirmOnHud.interactable = false;
        if (reloadEntryButton != null) reloadEntryButton.interactable = false;

        ApplyReloadPopupHandInteractivity();
        BattleUIManager.I.RefreshMagicCardInteractivity(BattleManager.I.playerHand);
        BattleUIManager.I.UpdateEconomicActionButtons();
    }

    private void OnReloadCancelled()
    {
        SoundEffectPlayer.I?.Play("Assets/SE/キャンセル4.mp3");
        ClearReloadHighlights();
        ClosePopupOnly();
        BattleManager.I?.RefreshUIFromHandReloadClose();
    }

    private static void ClearReloadHighlights()
    {
        var hand = BattleManager.I?.playerHand;
        if (hand == null) return;
        foreach (var c in hand)
        {
            if (c?.cardUI != null)
                c.cardUI.SetHighlight(false);
        }
    }

    private void ClosePopupOnly()
    {
        _popupOpen = false;
        _reloadSelection.Clear();
        if (_popupInstance != null)
        {
            Destroy(_popupInstance);
            _popupInstance = null;
        }
        _popupView = null;
        if (reloadEntryButton != null) reloadEntryButton.interactable = true;
    }

    private void OnReloadConfirmed() => _ = RunReloadConfirmSequenceAsync();

    private async Task RunReloadConfirmSequenceAsync()
    {
        if (_reloadSelection.Count < 3) return;
        if (BattleManager.I == null) return;
        if (BattleManager.I.HandRefill == null) return;
        if (_sequenceRunning) return;
        _sequenceRunning = true;
        CancelBlinkCts();
        _blinkInProgress = false;
        SetReloadEntryBackgroundOriginal();

        var toReplace = new List<CardData>(_reloadSelection);
        var bm = BattleManager.I;

        ClosePopupOnly();
        if (reloadEntryButton != null) reloadEntryButton.interactable = false;
        BattleUIManager.I?.SetHandClickable(false);

        IReadOnlyList<HandRefillService.HandReloadSlotWork> work;
        try
        {
            work = bm.HandRefill.BeginHandReloadReplaceAllFaceDown(toReplace, bm.playerHand);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            _sequenceRunning = false;
            BattleUIManager.I?.SetHandClickable(true);
            bm.RefreshUIFromHandReloadClose();
            return;
        }

        if (work == null || work.Count == 0)
        {
            _sequenceRunning = false;
            BattleUIManager.I?.SetHandClickable(true);
            bm.RefreshUIFromHandReloadClose();
            return;
        }

        float fade = BattleUIManager.I != null
            ? BattleUIManager.I.ShowHandReloadPopup(bm.GetPlayerStatus())
            : 0f;

        await DamagePopup.WaitAfterPopupLifetimeAsync(fade, CancellationToken.None);
        await Task.Delay(HandRefillService.HandReloadAfterPopupWaitMs, CancellationToken.None);

        if (bm.HandRefill != null)
            await bm.HandRefill.RevealHandReloadSlotsSequentially(work, CancellationToken.None);

        _sequenceRunning = false;
        BattleUIManager.I?.SetHandClickable(true);
        bm.UpdateTotalATKDEFDisplay();
        bm.RefreshUIFromHandReloadClose();
    }

    private void SyncPopupAfterSelectionChange()
    {
        if (_popupView == null) return;
        _popupView.RefreshReloadCardsThumbnails(_reloadSelection, BattleManager.I.playerHand);
        bool ok = _reloadSelection.Count >= 3;
        _popupView.SetConfirmInteractable(ok);
    }

    private void RefreshHandHighlights()
    {
        var hand = BattleManager.I != null ? BattleManager.I.playerHand : null;
        if (hand == null) return;
        foreach (var c in hand)
        {
            if (c?.cardUI == null) continue;
            c.cardUI.SetHighlight(IsReloadSelected(c));
        }
    }

    private void ApplyReloadPopupHandInteractivity()
    {
        var hand = BattleManager.I?.playerHand;
        if (hand == null) return;
        var allowed = new List<CardData>();
        foreach (var c in hand)
        {
            if (c == null) continue;
            if (IsCardAllowedForReloadPick(c))
                allowed.Add(c);
        }
        BattleUIManager.I?.UpdateHandInteractivity(hand, allowed);
    }

    private bool IsCardAllowedForReloadPick(CardData card)
    {
        var h = BattleManager.I?.playerHand;
        if (h == null || card == null) return false;
        if (_reloadSelection.Count == 0)
            return CardDefinitionIdentity.CountSameInHand(card, h) >= 3;
        return CardDefinitionIdentity.IsSameDefinition(card, _reloadSelection[0]);
    }

    private void EnsureReloadButtonRectMask2D()
    {
        var t = reloadButtonClippingContainer;
        if (t == null && reloadEntryButton != null)
            t = reloadEntryButton.transform.parent as RectTransform;
        if (t == null) return;
        if (t.GetComponent<RectMask2D>() == null)
            t.gameObject.AddComponent<RectMask2D>();
    }

    private void SetReloadEntryBackgroundOriginal()
    {
        if (!_hasReloadEntryBackgroundOriginal || reloadEntryBackgroundImage == null) return;
        reloadEntryBackgroundImage.color = _reloadEntryBackgroundColorOriginal;
    }

    private void SetReloadEntryBackgroundColorUi(Color c)
    {
        if (reloadEntryBackgroundImage != null) reloadEntryBackgroundImage.color = c;
    }

    private RectTransform GetSlideMovableOrNull()
    {
        if (reloadButtonSlideMovable != null) return reloadButtonSlideMovable;
        return reloadEntryButton != null ? reloadEntryButton.GetComponent<RectTransform>() : null;
    }

    private RectTransform GetSlideClipOrNull()
    {
        if (reloadButtonClippingContainer != null) return reloadButtonClippingContainer;
        if (reloadEntryButton == null) return null;
        return reloadEntryButton.transform.parent as RectTransform;
    }

    /// <summary>ビューポート下から一つに見える位置（movable の親ローカル）。親＝マスク枠想定。</summary>
    private Vector2 GetSlideInStartLocalPosition(RectTransform movable)
    {
        if (movable == null) return _movableLandedAnchored;
        var parent = movable.parent as RectTransform;
        var clip = GetSlideClipOrNull();
        if (parent != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        }
        else if (clip != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(clip);
        }
        float h = 0f;
        if (parent != null) h = parent.rect.height;
        if (h < 0.01f && clip != null) h = clip.rect.height;
        float m = movable.rect.height;
        float yDown = Mathf.Max(h, m, reloadButtonSlideInOffset, 1f);
        return new Vector2(_movableLandedAnchored.x, _movableLandedAnchored.y - yDown);
    }

    private void StopSlideAndBlink()
    {
        CancelSlideCts();
        CancelBlinkCts();
        _slideInProgress = false;
        _blinkInProgress = false;
        SetReloadEntryBackgroundOriginal();
    }

    private void CancelSlideCts()
    {
        if (_slideCts == null) return;
        _slideCts.Cancel();
        _slideCts.Dispose();
        _slideCts = null;
    }

    private void CancelBlinkCts()
    {
        if (_blinkCts == null) return;
        _blinkCts.Cancel();
        _blinkCts.Dispose();
        _blinkCts = null;
    }

    private void StartSlideInAsync()
    {
        CancelSlideCts();
        _slideCts = new CancellationTokenSource();
        _slideInProgress = true;
        _ = RunSlideInAsync(_slideCts.Token);
    }

    private async Task RunSlideInAsync(CancellationToken ct)
    {
        bool slideCompleted = false;
        try
        {
            var rt = GetSlideMovableOrNull();
            if (rt == null) return;

            Vector2 start = rt.anchoredPosition;
            Vector2 end = _movableLandedAnchored;
            float d = Mathf.Max(0.02f, reloadButtonSlideInDuration);
            float elapsed = 0f;
            while (elapsed < d)
            {
                if (ct.IsCancellationRequested) return;
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / d);
                float s = 1f - (1f - u) * (1f - u);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, s);
                await Task.Yield();
            }
            if (ct.IsCancellationRequested) return;
            rt.anchoredPosition = end;
            slideCompleted = true;
        }
        finally
        {
            _slideInProgress = false;
        }

        if (!slideCompleted || ct.IsCancellationRequested) return;
        if (PlayerCanUseReloadEntry() && !_popupOpen)
            _ = StartBlinkAsync();
    }

    private async Task StartBlinkAsync()
    {
        CancelBlinkCts();
        _blinkCts = new CancellationTokenSource();
        _blinkInProgress = true;
        var token = _blinkCts.Token;
        Color alt = ReloadEntryBlinkBackgroundAlt;
        if (_hasReloadEntryBackgroundOriginal)
            alt.a = _reloadEntryBackgroundColorOriginal.a;
        try
        {
            bool useAlt = false;
            while (reloadEntryButton != null
                   && reloadEntryButton.gameObject.activeInHierarchy
                   && _hasReloadEntryBackgroundOriginal
                   && PlayerCanUseReloadEntry()
                   && !_popupOpen
                   && !_sequenceRunning
                   && !token.IsCancellationRequested)
            {
                SetReloadEntryBackgroundColorUi(useAlt ? alt : _reloadEntryBackgroundColorOriginal);
                useAlt = !useAlt;
                float t = 0f;
                while (t < 1f)
                {
                    if (token.IsCancellationRequested) return;
                    t += Time.unscaledDeltaTime;
                    await Task.Yield();
                }
            }
        }
        finally
        {
            _blinkInProgress = false;
            if (_blinkCts != null) { _blinkCts.Dispose(); _blinkCts = null; }
            SetReloadEntryBackgroundOriginal();
        }
    }
}
