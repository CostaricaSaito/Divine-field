using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orchestrates player hand auto-sort (Pattern A: face-down use, Pattern B: reveal).
/// </summary>
public sealed class HandSortCoordinator : MonoBehaviour
{
    public static HandSortCoordinator I { get; private set; }

    [SerializeField] private float sortAnimDuration = 1f;
    [SerializeField] private bool logSortKeys = true;

    private static int _batchSuppressDepth;
    private readonly SemaphoreSlim _sortGate = new SemaphoreSlim(1, 1);
    private HandSortPresenter _presenter;

    /// <summary>裏向きが連続したとき、ソートを1回にまとめる。</summary>
    private const int FaceDownSortDebounceMs = 50;

    private int _faceDownSortRequestId;

    public static bool IsBatchSuppressActive => _batchSuppressDepth > 0;

    public static HandSortBatchScope PushBatchSuppress() => new HandSortBatchScope();

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(this);
            return;
        }

        I = this;
        _presenter = new HandSortPresenter(sortAnimDuration);
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    public Task SortAfterFaceDownUseAsync(CancellationToken ct = default)
        => SortInternalAsync(waitForAnimation: false, context: "AfterFaceDownUse", ct);

    /// <summary>
    /// Pattern A: 複数枚が同ターンに続けて裏返るとき、デバウンス後に1回だけソートする。
    /// </summary>
    public void ScheduleSortAfterFaceDownUse()
    {
        if (IsBatchSuppressActive) return;
        int requestId = Interlocked.Increment(ref _faceDownSortRequestId);
        _ = RunDebouncedFaceDownSortAsync(requestId);
    }

    private async Task RunDebouncedFaceDownSortAsync(int requestId)
    {
        try
        {
            await Task.Delay(FaceDownSortDebounceMs);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (requestId != Volatile.Read(ref _faceDownSortRequestId))
            return;

        await SortAfterFaceDownUseAsync();
    }

    /// <param name="preDelayMs">Optional wait after reveal before sort (opening only: 500).</param>
    public async Task SortAfterRevealAsync(bool waitForAnimation, int preDelayMs = 0, CancellationToken ct = default)
    {
        if (preDelayMs > 0)
        {
            try
            {
                await Task.Delay(preDelayMs, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        await SortInternalAsync(waitForAnimation, "AfterReveal", ct);
    }

    private async Task SortInternalAsync(bool waitForAnimation, string context, CancellationToken ct)
    {
        if (IsBatchSuppressActive) return;

        var bm = BattleManager.I;
        if (bm == null || bm.playerHand == null || bm.handPanel == null) return;

        await _sortGate.WaitAsync(ct);
        try
        {
            await RunSortOnceAsync(bm.playerHand, bm.handPanel, waitForAnimation, context, ct);
        }
        finally
        {
            _sortGate.Release();
        }
    }

    private async Task RunSortOnceAsync(
        List<CardData> hand,
        Transform handPanel,
        bool waitForAnimation,
        string context,
        CancellationToken ct)
    {
        var working = HandSortRules.CollectSortableSlots(hand, handPanel);
        if (working.Count <= 1) return;

        var target = new List<HandSortRules.SortableSlot>(working);
        HandSortRules.SortSlotsInPlace(target);

        if (IsAlreadyInOrder(working, target))
            return;

        if (logSortKeys)
            HandSortRules.LogOrderedKeys(target, context);

        BattleUIManager.I?.SetHandClickable(false);

        Canvas.ForceUpdateCanvases();
        var handRt = handPanel as RectTransform;
        if (handRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRt);

        var startAnchoredByUi = HandSortRules.CaptureAnchoredByUi(working);

        HandSortRules.ApplySortToHandAndUi(hand, handPanel, working, target);

        Canvas.ForceUpdateCanvases();
        if (handRt != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(handRt);

        var endAnchoredByUi = HandSortRules.CaptureAnchoredByUi(target);

        var layoutGroup = handPanel.GetComponent<LayoutGroup>();
        bool layoutWasEnabled = layoutGroup != null && layoutGroup.enabled;
        if (layoutGroup != null)
            layoutGroup.enabled = false;

        HandSortRules.SnapAnchoredPositions(startAnchoredByUi);

        var moveJobs = BuildCardMoveJobs(target, startAnchoredByUi, endAnchoredByUi);

        Task animTask = PlayMovesAndRestoreLayoutAsync(
            layoutGroup, layoutWasEnabled, handRt, target, endAnchoredByUi, moveJobs, ct);

        if (waitForAnimation)
        {
            try
            {
                await animTask;
            }
            finally
            {
                BattleUIManager.I?.SetHandClickable(true);
            }
        }
        else
        {
            _ = RestoreHandClickableAfterAsync(animTask);
        }
    }

    private static async Task RestoreHandClickableAfterAsync(Task animTask)
    {
        try
        {
            await animTask;
        }
        catch (OperationCanceledException)
        {
            // battle phase cancelled
        }

        BattleUIManager.I?.SetHandClickable(true);
    }

    private static bool IsAlreadyInOrder(
        IReadOnlyList<HandSortRules.SortableSlot> current,
        IReadOnlyList<HandSortRules.SortableSlot> sorted)
    {
        if (current.Count != sorted.Count) return false;
        for (int i = 0; i < current.Count; i++)
        {
            if (!ReferenceEquals(current[i].Card, sorted[i].Card))
                return false;
        }

        return true;
    }

    private async Task PlayMovesAndRestoreLayoutAsync(
        LayoutGroup layoutGroup,
        bool layoutWasEnabled,
        RectTransform handRt,
        IReadOnlyList<HandSortRules.SortableSlot> sortedLeftToRight,
        IReadOnlyDictionary<CardUI, Vector2> endAnchoredByUi,
        IReadOnlyList<HandSortPresenter.CardMoveJob> moveJobs,
        CancellationToken ct)
    {
        try
        {
            await _presenter.PlayReorderCardMoveAsync(moveJobs, ct);
        }
        finally
        {
            HandSortRules.ApplySiblingOrder(sortedLeftToRight);
            HandSortRules.SnapAnchoredPositions(endAnchoredByUi);
            if (layoutGroup != null)
                layoutGroup.enabled = layoutWasEnabled;
        }
    }

    private static List<HandSortPresenter.CardMoveJob> BuildCardMoveJobs(
        IReadOnlyList<HandSortRules.SortableSlot> sortedLeftToRight,
        IReadOnlyDictionary<CardUI, Vector2> startAnchoredByUi,
        IReadOnlyDictionary<CardUI, Vector2> endAnchoredByUi)
    {
        var jobs = new List<HandSortPresenter.CardMoveJob>();
        for (int j = 0; j < sortedLeftToRight.Count; j++)
        {
            var ui = sortedLeftToRight[j].Ui;
            if (ui == null) continue;
            if (!startAnchoredByUi.TryGetValue(ui, out Vector2 start)) continue;
            if (!endAnchoredByUi.TryGetValue(ui, out Vector2 end)) continue;

            if ((start - end).sqrMagnitude < 0.25f)
                continue;

            var rt = ui.transform as RectTransform;
            if (rt == null) continue;

            jobs.Add(new HandSortPresenter.CardMoveJob(rt, start, end));
        }

        return jobs;
    }

    /// <summary>Suppresses auto-sort until disposed (C# 9–compatible; not a struct).</summary>
    public sealed class HandSortBatchScope : IDisposable
    {
        private bool _disposed;

        internal HandSortBatchScope()
        {
            _batchSuppressDepth++;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_batchSuppressDepth > 0)
                _batchSuppressDepth--;
        }
    }
}
